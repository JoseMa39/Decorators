using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Decorators.CodeInjections
{
    class DecoratorRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel modeloSemantico;
        private MethodDeclarationSyntax decoratorMethod;
        private MethodDeclarationSyntax toDecorated;
        private string currentArgsName, paramsName, argsLength, dynamicParam, dynamicResult;
        private IdentifierNameSyntax lca;

        public DecoratorRewriter(SemanticModel modeloSemantico, MethodDeclarationSyntax decoratorMethod, MethodDeclarationSyntax toDecorated, string paramsName = "__param", string argsLength = "__argsLength", string dynamicParam = "DynamicParam", string dynamicResult = "DynamicResult")
        {
            this.modeloSemantico = modeloSemantico;
            this.decoratorMethod = decoratorMethod;
            this.toDecorated = toDecorated;
            this.paramsName = paramsName;
            this.argsLength = argsLength;
            this.dynamicParam = dynamicParam;
            this.dynamicResult = dynamicResult;
            this.lca = LCA();
        }

        #region Visitors Methods
        
        
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            node = base.VisitIdentifierName(node) as IdentifierNameSyntax;
            if (node.Identifier.Text == dynamicResult)
            {
                return toDecorated.ReturnType.WithTriviaFrom(node);
            }
            if (node.Identifier.Text == dynamicParam)
            {
                return this.lca.WithTriviaFrom(node);
            }
            return node;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            bool isWrapper = IsWrapperDecorator(node);

            //guardo current args name por si hay mas de una funcion anidada
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
                currentArgsName = node.ParameterList.Parameters[0].Identifier.Text;

            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;
            //si el metodo no es el decorador  (revisar,  me gustaria usar un atributo decorator)

            if (node.Identifier.Text != decoratorMethod.Identifier.Text)
            {
                if(isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
                {
                    var parameters = SyntaxFactory.ParameterList();
                    //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                    var paramArray = MakingParameters();
                    //actualizando tipo de retorno
                    node = node.WithParameterList(parameters.AddParameters(paramArray)).WithReturnType(toDecorated.ReturnType);

                    node = node.WithBody(WorkingWithWrapperFunction(node.Body) as BlockSyntax);
                }
                currentArgsName = temp;
                return node;
            }

            //func<int,int,int>  y cambiando el nombre a __DecoratorToDecorate
            var fun = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), MakingFuncDelegateTypeArguments());
            node  = node.WithIdentifier(SyntaxFactory.Identifier("__"+ decoratorMethod.Identifier.Text + toDecorated.Identifier.Text)).WithReturnType(fun);

            //generando el parametro para el decorador
            ParameterSyntax param = SyntaxFactory.Parameter(node.ParameterList.Parameters[0].Identifier).WithType(fun);

            node = node.WithParameterList(SyntaxFactory.ParameterList().AddParameters(param));

            return node;
        }

        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            SimpleLambdaExpressionSyntax a;
            bool isWrapper = IsWrapperDecorator(node);
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
                currentArgsName = node.ParameterList.Parameters[0].Identifier.Text;

            node = base.VisitParenthesizedLambdaExpression(node) as ParenthesizedLambdaExpressionSyntax;

            if (isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
            {
                var parameters = SyntaxFactory.ParameterList();

                //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                var paramArray = MakingParameters();
                //actualizando tipo de retorno
                node = node.WithParameterList(parameters.AddParameters(paramArray));
                node = node.WithBody(WorkingWithWrapperFunction(node.Body));
            }
            currentArgsName = temp;
            return node;
        }

        public override SyntaxNode VisitGenericName(GenericNameSyntax node)
        {
            bool iswrapper = IsWrapperDecorator(node);
            node = base.VisitGenericName(node) as GenericNameSyntax;
            if (iswrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
            {
                node = node.WithTypeArgumentList(MakingFuncDelegateTypeArguments());
            }
            return node;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            bool isToDecorated = IsToDecoratedFunction(node);
            node = base.VisitInvocationExpression(node) as InvocationExpressionSyntax;
            //falta considerar cuando la funcion se guarda desde un delegate
            if (IsToDecoratedFunction(node))
            {
                if(node.ArgumentList.Arguments[0].Expression.ToFullString() == currentArgsName)
                    node  = node.WithArgumentList(MakingInvocationArguments());
                else
                    node = node.WithArgumentList(MakingInvocationArguments(node.ArgumentList.Arguments[0]));
            }
            return node;

        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax && (node.Expression as IdentifierNameSyntax).Identifier.Text == currentArgsName  && (node.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax))
                return SyntaxFactory.IdentifierName(paramsName + node.ArgumentList.Arguments[0].ToFullString());

            node =  base.VisitElementAccessExpression(node) as ElementAccessExpressionSyntax;
            return node;
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            node =  base.VisitMemberAccessExpression(node) as MemberAccessExpressionSyntax;
            
            //sustituyendo args.length
            if (node.Expression is IdentifierNameSyntax && (node.Expression as IdentifierNameSyntax).Identifier.Text == currentArgsName && (node.Name.Identifier.Text == "Length" || node.Name.Identifier.Text == "LongLength"))
                return SyntaxFactory.IdentifierName(argsLength);

            return node;
        }

        public override SyntaxNode VisitArrayType(ArrayTypeSyntax node)
        {
            var type = modeloSemantico.GetTypeInfo(node).Type as IArrayTypeSymbol;

            node =  base.VisitArrayType(node) as ArrayTypeSyntax;

            if (type.ElementType.Name == this.dynamicParam )
                node = node.WithElementType(this.lca);
            return node;
        }

        public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
        {

            var type = modeloSemantico.GetTypeInfo(node.Type).Type as INamedTypeSymbol;
            node =  base.VisitVariableDeclaration(node) as VariableDeclarationSyntax;

            if (type!= null && type.Name == dynamicParam)
            {
                //multiple inicializacion, pongo el lca porque no todos tienen el mismo tipo, igual si no esta inicializado
                if (node.Variables.Count > 1 || node.Variables[0].Initializer == null)
                    return node.WithType(this.lca.WithLeadingTrivia(node.Type.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
                
                //si tiene inicializacion entonces puedo ponerle var, como var t = new DynamicParam(10);
                return node.WithType(SyntaxFactory.IdentifierName("var").WithLeadingTrivia(node.Type.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
            }
            return node;

        }

        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var type = modeloSemantico.GetTypeInfo(node).Type as INamedTypeSymbol;
            node =  base.VisitObjectCreationExpression(node) as ObjectCreationExpressionSyntax;

            if (type.Name == dynamicParam || type.Name == dynamicResult)
                return node.ArgumentList.Arguments[0].Expression;

            return node;
        }
        public override SyntaxNode VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
        {
            return base.VisitAnonymousObjectCreationExpression(node);
        }

        #endregion

        #region IsWrapperDecorators
        //revisa si es un wrapper dentro del decorador
        private bool IsWrapperDecorator(ParenthesizedLambdaExpressionSyntax node, string decoratorParam = "DynamicParam", string decoratorReturn = "DynamicResult")
        {
            var symbol = modeloSemantico.GetSymbolInfo(node).Symbol as IMethodSymbol;

            if (symbol.ReturnType.Name!= decoratorReturn)
                return false;

            if (node.ParameterList.Parameters.Count != 1)
                return false;

            ////falta revisar los parametros pero me esta dando problemas

            //var param = node.ChildNodes().OfType<ParameterListSyntax>().First().ChildNodes().OfType<ParameterSyntax>().First();
            //var paramSymbol = modeloSemantico.GetTypeInfo(param).Type;

            // Console.WriteLine(paramSymbol.Type.Name);
            //if (paramSymbol.Name != decoratorParam + "[]")
            //    return false;

            return true;
        }

        private bool IsWrapperDecorator(GenericNameSyntax node, string decoratorParam = "DynamicParam", string decoratorReturn = "DynamicResult")
        {
            if (node.Identifier.Text !="Func" || node.Arity!=2)
                return false;

            if (((node.TypeArgumentList.Arguments[0]).ToFullString() != decoratorParam + "[]" ) || (node.TypeArgumentList.Arguments[1].ToFullString() != decoratorReturn))
                return false;

            return true;
        }

        private bool IsWrapperDecorator(MethodDeclarationSyntax node, string decoratorParam = "DynamicParam" , string decoratorReturn = "DynamicResult")
        {
            //buscar si existe una mejor forma trabajando con el modelo semantico
            var returnType = modeloSemantico.GetTypeInfo(node.ReturnType).Type;

            if (returnType.Name != decoratorParam)
                return false;

            var a = node.ParameterList.Parameters[0];
            var paramType = modeloSemantico.GetTypeInfo(node.ParameterList.Parameters[0].Type).Type;
            if (paramType.ToDisplayString() != decoratorParam + "[]")
                return false;

            return true;
            
        }

        private bool IsToDecoratedFunction(InvocationExpressionSyntax node)
        {
            if (node.Expression.ToFullString() == decoratorMethod.ParameterList.Parameters[0].Identifier.Text)
                return true;

            return false;
        }
        #endregion

        #region Utiles
        private ParameterSyntax[] MakingParameters ()
        {
            ParameterSyntax[] paramArray = new ParameterSyntax[toDecorated.ParameterList.Parameters.Count];
            for (int i = 0; i < paramArray.Length; i++)
            {
                paramArray[i] = toDecorated.ParameterList.Parameters[i].WithIdentifier(SyntaxFactory.Identifier("__param" + i));
            }
            return paramArray;
        }

        // calcula el lowest common ancester(LCA) de todos los parametros para en caso de que se haga un array
        private IdentifierNameSyntax LCA()
        {
            return SyntaxFactory.IdentifierName("object");
        }

        private TypeArgumentListSyntax MakingFuncDelegateTypeArguments()
        {
            //generando el nuevo tipo de retorno de la funcion
            var argumentList = SyntaxFactory.TypeArgumentList();
            foreach (var item in toDecorated.ParameterList.Parameters)
            {
                argumentList = argumentList.AddArguments(item.Type);
            }
            return argumentList.AddArguments(toDecorated.ReturnType);
        }

        //se utiliza para crear la lista de argumentos par llamar a la funcion decorada
        private ArgumentListSyntax MakingInvocationArguments()
        {
            //generando el nuevo tipo de retorno de la funcion
            var argumentList = SyntaxFactory.ArgumentList();
            for (int i = 0; i < toDecorated.ParameterList.Parameters.Count; i++)
            {
                argumentList = argumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("__param" + i)));
            }
            return argumentList;
        }

        private ArgumentListSyntax MakingInvocationArguments(ArgumentSyntax arrayArgs)
        {
            //generando la lista de argumentos con los elementos del array
            var argumentList = SyntaxFactory.ArgumentList();

            for (int i = 0; i < toDecorated.ParameterList.Parameters.Count; i++)
            {
                //casteando al tipo especifico del parametro
                var castExpression = SyntaxFactory.CastExpression(toDecorated.ParameterList.Parameters[i].Type.WithoutTrailingTrivia(), SyntaxFactory.ElementAccessExpression
                    (arrayArgs.Expression, SyntaxFactory.BracketedArgumentList(
                    (SyntaxFactory.SeparatedList<ArgumentSyntax>().Add
                    (SyntaxFactory.Argument(SyntaxFactory.LiteralExpression
                    (SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i))))))));

                argumentList = argumentList.AddArguments(SyntaxFactory.Argument(castExpression));
            }
            return argumentList;
        }

        // se encarga de modificar el cuerpo de los wrappers internos (crear array args si es necesario, crear __lengthParams)
        private CSharpSyntaxNode WorkingWithWrapperFunction(CSharpSyntaxNode body)
        {
            if (body is BlockSyntax)
            {
                var newBody = body as BlockSyntax;

                if (newBody.DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => n.Identifier.Text == currentArgsName).Any())
                    newBody = newBody.WithStatements(newBody.Statements.Insert(0, CreateArgsArrayInstruction(newBody.Statements[0].GetLeadingTrivia(), newBody.Statements[0].GetTrailingTrivia())));

                body = newBody.WithStatements(newBody.Statements.Insert(0, ArgsLengthInstruction(newBody.Statements[0].GetLeadingTrivia(), newBody.Statements[0].GetTrailingTrivia(), "int")));
            }
            return body;
        }

        //construye la instruccion lca[] args = {__param0, __param1}
        private StatementSyntax CreateArgsArrayInstruction(SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        {

            var initializerExp = SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression);
            for (int i = 0; i < toDecorated.ParameterList.Parameters.Count; i++)
            {
                initializerExp = initializerExp.AddExpressions(SyntaxFactory.IdentifierName(paramsName + i));
            }

            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ArrayType(
                        this.lca,SyntaxFactory.List<ArrayRankSpecifierSyntax>().Add(SyntaxFactory.ArrayRankSpecifier())),
                            SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>().Add(SyntaxFactory.VariableDeclarator(currentArgsName).WithInitializer(
                                SyntaxFactory.EqualsValueClause(initializerExp))))).WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
        }

        //construye la instruccion int __argsLength = ??;
        private StatementSyntax ArgsLengthInstruction( SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia, string type = "int")
        {
            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(type).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")),SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>().Add(SyntaxFactory.VariableDeclarator(argsLength).WithInitializer(
                        SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(toDecorated.ParameterList.Parameters.Count))))))).WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
        }
        #endregion
    }
}
