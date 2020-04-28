using Decorators.DecoratorsCollector.DecoratorClass;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.PredefinedDecorators
{
    internal abstract class PredefinedDecorator : DecoratorTypeFunctionToFunction
    {
        //To Create Specific Decorators
        protected GenericNameSyntax GetFuncSyntax(MethodDeclarationSyntax methodToDecorated)
        {
            var TypesList = SyntaxFactory.SeparatedList(methodToDecorated.ParameterList.Parameters.Select(p => p.Type));
            TypesList = TypesList.Add(methodToDecorated.ReturnType);

            return SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("Func"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            TypesList));
        }

        protected SeparatedSyntaxList<TupleElementSyntax> GetParametersAsTuple(MethodDeclarationSyntax methodToDecorated)
        {
            return SyntaxFactory.SeparatedList(methodToDecorated.ParameterList.Parameters.Select(
                p => SyntaxFactory.TupleElement(p.Type)));
        }

        protected ParameterListSyntax GetLambdaParameters(MethodDeclarationSyntax methodToDecorated)
        {
            var parameterList = new SeparatedSyntaxList<ParameterSyntax>();

            int k = 1;
            foreach (var p in methodToDecorated.ParameterList.Parameters)
            {
                parameterList = parameterList.Add(
                    SyntaxFactory.Parameter(
                        SyntaxFactory.Identifier($"arg{k++}"))
                    .WithType(
                        p.Type));
            }

            var ans = SyntaxFactory.ParameterList(
                        parameterList);

            return ans;
        }

        protected SeparatedSyntaxList<ArgumentSyntax> GetArgumentsSyntax(MethodDeclarationSyntax methodToDecorated)
        {
            var argumentList = new SeparatedSyntaxList<ArgumentSyntax>();

            int k = 1, paramsCount = methodToDecorated.ParameterList.Parameters.Count;
            while (k <= paramsCount)
            {
                argumentList = argumentList.Add(
                    SyntaxFactory.Argument(
                        SyntaxFactory.IdentifierName($"arg{k++}")));
            }

            return argumentList;
        }

    }

}
