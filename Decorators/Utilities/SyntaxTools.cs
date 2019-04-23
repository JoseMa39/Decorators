using Decorators.Utilities.ErrorLogger;
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

        #region Constructors Names Functions
        internal static string GetFuncPrivateName(string identifier)
        {
            return FormatterStringNames(identifier, "Private");
        }

        internal static string GetStaticDelegatePrivateName(string identifier)
        {
            return FormatterStringNames(identifier, "Decorated");
        }

        internal static string GetStaticClassPrivateName(string identifier)
        {
            return FormatterStringNames(identifier, "PrivateClass");
        }

        internal static string FormatterStringNames(string identifier, string modifier)
        {
            return "__" + identifier + modifier;
        }

        #endregion



        //construye <t2,t3> de args para genericNameSyntax a partir de <t2,t3> de parametros
        internal static TypeArgumentListSyntax MakeArgsFromParams(TypeParameterListSyntax parametersList)
        {
            return SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(parametersList.Parameters.Select(n => SyntaxFactory.IdentifierName(n.Identifier.Text))));
        }

        internal static bool CheckErrors(Compilation compilation, IErrorLog log) //chequea si el copmilation tiene algun error
        {
            if (compilation.GetDiagnostics().Where(n => n.Severity == DiagnosticSeverity.Error).Any())   //si el project tiene algun error de compilacion
            {
                log.AddErrors(compilation.GetDiagnostics().Where(n => n.Severity == DiagnosticSeverity.Error).Select(d => new DiagnosticMessage(d.Location.SourceTree.FilePath, d.Location.GetLineSpan().StartLinePosition.Line, d.GetMessage(), Severity.Error)));
                return true;
            }
            return false;
        }
    }
}
