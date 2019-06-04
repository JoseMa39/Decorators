using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.DecoratorClass
{
    internal interface IDecorator
    {
        TypeDecorator Type { get;}

        string Identifier { get;}

        IEnumerable<UsingDirectiveSyntax> GetUsingNamespaces();

        string CurrentNamespaces { get; }

        MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol);

        ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr);

    }
}
