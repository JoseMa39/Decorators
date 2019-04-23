using Decorators.DecoratorsCollector.DecoratorClass;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.IsDecoratorChecker
{
    internal interface IDecoratorChecker
    {
        /// <summary>
        /// Return true if node is a decorator
        /// </summary>
        /// <param name="node"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        bool IsDecorator(SyntaxNode node, SemanticModel model);

        /// <summary>
        /// Return true if the attribute is a decorator attribute
        /// </summary>
        /// <param name="attr"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        bool IsDecorateAttr(AttributeSyntax attr, SemanticModel semanticModel);

        /// <summary>
        /// Extract the full name of a decorator function or class
        /// </summary>
        /// <param name="attr"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        string ExtractDecoratorFullNameFromAttr(AttributeSyntax attr, SemanticModel semanticModel);

        /// <summary>
        /// extract all decorators declarated in project
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        Task<IEnumerable<IDecorator>> GetDecorators(Project project);
    }
}