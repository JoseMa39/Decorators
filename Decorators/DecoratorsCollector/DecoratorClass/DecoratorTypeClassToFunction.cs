using Decorators.CodeInjections;
using Decorators.DecoratorsCollector.IsDecoratorChecker;
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
        public SyntaxNode DecoratorNode => _decorator;

        public string Identifier { get => this._decorator.Identifier.Text;}
        public TypeDecorator Type { get => type; }


        //(new Decorator()).Decorator(expr)
        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr)
        {
            var methodToDecorated = toDecorated as MethodDeclarationSyntax;
            var objectCreation = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName(GetNameSpecificDecorator(methodToDecorated.Identifier.Text)).WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" ")), GetParameters(methodToDecorated,attr), null);
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
        #endregion


        #region Tools
        private string GetNameSpecificDecorator(string nameMethodToDecorated)  //devuelve el nombre con que se generaran los decoradores de este tipo
        {
            return "__" + _decorator.Identifier.Text + nameMethodToDecorated;
        }

        private ArgumentListSyntax GetParameters(MethodDeclarationSyntax methodToDecorated, AttributeSyntax attr)
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
            return GetParametersFromMethodDeclaration(methodToDecorated, attr);
        }

        //Busca los parametros en el cuerpo de la funcion a decorar de algo como Log a = new Log("bla")
        private ArgumentListSyntax GetParametersFromMethodDeclaration(MethodDeclarationSyntax methodToDecorated, AttributeSyntax attr)
        {
            var typeAttr = semanticModel.GetTypeInfo(attr).Type as INamedTypeSymbol;
            int index = -1;
            foreach (var attributeList in methodToDecorated.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var currentAttrType = semanticModel.GetTypeInfo(attribute).Type as INamedTypeSymbol;
                    if (typeAttr.Name == currentAttrType.Name && !attribute.ChildNodes().OfType<AttributeArgumentListSyntax>().Any())
                    {
                        index++;
                        if (attribute == attr)
                        {
                            var objectCreationAttr = methodToDecorated.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(n => semanticModel.GetTypeInfo(n).Type.OriginalDefinition.ToDisplayString() == typeAttr.OriginalDefinition.ToDisplayString());
                            return objectCreationAttr.Skip(index).First().ArgumentList;
                        }
                    }
                }
            }
            return SyntaxFactory.ArgumentList();
        }


        #endregion


    }
}
