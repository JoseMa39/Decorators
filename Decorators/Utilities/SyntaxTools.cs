using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities
{
    internal static class SyntaxTools
    {
        internal static bool HasGenericTypes(ClassDeclarationSyntax member)
        {
            return member.TypeParameterList != null && member.TypeParameterList.Parameters.Count > 0;
        }

        //verifica si tiene tipos genericos
        internal static bool HasGenericTypes(MethodDeclarationSyntax member)
        {
            return member.TypeParameterList != null && member.TypeParameterList.Parameters.Count > 0;
        }


        //construye <t2,t3> de args para genericNameSyntax a partir de <t2,t3> de parametros
        internal static TypeArgumentListSyntax MakeArgsFromParams(TypeParameterListSyntax parametersList)
        {
            return SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(parametersList.Parameters.Select(n => SyntaxFactory.IdentifierName(n.Identifier.Text))));
        }
    }
}
