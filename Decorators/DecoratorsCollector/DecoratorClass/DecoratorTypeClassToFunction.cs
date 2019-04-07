using Microsoft.CodeAnalysis;
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
        readonly ClassDeclarationSyntax _decorator;
        readonly SemanticModel semanticModel;

        TypeDecorator type;
       
        public DecoratorTypeClassToFunction(ClassDeclarationSyntax decorator, SemanticModel semanticModel)
        {
            this._decorator = decorator;
            this.semanticModel = semanticModel;
            type = TypeDecorator.Class;
        }
        public SyntaxNode DecoratorNode => _decorator;

        public string Identifier { get => throw new NotImplementedException();}
        public TypeDecorator Type { get => type; }

        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr = null)
        {
            throw new NotImplementedException();
        }

        public MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            throw new NotImplementedException();
        }
    }
}
