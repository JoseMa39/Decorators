using Decorators.Utilities;
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
    class MemoizeDeco: PredefinedDecorator
    {
        public override string Identifier { get => "Memoize"; }

        //cambiar despues
        public override string CurrentNamespaces { get => "System"; }

        public override IEnumerable<UsingDirectiveSyntax> GetUsingNamespaces()
        {
            return new List<UsingDirectiveSyntax>();
        }


        public override MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            var methodToDecorated = toDecorated as MethodDeclarationSyntax;

            var ans = SyntaxFactory.MethodDeclaration(
                        GetFuncSyntax(methodToDecorated),
                            SyntaxFactory.Identifier(SyntaxTools.FormatterStringNames(this.Identifier, methodToDecorated.Identifier.Text)))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            new[]{
                                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword)}))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                                SyntaxFactory.Parameter(
                                    SyntaxFactory.Identifier("F"))
                                .WithType(
                                    GetFuncSyntax(methodToDecorated)))))
                    .WithBody(
                        GetBody(methodToDecorated))
                    .NormalizeWhitespace();

            Console.WriteLine(ans.ToFullString());

            return ans;
        }

        private BlockSyntax GetBody(MethodDeclarationSyntax methodToDecorated)
        {
            var ans = SyntaxFactory.Block(
                    SyntaxFactory.LocalDeclarationStatement(
                        GetMemoryVariableSyntax(methodToDecorated)),
            SyntaxFactory.ReturnStatement(
                GetReturnedLambda(methodToDecorated)));

            return ans;
        }

        private ParenthesizedLambdaExpressionSyntax GetReturnedLambda(MethodDeclarationSyntax methodToDecorated)
        {
            var ans = SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.Block(
                                SyntaxFactory.LocalDeclarationStatement(
                                    SyntaxFactory.VariableDeclaration(
                                        GetKeyVariable(methodToDecorated))
                                    .WithVariables(
                                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                            SyntaxFactory.VariableDeclarator(
                                                SyntaxFactory.Identifier("key"))
                                            .WithInitializer(
                                                GetKeyVarAssigment(methodToDecorated))))),
                                GetIfStatement(methodToDecorated),
                                GetFirstAssigment(methodToDecorated),
                                GetSecondAssigment(methodToDecorated),
                                SyntaxFactory.ReturnStatement(
                                    SyntaxFactory.IdentifierName("ans"))))
                                .WithParameterList(
                                    GetLambdaParameters(methodToDecorated));

            return ans;
        }

        private EqualsValueClauseSyntax GetKeyVarAssigment(MethodDeclarationSyntax methodToDecorated)
        {
            var paramsCount = methodToDecorated.ParameterList.Parameters.Count;

            if (paramsCount == 1)
                return SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.IdentifierName("arg1"));

            var argumentList = GetArgumentsSyntax(methodToDecorated);
            var ans = SyntaxFactory.EqualsValueClause(
                        SyntaxFactory.TupleExpression(
                            argumentList));
            return ans;
        }

        private VariableDeclarationSyntax GetMemoryVariableSyntax(MethodDeclarationSyntax methodToDecorated)
        {
            return SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier("memory"))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.QualifiedName(
                                            SyntaxFactory.QualifiedName(
                                                SyntaxFactory.QualifiedName(
                                                    SyntaxFactory.IdentifierName("System"),
                                                    SyntaxFactory.IdentifierName("Collections")),
                                                SyntaxFactory.IdentifierName("Generic")),
                                            SyntaxFactory.GenericName(
                                                SyntaxFactory.Identifier("Dictionary"))
                                            .WithTypeArgumentList(
                                                SyntaxFactory.TypeArgumentList(
                                                    GetDicTypes(methodToDecorated)))))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList())))));
        }

        private SeparatedSyntaxList<TypeSyntax> GetDicTypes(MethodDeclarationSyntax methodToDecorated)
        {
            return SyntaxFactory.SeparatedList<TypeSyntax>(
                    new SyntaxNodeOrToken[]{
                        GetDicKeyDeclaration(methodToDecorated),
                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                        methodToDecorated.ReturnType });
        }

        private TypeSyntax GetDicKeyDeclaration(MethodDeclarationSyntax methodToDecorated)
        {
            var paramsCount = methodToDecorated.ParameterList.Parameters.Count;

            if (paramsCount > 1)
                return GetDicKeyDeclarationTuple(methodToDecorated);

            return methodToDecorated.ParameterList.Parameters.Single().Type;
        }

        private TypeSyntax GetKeyVariable(MethodDeclarationSyntax methodToDecorated)
        {
            var paramsCount = methodToDecorated.ParameterList.Parameters.Count;

            if (paramsCount > 1)
                return GetKeyVariableTuple(methodToDecorated);

            return methodToDecorated.ParameterList.Parameters.Single().Type;
        }

        private TupleTypeSyntax GetDicKeyDeclarationTuple(MethodDeclarationSyntax methodToDecorated)
        {
            return SyntaxFactory.TupleType(
                    GetParametersAsTuple(methodToDecorated));
        }

        private TupleTypeSyntax GetKeyVariableTuple(MethodDeclarationSyntax methodToDecorated)
        {
            return SyntaxFactory.TupleType(
                    GetParametersAsTuple(methodToDecorated));
        }

        private IfStatementSyntax GetIfStatement(MethodDeclarationSyntax methodToDecorated)
        {
            var ans = SyntaxFactory.IfStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("memory"),
                                SyntaxFactory.IdentifierName("TryGetValue")))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("key")),
                                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.DeclarationExpression(
                                                methodToDecorated.ReturnType,
                                                SyntaxFactory.SingleVariableDesignation(
                                                    SyntaxFactory.Identifier("ans"))))
                                        .WithRefKindKeyword(
                                            SyntaxFactory.Token(SyntaxKind.OutKeyword))}))),
                        SyntaxFactory.ReturnStatement(
                            SyntaxFactory.IdentifierName("ans")));

            return ans;
        }

        private ExpressionStatementSyntax GetFirstAssigment(MethodDeclarationSyntax methodToDecorated)
        {
            var argumentList = GetArgumentsSyntax(methodToDecorated);

            var ans = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName("ans"),
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.IdentifierName("F"))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    argumentList))));

            return ans;
        }

        private ExpressionStatementSyntax GetSecondAssigment(MethodDeclarationSyntax methodToDecorated)
        {
            var ans = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.ElementAccessExpression(
                                SyntaxFactory.IdentifierName("memory"))
                            .WithArgumentList(
                                SyntaxFactory.BracketedArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("key"))))),
                            SyntaxFactory.IdentifierName("ans")));

            return ans;
        }
    }
}
