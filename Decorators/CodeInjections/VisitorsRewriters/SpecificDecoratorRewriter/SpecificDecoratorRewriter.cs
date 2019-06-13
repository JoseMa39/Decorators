using Decorators.CodeInjections.ClassesToCreate;
using Decorators.Utilities;
using DecoratorsDLL.DecoratorsClasses.DynamicTypes;
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
        protected string currentArgsName,paramsName, currentparamsName, dynamicParam, dynamicResult, dynamicResultValue, paramClassGenerated, toTupleParamsType, toTupleMethodName;
        protected int cantArgumentsToDecorated, deep;

        protected bool hasModifierParams;

        protected IMethodSymbol toDecoratedMethodSymbol;

        //para cuando no es estatico
        protected TypeSyntax[] specificDecoratorTypeParams;

        public SpecificDecoratorRewriter(SemanticModel modeloSemanticoDecorator, IMethodSymbol toDecoratedMethodSymbol, MethodDeclarationSyntax toDecorated, string paramsName = "__param", string paramClassGenerated = "ParamsGenerics")
        {
            this.modeloSemanticoDecorator = modeloSemanticoDecorator;
            this.toDecorated = toDecorated;

            this.paramsName = paramsName;
            this.paramClassGenerated = paramClassGenerated;
            this.deep = 0;  //para controlar la profundidad de las funciones y no repetir el nombre de los parametros
            this.currentparamsName = paramsName + deep;

            this.dynamicParam = typeof(DynamicParamsCollection).FullName;
            this.dynamicResult = typeof(DynamicResult).FullName;
            this.toTupleParamsType = typeof(DynamicParamsCollection.ToTupleParamsType).FullName.Replace('+','.'); // fullName al ser un tipo anidado me pone + en lugar de 0.   (DynamicParamsCollection+ToTupleParamsType)

            this.toTupleMethodName = "ToTuple()";
            this.dynamicResultValue = "Value";

            this.hasModifierParams = SyntaxTools.HasParamsModifiers(toDecoratedMethodSymbol);

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
            bool isWrapper = IsWrapperDecorator(node);
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
            {
                currentArgsName = node.ParameterList.Parameters[0].Identifier.Text;
                currentparamsName = this.paramsName + (++this.deep);
            }

            node = base.VisitParenthesizedLambdaExpression(node) as ParenthesizedLambdaExpressionSyntax;

            if (isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
            {
                var parameters = SyntaxFactory.ParameterList();
                
                //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                var paramArray = MakingParameters();
                //actualizando tipo de retorno
                node = node.WithParameterList(parameters.AddParameters(paramArray));
                node = node.WithBody(WorkingWithWrapperFunction(node.Body));
                currentparamsName = this.paramsName + (--this.deep);
            }
            currentArgsName = temp;
            return node;
        }

        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            bool isWrapper = IsWrapperDecorator(node);
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
            {
                currentArgsName = node.Parameter.Identifier.Text;
                currentparamsName = this.paramsName + (++this.deep);
            }

            node = base.VisitSimpleLambdaExpression(node) as SimpleLambdaExpressionSyntax;

            if (isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
            {
                var parameters = SyntaxFactory.ParameterList();

                //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                var paramArray = MakingParameters();
                //actualizando tipo de retorno
                var result =  SyntaxFactory.ParenthesizedLambdaExpression(parameters.AddParameters(paramArray), WorkingWithWrapperFunction(node.Body));
                currentparamsName = this.paramsName + (--this.deep);
                return result;
            }
            currentArgsName = temp;
            return node;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var methodSymbol = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol as IMethodSymbol;
            bool isToDecorated = IsToDecoratedFunction(node);
            node = base.VisitInvocationExpression(node) as InvocationExpressionSyntax;

            if (isToDecorated)
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
                return (!toDecoratedMethodSymbol.ReturnsVoid) ?toDecorated.ReturnType.WithTriviaFrom(node):SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)).WithTriviaFrom(node);
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
                if (hasModifierParams)
                    return SyntaxTools.MakingDelegateName(toDecorated,toDecoratedMethodSymbol).WithTriviaFrom(node);
                node = node.WithTypeArgumentList(MakingFuncDelegateTypeArguments());
            }
            return node;
        }

        //asegurandome cambiar los tipos dynamic... del decorador
        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
        {
            //var type = modeloSemanticoDecorator.GetTypeInfo(node).Type;

            var type = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol;

            string completeType = type.OriginalDefinition.ToDisplayString();
            if (completeType == this.toTupleParamsType)
            {
               return MakingTupleType();
            }
            if (completeType == dynamicResult)
            {
                return (!toDecoratedMethodSymbol.ReturnsVoid) ? toDecorated.ReturnType.WithTriviaFrom(node) : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)).WithTriviaFrom(node);

            }
            if (completeType == dynamicParam)
            {
                return MakingGenericNameWithParams();
            }
            node = base.VisitQualifiedName(node) as QualifiedNameSyntax;

            return node;


        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var type = modeloSemanticoDecorator.GetTypeInfo(node.Expression).Type as ITypeSymbol;

            node =  base.VisitMemberAccessExpression(node) as MemberAccessExpressionSyntax;
            if (type.OriginalDefinition.ToDisplayString() == dynamicResult && node.Name.Identifier.Text == this.dynamicResultValue)
                return node.Expression;

            return node;
        }



        #endregion

        #region IsWrapperDecorators
        //revisa si es un wrapper dentro del decorador  (verifica que el tipo de retorno sea dynamicresult y tenga un parametro de tipo dynamicParamsCollection)
        protected bool IsWrapperDecorator(ParenthesizedLambdaExpressionSyntax node)
        {
            var symbol = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (symbol.ReturnType.OriginalDefinition.ToDisplayString() != dynamicResult)
                return false;

            if (node.ParameterList.Parameters.Count != 1)
                return false;

            var parameter = node.ParameterList.Parameters[0];
            var paramSymbol = modeloSemanticoDecorator.GetDeclaredSymbol(parameter);

            if (paramSymbol.OriginalDefinition.ToDisplayString()!= this.dynamicParam)
                return false;

            return true;
        }

        protected bool IsWrapperDecorator(SimpleLambdaExpressionSyntax node)
        {
            var symbol = modeloSemanticoDecorator.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (symbol.ReturnType.OriginalDefinition.ToDisplayString() != dynamicResult)
                return false;

            var parameter = node.Parameter;
            var paramSymbol = modeloSemanticoDecorator.GetDeclaredSymbol(parameter);

            if (paramSymbol.OriginalDefinition.ToDisplayString() != this.dynamicParam)
                return false;

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

            //if ((typeParams.OriginalDefinition.ToDisplayString() != dynamicParam))
            //    return false;

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


        //considera tambien cuando la funcion se gUARDA EN otro delegate
        protected bool IsToDecoratedFunction(InvocationExpressionSyntax node)
        {
            var methodSymbol = modeloSemanticoDecorator.GetTypeInfo(node.Expression).Type as INamedTypeSymbol;

            if (methodSymbol != null && methodSymbol.ToDisplayString() == $"System.Func<{this.dynamicParam}, {this.dynamicResult}>")
                return true;

            return false;
        }
        #endregion

        #region Utiles

        //MyDelegate<...> para los atributos
        
        protected ParameterSyntax[] MakingParameters()
        {
            ParameterSyntax[] paramArray = new ParameterSyntax[this.cantArgumentsToDecorated];
            for (int i = 0; i < paramArray.Length; i++)
            {
                var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(this.currentparamsName + i)).WithType(this.specificDecoratorTypeParams[i]);
                if (toDecoratedMethodSymbol.IsStatic || i > 0)   // si no es estatico entonces los parametros estan corridos
                {
                    var mod = toDecorated.ParameterList.Parameters[i - ((toDecoratedMethodSymbol.IsStatic) ? 0 : 1)].Modifiers;    //anadiendo out o ref si están

                    if (mod.Count != 0)
                        param = param.AddModifiers(mod.First());
                }


                paramArray[i] = param;
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
                var arg = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(this.currentparamsName + i));  //anadiendo out o ref si están


                if (toDecoratedMethodSymbol.IsStatic || i > 0)
                {
                    var mod = toDecorated.ParameterList.Parameters[i - ((toDecoratedMethodSymbol.IsStatic) ? 0 : 1)].Modifiers;    //anadiendo out o ref si están

                    if (mod.Count != 0)
                        arg = arg.WithRefKindKeyword(mod.First());
                }

                argumentList = argumentList.AddArguments(arg);
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

                var arg = SyntaxFactory.Argument(memberAccess);

                if (toDecoratedMethodSymbol.IsStatic || i>0)
                {
                    var mod = toDecorated.ParameterList.Parameters[i - ((toDecoratedMethodSymbol.IsStatic) ? 0 : 1)].Modifiers;    //anadiendo out o ref si están

                    if (mod.Count != 0)
                        arg = arg.WithRefKindKeyword(mod.First());
                }

                argumentList = argumentList.AddArguments(arg);


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
            return (!toDecoratedMethodSymbol.ReturnsVoid) ? argumentList.AddArguments(toDecorated.ReturnType): argumentList.AddArguments(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)));
        }

        // se encarga de modificar el cuerpo de los wrappers internos (crear class args si es necesario)
        protected CSharpSyntaxNode WorkingWithWrapperFunction(CSharpSyntaxNode body)
        {
            BlockSyntax newBody;
            if (body is BlockSyntax)   
                newBody = body as BlockSyntax;
            else   //es necesario por si es un lambda de una sola linea poder añadirle la creacion de paramsgenerics
            {
                newBody = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")),body as ExpressionSyntax, SyntaxFactory.Token(SyntaxKind.SemicolonToken))).WithTriviaFrom(body);
            }
            
            if (newBody.DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => n.Identifier.Text == currentArgsName && !n.GetAnnotations("toChangeId").Any()).Any())   //chequea si hace falta instanciar la clase de los parametros
                newBody = newBody.WithStatements(newBody.Statements.Insert(0, CreateArgsClassInstruction(newBody.Statements[0].GetLeadingTrivia(), newBody.Statements[0].GetTrailingTrivia())));
            else
            {
                //en caso de que no hizo falta crea la clase params, entonces elimino su existencia (sustituyo por los parametros reales)
                var invocationToChange = newBody.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n => n.GetAnnotations("toChange").Any());
                newBody = newBody.ReplaceNodes(invocationToChange, (n1, n2) => n1.WithArgumentList(MakingInvocationArguments()));

                var arrayAccessExp = newBody.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(n => n.GetAnnotations("toChange").Any());
                newBody = newBody.ReplaceNodes(arrayAccessExp, (n1, n2) => SyntaxFactory.IdentifierName(currentparamsName + n1.GetAnnotations("toChange").First().Data));
               
                var memberAccessExpsIEnumerable = newBody.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n => n.GetAnnotations("toChangeToTupleMember").Any());
                newBody = newBody.ReplaceNodes(memberAccessExpsIEnumerable, (n1, n2) => MakingTupleValues());

            }
            return newBody;
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
                initializerExp = initializerExp.AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(currentparamsName + i)));
            }

            // ParamsGenerics2<int, int> a = new ParamsGenerics2<int, int>(__param0, _param1);
            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(genericName,
                            SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>().Add(SyntaxFactory.VariableDeclarator(currentArgsName).WithInitializer(
                                SyntaxFactory.EqualsValueClause(initializerExp))))).WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
        }

        // Construye ParamsGenerics2<int,int>
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
                return SyntaxFactory.IdentifierName(this.currentparamsName + "0");

            var newNode = SyntaxFactory.TupleExpression();  //mas de 1, tuplas
            for (int i = 0; i < cantArgumentsToDecorated; i++)
                newNode = newNode.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(this.currentparamsName + i.ToString())));

            return newNode;
        }

        protected void FillDecoratorTypeParams()
        {
            if (!toDecoratedMethodSymbol.IsStatic)
                this.specificDecoratorTypeParams[0] = SyntaxTools.GetTargetType(toDecorated,toDecoratedMethodSymbol);

            //this.specificDecoratorTypeParams[0] = SyntaxFactory.IdentifierName(toDecoratedMethodSymbol.ReceiverType.Name).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "));
            for (int i = (toDecoratedMethodSymbol.IsStatic) ? 0 : 1; i < this.specificDecoratorTypeParams.Length; i++)
            {
                this.specificDecoratorTypeParams[i] = toDecorated.ParameterList.Parameters[i - ((toDecoratedMethodSymbol.IsStatic) ? 0 : 1)].Type;
            }
        }

     
        #endregion
    }
}
