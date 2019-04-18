using Decorators.CodeInjections;
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
    internal enum TypeDecorator {Function, Class}
    class DecoratorTypeFunctionToFunction:IDecorator
    {
        readonly MethodDeclarationSyntax _decorator;
        readonly SemanticModel semanticModel;
        TypeDecorator type;
        public DecoratorTypeFunctionToFunction(MethodDeclarationSyntax decorator, SemanticModel semanticModel)
        {
            this._decorator = decorator;
            this.semanticModel = semanticModel;
            type = TypeDecorator.Function;
        }

        #region IDecorator
        public SyntaxNode DecoratorNode => _decorator;

        public string Identifier { get => this._decorator.Identifier.Text;}
        public TypeDecorator Type { get => type; }

        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr)
        {
            var node = toDecorated as MethodDeclarationSyntax;

            string nameDecorator = GetNameSpecificDecorator(node.Identifier.Text);
            TypeSyntax type;

            if (SyntaxTools.HasGenericTypes(node))
                type = SyntaxFactory.GenericName(SyntaxFactory.Identifier(nameDecorator), SyntaxTools.MakeArgsFromParams(node.TypeParameterList));
            else type = SyntaxFactory.IdentifierName(nameDecorator);


            return SyntaxFactory.InvocationExpression(type, SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(expr)));
        }

        public MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            SpecificDecoratorFuncRewriterVisitor deco = new SpecificDecoratorFuncRewriterVisitor(this.semanticModel, toDecoratedSymbol, this._decorator , toDecorated as MethodDeclarationSyntax);
            var newDecorator = deco.Visit(_decorator);
            return newDecorator as MethodDeclarationSyntax;
        }

        #endregion

        #region tools
        private string GetNameSpecificDecorator(string nameMethodToDecorated)  //devuelve el nombre con que se generaran los decoradores de este tipo
        {
            return "__" + _decorator.Identifier.Text + nameMethodToDecorated;
        }

        #endregion
    }
}
