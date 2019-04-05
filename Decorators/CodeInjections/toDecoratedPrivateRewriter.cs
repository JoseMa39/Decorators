using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Decorators.DecoratorsCollector;
using Decorators.DecoratorsCollector.IsDecoratorChecker;

namespace Decorators.CodeInjections
{
    class ToDecoratedPrivateRewriter: CSharpSyntaxRewriter
    {
        readonly MethodDeclarationSyntax toDecoratedMethod;
        readonly SemanticModel modeloSemanticoToDecoratedMethod;
        readonly IMethodSymbol toDecoratedMethodSymbol;

        readonly string instanceName;
        public ToDecoratedPrivateRewriter(MethodDeclarationSyntax toDedecoratedMethod, SemanticModel modeloSemanticoToDecoratedMethod, IMethodSymbol toDecoratedMethodSymbol)
        {
            this.toDecoratedMethodSymbol = toDecoratedMethodSymbol;
            this.modeloSemanticoToDecoratedMethod = modeloSemanticoToDecoratedMethod;
            this.toDecoratedMethod = toDedecoratedMethod;

            instanceName = "instance";

        }


        #region Visitor Functions
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            SyntaxToken name = SyntaxFactory.Identifier("__" + node.Identifier.ToString() + "Private");
            var attrList = GetNoDecoratorAttrs();

            if (toDecoratedMethodSymbol.IsStatic && node == toDecoratedMethod)  //si es estatico solo hay que cambiarle el nombre
            {
                return node.WithIdentifier(name).WithAttributeLists(attrList).WithModifiers(AddingPrivateModifier());
            }
            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;

            return node.WithParameterList(MakeNewParametersList()).WithIdentifier(name).WithAttributeLists(attrList).WithModifiers(AddingPrivateModifier()).AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));

        }


        //cambia this por instanceName
        public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
        {
            return SyntaxFactory.IdentifierName(this.instanceName).WithTriviaFrom(node);
        }

        //revisa donde quiera que haga falta poner instance.
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            node =  base.VisitIdentifierName(node) as IdentifierNameSyntax;

            var identifierSymbol = modeloSemanticoToDecoratedMethod.GetSymbolInfo(node).Symbol;

            if (!(node.Parent is MemberAccessExpressionSyntax))   //si no forma parte de una expresion de la forma a.method(), entonces tengo que poner la instancia del objeto
            {
                if(identifierSymbol.Kind == SymbolKind.Field || identifierSymbol.Kind == SymbolKind.Property || (identifierSymbol.Kind == SymbolKind.Method && identifierSymbol.ContainingType == toDecoratedMethodSymbol.ReceiverType && !identifierSymbol.IsStatic))
                {
                    return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(this.instanceName), node.WithoutLeadingTrivia()).WithTriviaFrom(node);
                }
            }

            return node;

        }

        #endregion



        #region Tools
        //Deja una lista con los atributos que no son de la dll de decoradores
        private SyntaxList<AttributeListSyntax> GetNoDecoratorAttrs()
        {
            var atributos = SyntaxFactory.SeparatedList<AttributeSyntax>(this.toDecoratedMethod.DescendantNodes().OfType<AttributeSyntax>().Where(n => (new DecoratorAttrChecker()).IsDecorateAttr(n)));
            AttributeListSyntax listaAtr = SyntaxFactory.AttributeList(atributos);
            List<AttributeListSyntax> lista = new List<AttributeListSyntax>();
            lista.Add(listaAtr);
            SyntaxList<AttributeListSyntax> aux = SyntaxFactory.List<AttributeListSyntax>();

            if (lista.Count > 0)
                aux.AddRange(lista);

            return aux;
        }


        //Pone el modificador private
        private SyntaxTokenList AddingPrivateModifier()
        {
            if (toDecoratedMethodSymbol.DeclaredAccessibility != Accessibility.Private)   //se encarga de ponerle private al metodo
                return SyntaxFactory.TokenList(toDecoratedMethod.Modifiers.Replace(toDecoratedMethod.Modifiers.First(m => IsAccesibilityModifiers(m)), SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTriviaFrom(toDecoratedMethod.Modifiers.First(m => IsAccesibilityModifiers(m)))));
            return toDecoratedMethod.Modifiers;
        }
        //agregando parametro classContainer instance
        private ParameterListSyntax MakeNewParametersList()
        {
            var separatedList  = SyntaxFactory.SeparatedList<ParameterSyntax>().Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(this.instanceName)).WithType(SyntaxFactory.IdentifierName(toDecoratedMethodSymbol.ReceiverType.Name).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "))));
            separatedList = separatedList.AddRange(this.toDecoratedMethod.ParameterList.Parameters);
            return SyntaxFactory.ParameterList(separatedList).WithTriviaFrom(toDecoratedMethod.ParameterList);
        }

        //dice si es un modificador de visibilidad distinto de private
        private bool IsAccesibilityModifiers(SyntaxToken m)
        {
            return m.Kind() == SyntaxKind.PublicKeyword || m.Kind() == SyntaxKind.ProtectedKeyword || m.Kind() == SyntaxKind.InternalKeyword;
        }

        #endregion
    }
}
