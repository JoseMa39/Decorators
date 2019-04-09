using Decorators.CodeInjections.ClassesToCreate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.CodeInjections
{
    internal class SpecificDecoratorRewriter : CSharpSyntaxRewriter
    {
        protected  SemanticModel modeloSemanticoDecorator;
        protected MethodDeclarationSyntax decoratorMethod;
        protected MethodDeclarationSyntax toDecorated;
        protected string currentArgsName, paramsName, dynamicParam, dynamicResult, paramClassGenerated, toTupleParamsType, toTupleMethodName;
        protected int cantArgumentsToDecorated;


        protected IMethodSymbol toDecoratedMethodSymbol;

        //para cuando no es estatico
        protected TypeSyntax[] specificDecoratorTypeParams;

        public SpecificDecoratorRewriter(SemanticModel modeloSemanticoDecorator, IMethodSymbol toDecoratedMethodSymbol, MethodDeclarationSyntax toDecorated, string paramsName = "__param", string paramClassGenerated = "ParamsGenerics")
        {
            this.modeloSemanticoDecorator = modeloSemanticoDecorator;
            this.toDecorated = toDecorated;

            this.paramsName = paramsName;
            this.paramClassGenerated = paramClassGenerated;

            this.dynamicParam = "DecoratorsDLL.DecoratorsClasses.DynamicTypes.DynamicParamsCollection";
            this.dynamicResult = "DecoratorsDLL.DecoratorsClasses.DynamicTypes.DynamicResult";
            this.toTupleParamsType = "DecoratorsDLL.DecoratorsClasses.DynamicTypes.DynamicParamsCollection.ToTupleParamsType";
            this.toTupleMethodName = "ToTuple()";

            //para trabajar cuando no es estatico
            this.toDecoratedMethodSymbol = toDecoratedMethodSymbol;
            //si es no es estatico necesito un parametro mas;
            cantArgumentsToDecorated = (this.toDecoratedMethodSymbol.IsStatic) ? toDecorated.ParameterList.Parameters.Count : toDecorated.ParameterList.Parameters.Count + 1;


            specificDecoratorTypeParams = new TypeSyntax[this.cantArgumentsToDecorated];
            FillDecoratorTypeParams();
        }


        #region Visitors Methods

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

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var methodSymbol = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol as IMethodSymbol;
            bool isToDecorated = IsToDecoratedFunction(node);
            node = base.VisitInvocationExpression(node) as InvocationExpressionSyntax;

            //falta considerar cuando la funcion se guarda desde un delegate
            if (IsToDecoratedFunction(node))
            {
                if (node.ArgumentList.Arguments[0].Expression.ToFullString() == currentArgsName) //toChange marca el nodo padre q se tendria q modificar
                { //toChangeId marca el nodo particular a ser cambiado (ver si luego se puede quitar)
                    node = node.WithAdditionalAnnotations(new SyntaxAnnotation("toChange", "invocationExp"));  //para si no hace falta crear la clase args poner luego directamente los parametros
                    node = node.WithArgumentList(MakingInvocationArguments(node.ArgumentList.Arguments[0].WithExpression(node.ArgumentList.Arguments[0].Expression.WithAdditionalAnnotations(new SyntaxAnnotation("toChangeId", "identifier")))));
                }
                else node = node.WithArgumentList(MakingInvocationArguments(node.ArgumentList.Arguments[0]));
            }
            //args.ToTuple()
            else if (methodSymbol!= null && methodSymbol.OriginalDefinition.ToDisplayString() == this.dynamicParam + "." + this.toTupleMethodName)
            {
                var memberAccessExp = node.Expression as MemberAccessExpressionSyntax;
                if(memberAccessExp.Expression.ToFullString() == currentArgsName)
                {
                    memberAccessExp = memberAccessExp.WithExpression(memberAccessExp.Expression.WithAdditionalAnnotations(new SyntaxAnnotation("toChangeId", "identifier")));
                    node = node.WithExpression(memberAccessExp).WithAdditionalAnnotations(new SyntaxAnnotation("toChangeToTupleMember", "ToTuple"));
                }
            }
            return node;
        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            //obteniendo el tipo
            var type = modeloSemanticoDecorator.GetTypeInfo(node.Expression).Type as ITypeSymbol;
            node = base.VisitElementAccessExpression(node) as ElementAccessExpressionSyntax;

            if (type.OriginalDefinition.ToDisplayString() == dynamicParam && (node.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax))
            {
                var newNode = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,node.Expression,SyntaxFactory.IdentifierName($"Item{1 + int.Parse(node.ArgumentList.Arguments[0].ToFullString())}"));
                if (node.Expression is IdentifierNameSyntax && (node.Expression as IdentifierNameSyntax).Identifier.Text == currentArgsName)
                    newNode = newNode.WithExpression(newNode.Expression.WithAdditionalAnnotations(new SyntaxAnnotation("toChangeId", "identifier"))).WithAdditionalAnnotations(new SyntaxAnnotation("toChange", node.ArgumentList.Arguments[0].ToFullString()));  //marcando para si despues tengo que modificar (guardo en la annotgation el no del parametro)

                return newNode;
            }
            return node;
        }

        //int a = ...;
        //public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
        //{

        //    var type = modeloSemanticoDecorator.GetTypeInfo(node.Type).Type as INamedTypeSymbol;
        //    node = base.VisitVariableDeclaration(node) as VariableDeclarationSyntax;

        //    if (type != null && type.OriginalDefinition.ToDisplayString() == dynamicParam)  //ver xq es necesario esto si en el visitor del identifierSyntax se soluciona
        //    {
        //        //multiple inicializacion, o si no esta inicializado
        //        if (node.Variables.Count > 1 || node.Variables[0].Initializer == null)
        //            return node.WithType(MakingGenericNameWithParams().WithLeadingTrivia(node.Type.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));

        //        //si tiene inicializacion entonces puedo ponerle var
        //        return node.WithType(SyntaxFactory.IdentifierName("var").WithLeadingTrivia(node.Type.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
        //    }


        //    return node;

        //}

        //new Class()


        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var type = modeloSemanticoDecorator.GetTypeInfo(node).Type as ITypeSymbol;
            node = base.VisitObjectCreationExpression(node) as ObjectCreationExpressionSyntax;

            if (type.OriginalDefinition.ToDisplayString() == dynamicResult)
                return node.ArgumentList.Arguments[0].Expression;

            return node;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var type = modeloSemanticoDecorator.GetTypeInfo(node).Type as ITypeSymbol;
            node = base.VisitIdentifierName(node) as IdentifierNameSyntax;

            if (type==null)
                return node;

            string completeType = type.OriginalDefinition.ToDisplayString();

            if (completeType == dynamicResult && node.Identifier.Text == dynamicResult.Split('.').Last() )
            {
                return toDecorated.ReturnType.WithTriviaFrom(node);
            }
            if (completeType == dynamicParam && node.Identifier.Text == dynamicParam.Split('.').Last())
            {
                return MakingGenericNameWithParams().WithTriviaFrom(node);
            }
            if (completeType == this.toTupleParamsType && node.Identifier.Text == toTupleParamsType.Split('.').Last())
            {
                return MakingTupleType().WithTriviaFrom(node);
            }
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

        //asegurandome cambiar los tipos dynamic... del decorador
        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
        {
            var type = modeloSemanticoDecorator.GetTypeInfo(node).Type as ITypeSymbol;
            string completeType = type.OriginalDefinition.ToDisplayString();
            if (completeType == this.toTupleParamsType)
            {
               return MakingTupleType();
            }
            if (completeType == dynamicResult)
            {
                return toDecorated.ReturnType.WithTriviaFrom(node);
            }
            if (completeType == dynamicParam)
            {
                return MakingGenericNameWithParams();
            }
            node = base.VisitQualifiedName(node) as QualifiedNameSyntax;

            return node;


        }



        #endregion

        #region IsWrapperDecorators
        //revisa si es un wrapper dentro del decorador
        protected bool IsWrapperDecorator(ParenthesizedLambdaExpressionSyntax node)
        {
            var symbol = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (symbol.ReturnType.OriginalDefinition.ToDisplayString() != dynamicResult)
                return false;

            if (node.ParameterList.Parameters.Count != 1)
                return false;

            ////falta revisar los parametros pero me esta dando problemas

            //var param = node.ChildNodes().OfType<ParameterListSyntax>().First().ChildNodes().OfType<ParameterSyntax>().First();
            //var paramSymbol = modeloSemantico.GetTypeInfo(param).Type;

            // Console.WriteLine(paramSymbol.Type.Name);
            //if (paramSymbol.Name != decoratorParam )
            //    return false;

            return true;
        }

        protected bool IsWrapperDecorator(GenericNameSyntax node)
        {
            if (node.Identifier.Text != "Func" || node.Arity != 2)
                return false;

            var typeParams= modeloSemanticoDecorator.GetTypeInfo(node.TypeArgumentList.Arguments[0]).Type as ITypeSymbol;
            var type = modeloSemanticoDecorator.GetTypeInfo(node.TypeArgumentList.Arguments[1]).Type as ITypeSymbol;
            if ((typeParams.OriginalDefinition.ToDisplayString() != dynamicParam) || (type.OriginalDefinition.ToDisplayString() != dynamicResult))
                return false;

            return true;
        }

        protected bool IsWrapperDecorator(MethodDeclarationSyntax node)
        {
            //buscar si existe una mejor forma trabajando con el modelo semantico
            var returnType = modeloSemanticoDecorator.GetTypeInfo(node.ReturnType).Type;

            if (returnType.OriginalDefinition.ToDisplayString() != dynamicResult)
                return false;

            var a = node.ParameterList.Parameters[0];
            var paramType = modeloSemanticoDecorator.GetTypeInfo(node.ParameterList.Parameters[0].Type).Type;
            if (paramType.OriginalDefinition.ToDisplayString() != dynamicParam)
                return false;

            return true;

        }


        //falta considerar cuando la funcion se guardo en otro delegate
        protected bool IsToDecoratedFunction(InvocationExpressionSyntax node)
        {
            if (node.Expression.ToFullString() == decoratorMethod.ParameterList.Parameters[0].Identifier.Text)
                return true;

            return false;
        }
        #endregion

        #region Utiles
        protected ParameterSyntax[] MakingParameters()
        {
            ParameterSyntax[] paramArray = new ParameterSyntax[this.cantArgumentsToDecorated];
            for (int i = 0; i < paramArray.Length; i++)
            {
                paramArray[i] = SyntaxFactory.Parameter(SyntaxFactory.Identifier("__param" + i)).WithType(this.specificDecoratorTypeParams[i]);
                //var a = toDecorated.ParameterList.Parameters[i].WithIdentifier(SyntaxFactory.Identifier("__param" + i));
            }
            return paramArray;
        }

        //se utiliza para crear la lista de argumentos par llamar a la funcion decorada
        protected ArgumentListSyntax MakingInvocationArguments()
        {
            //generando el nuevo tipo de retorno de la funcion
            var argumentList = SyntaxFactory.ArgumentList();
            for (int i = 0; i < this.cantArgumentsToDecorated; i++)
            {
                argumentList = argumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("__param" + i)));
            }
            return argumentList;
        }

        protected ArgumentListSyntax MakingInvocationArguments(ArgumentSyntax arrayArgs)
        {
            //generando la lista de argumentos con los elementos de la clase params
            var argumentList = SyntaxFactory.ArgumentList();

            for (int i = 0; i < this.cantArgumentsToDecorated; i++)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, arrayArgs.Expression, SyntaxFactory.IdentifierName($"Item{1 + i}"));

                //casteando al tipo especifico del parametro
                argumentList = argumentList.AddArguments(SyntaxFactory.Argument(memberAccess));
            }
            return argumentList;
        }

        protected TypeArgumentListSyntax MakingFuncDelegateTypeArguments()
        {
            //generando el nuevo tipo de retorno de la funcion
            var argumentList = SyntaxFactory.TypeArgumentList();

            foreach (var item in this.specificDecoratorTypeParams)
            {
                argumentList = argumentList.AddArguments(item);
            }
            return argumentList.AddArguments(toDecorated.ReturnType);
        }

        // se encarga de modificar el cuerpo de los wrappers internos (crear class args si es necesario)
        protected CSharpSyntaxNode WorkingWithWrapperFunction(CSharpSyntaxNode body)
        {
            if (body is BlockSyntax)
            {
                var newBody = body as BlockSyntax;

                if (newBody.DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => n.Identifier.Text == currentArgsName && !n.GetAnnotations("toChangeId").Any()).Any())   //chequea si hace falta instanciar la clase de los parametros
                    newBody = newBody.WithStatements(newBody.Statements.Insert(0, CreateArgsClassInstruction(newBody.Statements[0].GetLeadingTrivia(), newBody.Statements[0].GetTrailingTrivia())));
                else
                {
                    //en caso de que no hizo falta crea la clase params, entonces elimino su existencia (sustituyo por los parametros reales)
                    var invocationToChange = newBody.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n => n.GetAnnotations("toChange").Any());
                    newBody = newBody.ReplaceNodes(invocationToChange, (n1, n2) => n1.WithArgumentList(MakingInvocationArguments()));

                    var arrayAccessExp = newBody.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(n => n.GetAnnotations("toChange").Any());
                    newBody = newBody.ReplaceNodes(arrayAccessExp, (n1, n2) => SyntaxFactory.IdentifierName(paramsName + n1.GetAnnotations("toChange").First().Data));
                   
                    var memberAccessExpsIEnumerable = newBody.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n => n.GetAnnotations("toChangeToTupleMember").Any());
                    newBody = newBody.ReplaceNodes(memberAccessExpsIEnumerable, (n1, n2) => MakingTupleValues());
                    //Console.WriteLine(newBody.ToFullString());

                }
                return newBody;
            }
            return body;
        }

        //construye la instruccion ParamsGenerics2<int, int> a = new ParamsGenerics2<int, int>(__param0, _param1);
        protected StatementSyntax CreateArgsClassInstruction(SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        {
            //construyendo ParamsGenerics2<int, int>
            var genericName = MakingGenericNameWithParams();

            //construyendo = new ParamsGenerics2<int, int>(__param0, _param1);
            var initializerExp = SyntaxFactory.ObjectCreationExpression(genericName.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" ")));

            for (int i = 0; i < this.cantArgumentsToDecorated; i++)
            {
                initializerExp = initializerExp.AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(paramsName + i)));
            }

            // ParamsGenerics2<int, int> a = new ParamsGenerics2<int, int>(__param0, _param1);
            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(genericName,
                            SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>().Add(SyntaxFactory.VariableDeclarator(currentArgsName).WithInitializer(
                                SyntaxFactory.EqualsValueClause(initializerExp))))).WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
        }

        // Construye DynamicParamsCollection<int,int>
        protected GenericNameSyntax MakingGenericNameWithParams()
        {
            var argumentList = SyntaxFactory.TypeArgumentList();
            foreach (var item in this.specificDecoratorTypeParams)
            {
                argumentList = argumentList.AddArguments(item.WithoutTrivia());
            }
           return SyntaxFactory.GenericName(SyntaxFactory.Identifier(paramClassGenerated + cantArgumentsToDecorated), argumentList);
        }

        //(int,int)
        protected SyntaxNode MakingTupleType()
        {
            if (cantArgumentsToDecorated == 0)
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));
            if (cantArgumentsToDecorated == 1)
                return toDecorated.ParameterList.Parameters[0].Type;

            var newNode = SyntaxFactory.TupleType();
            for (int i = 0; i < cantArgumentsToDecorated; i++)
                newNode = newNode.AddElements(SyntaxFactory.TupleElement(this.specificDecoratorTypeParams[i]));

            return newNode;
        }

        //(__param0,__param1,__...)
        protected SyntaxNode MakingTupleValues()
        {
            if (cantArgumentsToDecorated == 0) //si no tiene parametros pongo null
                return SyntaxFactory.LiteralExpression(SyntaxKind.NullKeyword);
            if (cantArgumentsToDecorated == 1) //1 parametro el tipo directo
                return SyntaxFactory.IdentifierName(this.paramsName + "0");

            var newNode = SyntaxFactory.TupleExpression();  //mas de 1, tuplas
            for (int i = 0; i < cantArgumentsToDecorated; i++)
                newNode = newNode.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(this.paramsName + i.ToString())));

            return newNode;
        }

        protected void FillDecoratorTypeParams()
        {
            if (!toDecoratedMethodSymbol.IsStatic)
                this.specificDecoratorTypeParams[0] = SyntaxFactory.IdentifierName(toDecoratedMethodSymbol.ReceiverType.Name).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "));
            for (int i = (toDecoratedMethodSymbol.IsStatic) ? 0 : 1; i < this.specificDecoratorTypeParams.Length; i++)
            {
                this.specificDecoratorTypeParams[i] = toDecorated.ParameterList.Parameters[i - ((toDecoratedMethodSymbol.IsStatic) ? 0 : 1)].Type;
            }
        }

        #endregion
    }
}
