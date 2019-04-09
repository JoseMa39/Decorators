using Decorators.CodeInjections;
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

        public SyntaxNode DecoratorNode => _decorator;

        public string Identifier { get => this._decorator.Identifier.Text;}
        public TypeDecorator Type { get => type; }

        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr)
        {
            var node = toDecorated as MethodDeclarationSyntax;
            
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__" + this.Identifier + node.Identifier.Text), SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(expr)));
        }

        public MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            SpecificDecoratorFuncRewriterVisitor deco = new SpecificDecoratorFuncRewriterVisitor(this.semanticModel, toDecoratedSymbol, this._decorator , toDecorated as MethodDeclarationSyntax);
            var newDecorator = deco.Visit(_decorator);
            return newDecorator as MethodDeclarationSyntax;
        }
    }
}
