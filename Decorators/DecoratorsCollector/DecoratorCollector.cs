using Decorators.DecoratorsCollector.IsDecoratorChecker;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Decorators.DecoratorsCollector.DecoratorClass;

namespace Decorators.DecoratorsCollector
{

    public static class DecoratorCollector
    {
        /// <summary>
        /// return an IEnumerble of methods which are decorators
        /// </summary>
        /// <param name="project"></param>
        /// <param name="checker"></param>
        /// <returns></returns>
        internal async static Task<IEnumerable<IDecorator>> GetDecorators(Project project, IDecoratorChecker checker)
        {
            var compilation =await project.GetCompilationAsync();
            List<IDecorator> decorators = new List<IDecorator>();
            foreach (var docId in project.DocumentIds)
            {
                var doc = project.GetDocument(docId);
                var syntaxTree = await doc.GetSyntaxTreeAsync();
                var root = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                decorators.AddRange(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(node => checker.IsDecorator(node, semanticModel)).Select(n=> new DecoratorTypeFunctionToFunction(n, semanticModel)));
                decorators.AddRange(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(node => checker.IsDecorator(node, semanticModel)).Select(n => new DecoratorTypeClassToFunction(n, semanticModel)));
            }
            return decorators;
        }

    }
}
