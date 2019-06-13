using Decorators.CodeInjections;
using Decorators.DecoratorsCollector.IsDecoratorChecker;
using Decorators.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.DecoratorClass
{
    class DecoratorTypeClassToFunction : IDecorator
    {
        //
        readonly ClassDeclarationSyntax _decorator;
        readonly SemanticModel semanticModel;
        IDecoratorChecker checker;
        TypeDecorator type;
       
        public DecoratorTypeClassToFunction(ClassDeclarationSyntax decorator, SemanticModel semanticModel, IDecoratorChecker checker)
        {
            this._decorator = decorator;
            this.semanticModel = semanticModel;
            this.checker = checker;
            type = TypeDecorator.Class;
        }

        #region IDecorator Methods
        //public SyntaxNode DecoratorNode => _decorator;

        public string Identifier { get => this._decorator.Identifier.Text;}
        public TypeDecorator Type { get => type; }

        public string CurrentNamespaces {
            get
            {
                return this._decorator.Ancestors().OfType<NamespaceDeclarationSyntax>().First().Name.WithoutTrivia().ToFullString();
            }
        }


        //(new Decorator()).Decorator(expr)
        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr, SemanticModel modelTodecorated)
        {
            var methodToDecorated = toDecorated as MethodDeclarationSyntax;


            string nameDecorator = GetNameSpecificDecorator(methodToDecorated.Identifier.Text);
            TypeSyntax type;

            if (SyntaxTools.HasGenericTypes(methodToDecorated))
                type = SyntaxFactory.GenericName(SyntaxFactory.Identifier(nameDecorator), SyntaxTools.MakeArgsFromParams(methodToDecorated.TypeParameterList));
            else type = SyntaxFactory.IdentifierName(nameDecorator);


            var objectCreation = SyntaxFactory.ObjectCreationExpression(type.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" ")), GetParameters(methodToDecorated, toDecoratedSymbol,attr, modelTodecorated), null);
            var parenthesizeddObjectCreation = SyntaxFactory.ParenthesizedExpression(objectCreation);
            var accessExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, parenthesizeddObjectCreation, SyntaxFactory.IdentifierName("Decorator"));
            return SyntaxFactory.InvocationExpression(accessExpr, SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(expr)));
        }

        //Devuelve la lista con los argumentos correspondientes a un decorador en especifico

        public MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            string nameSpecificDecorator = GetNameSpecificDecorator((toDecorated as MethodDeclarationSyntax).Identifier.Text);
            var specificDecorator = new SpecificDecoratorClassRewriterVisitor(semanticModel,toDecoratedSymbol,this._decorator,toDecorated as MethodDeclarationSyntax, nameSpecificDecorator);
            return specificDecorator.Visit(this._decorator) as ClassDeclarationSyntax;
        }

        public IEnumerable<UsingDirectiveSyntax> GetUsingNamespaces()
        {
            return this._decorator.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>();
        }
        #endregion


        #region Tools
        private string GetNameSpecificDecorator(string nameMethodToDecorated)  //devuelve el nombre con que se generaran los decoradores de este tipo
        {
            return SyntaxTools.FormatterStringNames(_decorator.Identifier.Text, nameMethodToDecorated);
        }

        private ArgumentListSyntax GetParameters(MethodDeclarationSyntax methodToDecorated, IMethodSymbol toDecoratedSymbol,AttributeSyntax attr, SemanticModel modelTodecorated)
        {
            if (attr.ChildNodes().OfType<AttributeArgumentListSyntax>().Any())  //si tiene los argumentos en el attribute
            {
                var argList = SyntaxFactory.ArgumentList();
                var attrArgList = attr.ChildNodes().OfType<AttributeArgumentListSyntax>().First();
                foreach (var item in attrArgList.Arguments)
                {
                    argList = argList.AddArguments(SyntaxFactory.Argument(item.Expression));
                }
                return argList;
            }
            return GetParametersFromMethodDeclaration(methodToDecorated, toDecoratedSymbol, attr, modelTodecorated);
        }

        //Busca los parametros en el cuerpo de la funcion a decorar de algo como Log a = new Log("bla")
        private ArgumentListSyntax GetParametersFromMethodDeclaration(MethodDeclarationSyntax methodToDecorated, IMethodSymbol toDecoratedSymbol, AttributeSyntax attr, SemanticModel modelTodecorated)
        {
            var typeAttr = modelTodecorated.GetTypeInfo(attr).Type as INamedTypeSymbol;
            int index = -1;
            foreach (var attributeList in methodToDecorated.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var currentAttrType = modelTodecorated.GetTypeInfo(attribute).Type as INamedTypeSymbol;
                    if (typeAttr.Name == currentAttrType.Name && !attribute.ChildNodes().OfType<AttributeArgumentListSyntax>().Any())
                    {
                        index++;
                        if (attribute == attr)
                        {
                            try
                            {
                                var objectCreationAttr = methodToDecorated.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(n => modelTodecorated.GetTypeInfo(n).Type.OriginalDefinition.ToDisplayString() == typeAttr.OriginalDefinition.ToDisplayString());
                                return TransformArgumentList(methodToDecorated, toDecoratedSymbol, objectCreationAttr.Skip(index).First().ArgumentList, modelTodecorated);
                            }
                            catch
                            {
                                return SyntaxFactory.ArgumentList();  //en caso de no encontrar ninguno
                            }
                        }
                    }
                }
            }
            return SyntaxFactory.ArgumentList();
        }


        //analiza los parametros igual que el decorador pues estos forman parte del decorador, si un decorador recibe un lambda de paramsCollection a bool,
        //al pasarle el parametro tambien se le pasara un paramscollection y hay que transformarlo
        private ArgumentListSyntax TransformArgumentList(MethodDeclarationSyntax methodToDecorated, IMethodSymbol toDecoratedSymbol, ArgumentListSyntax argumentList, SemanticModel modelTodecorated)
        {
            var newArgList = SyntaxFactory.SeparatedList<ArgumentSyntax>();
            foreach (var item in argumentList.Arguments) 
            {
                SpecificDecoratorRewriter paramAnalizer = new SpecificDecoratorRewriter(modelTodecorated, toDecoratedSymbol, methodToDecorated);
                newArgList = newArgList.Add(item.WithExpression(paramAnalizer.Visit(item.Expression) as ExpressionSyntax));
            }
            return argumentList.WithArguments(newArgList);
        }




        #endregion


    }
}
