using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Decorators.DecoratorsCollector.IsDecoratorChecker
{
    internal interface IDecoratorChecker
    {
        bool IsDecorator(SyntaxNode node, SemanticModel model);
    }
}