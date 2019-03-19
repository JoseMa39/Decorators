using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Decorators.DecoratorsCollector
{
    internal interface IDecoratorChecker
    {
        bool IsDecorator(MethodDeclarationSyntax node, SemanticModel model);
    }
}