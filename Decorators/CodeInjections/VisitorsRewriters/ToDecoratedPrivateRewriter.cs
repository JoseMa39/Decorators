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
using Decorators.Utilities;

namespace Decorators.CodeInjections
{

    //visitor que construye el ast de la funcion a decorar (si esta es de instancia entonces se le anade un parametro y se cambian todas las referencias a this hacia ese parametro)
    class ToDecoratedPrivateRewriter: CSharpSyntaxRewriter
    {
        readonly MethodDeclarationSyntax toDecoratedMethod;
        readonly SemanticModel modeloSemanticoToDecoratedMethod;
        readonly IMethodSymbol toDecoratedMethodSymbol;
        readonly IDecoratorChecker checker;

        readonly string instanceName;
        public ToDecoratedPrivateRewriter(MethodDeclarationSyntax toDecoratedMethod, SemanticModel modeloSemanticoToDecoratedMethod, IMethodSymbol toDecoratedMethodSymbol, IDecoratorChecker checker)
        {
            this.toDecoratedMethodSymbol = toDecoratedMethodSymbol;
            this.modeloSemanticoToDecoratedMethod = modeloSemanticoToDecoratedMethod;
            this.toDecoratedMethod = toDecoratedMethod;
            this.checker = checker;
            instanceName = "instance";

        }


        #region Visitor Functions
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            SyntaxToken name = SyntaxFactory.Identifier(SyntaxTools.GetFuncPrivateName(node.Identifier.Text));
            var attrList = GetNoDecoratorAttrs();
          
            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;

            if (node.Identifier.Text == toDecoratedMethod.Identifier.Text)   //si es el methodprivate a generar (puede que no lo sea pues se pueden declarar funciones anidadas)
            {
                if (toDecoratedMethodSymbol.ReturnsVoid)
                {
                    node = node.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)).WithTriviaFrom(node.ReturnType));
                    node = node.WithBody(node.Body.AddStatements(SyntaxFactory.ReturnStatement(SyntaxFactory.Token(node.Body.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia("    ")), SyntaxKind.ReturnKeyword, SyntaxFactory.ParseTrailingTrivia(" ")), SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression), SyntaxFactory.Token(SyntaxKind.SemicolonToken)).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia("\n"))));
                }


                node = node.WithIdentifier(name).WithAttributeLists(attrList).WithModifiers(SyntaxTools.AddingPrivateModifier(toDecoratedMethod.Modifiers));

                return (toDecoratedMethodSymbol.IsStatic) ? node : node.WithParameterList(MakeNewParametersList()).AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
            }
            return node;

        }

        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            node =  base.VisitReturnStatement(node) as ReturnStatementSyntax;
            
            if(node.Expression == null && toDecoratedMethodSymbol.ReturnsVoid && !node.Ancestors().OfType<LambdaExpressionSyntax>().Any() && node.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.Text == toDecoratedMethod.Identifier.Text)
            {
                return SyntaxFactory.ReturnStatement(SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")), SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression), SyntaxFactory.Token(SyntaxKind.SemicolonToken)).WithTriviaFrom(node);
            }
            return node;

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

            if (!(node.Parent is MemberAccessExpressionSyntax) && !toDecoratedMethodSymbol.IsStatic && identifierSymbol!=null)   //si no forma parte de una expresion de la forma a.method(), entonces tengo que poner la instancia del objeto
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
            var atributos = SyntaxFactory.SeparatedList<AttributeSyntax>(this.toDecoratedMethod.DescendantNodes().OfType<AttributeSyntax>().Where(n => checker.IsDecorateAttr(n,modeloSemanticoToDecoratedMethod)));
            AttributeListSyntax listaAtr = SyntaxFactory.AttributeList(atributos);
            List<AttributeListSyntax> lista = new List<AttributeListSyntax>();
            lista.Add(listaAtr);
            SyntaxList<AttributeListSyntax> aux = SyntaxFactory.List<AttributeListSyntax>();

            if (lista.Count > 0)
                aux.AddRange(lista);

            return aux;
        }


       
        //agregando parametro classContainer instance
        private ParameterListSyntax MakeNewParametersList()
        {
            var separatedList  = SyntaxFactory.SeparatedList<ParameterSyntax>().Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(this.instanceName)).WithType(SyntaxFactory.IdentifierName(toDecoratedMethodSymbol.ReceiverType.Name).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "))));
            separatedList = separatedList.AddRange(this.toDecoratedMethod.ParameterList.Parameters);
            return SyntaxFactory.ParameterList(separatedList).WithTriviaFrom(toDecoratedMethod.ParameterList);
        }

       

        #endregion
    }
}
