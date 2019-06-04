using Decorators.CodeInjections.ClassesToCreate;
using Decorators.DecoratorsCollector.IsDecoratorChecker;
using Decorators.Utilities;
using DecoratorsDLL.DecoratorsClasses.DynamicTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.CodeInjections
{
    internal class SpecificDecoratorClassRewriterVisitor : SpecificDecoratorRewriter
    {
        protected ClassDeclarationSyntax classDecorator;
        string nameSpecificDecoratorgenerated;
        public SpecificDecoratorClassRewriterVisitor(SemanticModel modeloSemanticoDecorator, IMethodSymbol toDecoratedMethodSymbol, ClassDeclarationSyntax decoratorClass, MethodDeclarationSyntax toDecorated, string nameSpecificDecoratorgenerated) : base(modeloSemanticoDecorator, toDecoratedMethodSymbol, toDecorated)
        {
            this.classDecorator = decoratorClass;
            this.decoratorMethod = GetDecoratorMethod();
            this.nameSpecificDecoratorgenerated = nameSpecificDecoratorgenerated;
        }

        

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = base.VisitClassDeclaration(node) as ClassDeclarationSyntax;

            if (node.Identifier.Text!= this.classDecorator.Identifier.Text)  //si es una clase anidada dentro del decorador
            {
                return node;
            }

            node = node.WithIdentifier(SyntaxFactory.Identifier(this.nameSpecificDecoratorgenerated)).WithBaseList(null);   //para quitar que herede del decoratorAttribute

            //si hace falta generar clase, anado una annotation ("using", cantParams) para luego poder anadir la referencia correspondiente y generar la clase
            if (node.DescendantTokens().OfType<SyntaxToken>().Where(n => n.Kind() == SyntaxKind.IdentifierToken && n.Text == (paramClassGenerated + cantArgumentsToDecorated.ToString())).Any())
                node = node.WithAdditionalAnnotations(new SyntaxAnnotation("using", cantArgumentsToDecorated.ToString()));

            return node.WithConstraintClauses(toDecorated.ConstraintClauses).WithTypeParameterList(toDecorated.TypeParameterList).WithModifiers(SyntaxTools.AddingPrivateModifier(node.Modifiers)).WithTriviaFrom(toDecorated);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            node = base.VisitConstructorDeclaration(node) as ConstructorDeclarationSyntax;
            return node.WithIdentifier(SyntaxFactory.Identifier(this.nameSpecificDecoratorgenerated));
        }
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            bool isWrapper = IsWrapperDecorator(node);
            bool isDecoratorMethod = IsDecoratorMethod(node);

            //guardo current args name por si hay mas de una funcion anidada
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
            {
                currentArgsName = node.ParameterList.Parameters[0].Identifier.Text;
                currentparamsName = this.paramsName + (++this.deep);
            }

            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;

            if (!isDecoratorMethod)  //si no es el decorador
            {
                if (isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
                {
                    var parameters = SyntaxFactory.ParameterList();
                    //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                    var paramArray = MakingParameters();
                    //actualizando tipo de retorno
                    node = node.WithParameterList(parameters.AddParameters(paramArray)).WithReturnType(toDecorated.ReturnType);
                    node = node.WithBody(WorkingWithWrapperFunction(node.Body) as BlockSyntax);
                    currentparamsName = this.paramsName + (--this.deep);
                }
                currentArgsName = temp;
                return node;
            }

            SimpleNameSyntax fun;
            if (hasModifierParams)
                fun = SyntaxTools.MakingDelegateName(toDecorated,toDecoratedMethodSymbol);
            else
                fun = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), MakingFuncDelegateTypeArguments());


            //func<int,int,int>  y cambiando el nombre a __DecoratorToDecorate
            node = node.WithReturnType(fun);

            //generando el parametro para el decorador
            ParameterSyntax param = SyntaxFactory.Parameter(node.ParameterList.Parameters[0].Identifier).WithType(fun);

            node = node.WithParameterList(SyntaxFactory.ParameterList().AddParameters(param).WithTriviaFrom(node.ParameterList));
            node = node.WithModifiers(SyntaxFactory.TokenList(node.Modifiers.Where(m => m.Kind() != SyntaxKind.OverrideKeyword)));   //quitando override
            return node; ;
        }

        private MethodDeclarationSyntax GetDecoratorMethod()
        {
            return classDecorator.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.Text == "Decorator" && IsDecoratorMethod(m));
        }

        private bool IsDecoratorMethod(MethodDeclarationSyntax node)  //comprueba que tenga un parametro func<...> y tipo de retorno igual
        {
            string decoratorParamType = $"System.Func<{typeof(DynamicParamsCollection).FullName}, {typeof(DynamicResult).FullName}>";
            IMethodSymbol methodSymbol = modeloSemanticoDecorator.GetDeclaredSymbol(node) as IMethodSymbol;

            return (methodSymbol.Parameters.Count() == 1 && methodSymbol.Parameters[0].OriginalDefinition.Type.ToDisplayString() == decoratorParamType && methodSymbol.ReturnType.ToDisplayString() == decoratorParamType);
        }
    }
}
