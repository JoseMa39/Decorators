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

        internal static string GetDelegateConstrName(string identifier)
        {
            return FormatterStringNames(identifier, "DelegateConstr");
        }

        internal static string FormatterStringNames(string identifier, string modifier)
        {
            return "__" + identifier + modifier;
        }

       //Verifica si la función a decorar tiene parametros con modificadores
        internal static bool HasParamsModifiers(IMethodSymbol toDecoratedSymbol)
        {
            foreach (var item in toDecoratedSymbol.Parameters)
            {
                if (item.RefKind != RefKind.None)
                    return true;
            }
            return false;
        }

        //Pone el modificador private
        

        internal static SyntaxTokenList AddingPrivateModifier(SyntaxTokenList modifiers)
        {
            var visibilityMod = modifiers.Where(m => IsAccesibilityModifiers(m));
            if (visibilityMod.Count()!= 0)
            {
                var modf = visibilityMod.First();
                return modifiers.Replace(modf, SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTriviaFrom(modf));
            }
            return modifiers;
        }

        //dice si es un modificador de visibilidad distinto de private
        internal static bool IsAccesibilityModifiers(SyntaxToken m)
        {
            return m.Kind() == SyntaxKind.PublicKeyword || m.Kind() == SyntaxKind.ProtectedKeyword || m.Kind() == SyntaxKind.InternalKeyword || m.Kind() == SyntaxKind.PrivateKeyword;
        }

        //construye MyDelegate<>
        internal static SimpleNameSyntax MakingDelegateName(MethodDeclarationSyntax toDecorated,IMethodSymbol toDecoratedMethodSymbol)
        {
            string name = SyntaxTools.GetDelegateConstrName(toDecorated.Identifier.Text);
            if (toDecoratedMethodSymbol.IsGenericMethod)
                return SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxTools.MakeArgsFromParams(toDecorated.TypeParameterList)).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "));
            return SyntaxFactory.IdentifierName(name).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "));
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
