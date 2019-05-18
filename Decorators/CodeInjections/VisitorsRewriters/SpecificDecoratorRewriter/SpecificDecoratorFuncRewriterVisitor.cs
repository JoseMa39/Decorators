using Decorators.CodeInjections.ClassesToCreate;
using Decorators.Utilities;
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
    internal class SpecificDecoratorFuncRewriterVisitor : SpecificDecoratorRewriter
    {
        public SpecificDecoratorFuncRewriterVisitor(SemanticModel modeloSemanticoDecorator, IMethodSymbol toDecoratedMethodSymbol, MethodDeclarationSyntax decoratorMethod, MethodDeclarationSyntax toDecorated):base(modeloSemanticoDecorator,toDecoratedMethodSymbol,toDecorated)
        {
            this.decoratorMethod = decoratorMethod;
        }

        //para llenar el array con los parametros
       
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            bool isWrapper = IsWrapperDecorator(node);

            //guardo current args name por si hay mas de una funcion anidada
            string temp = currentArgsName;

            //para saber el nombre con que se trata a los parametros dentro de la funcion
            if (isWrapper)
            {
                currentArgsName = node.ParameterList.Parameters[0].Identifier.Text;
                currentparamsName = this.paramsName + (++this.deep);  //para que no coincidan los nombres en caso de anidamiento de funciones
            }

            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;
            //si el metodo no es el decorador  (revisar,  me gustaria usar un atributo decorator)

            if (node.Identifier.Text != decoratorMethod.Identifier.Text)
            {
                if (isWrapper)  //reconociendo se se trata de un wrapper a la funcion decorada
                {
                    var parameters = SyntaxFactory.ParameterList();
                    //creando lista de parametros (el nombre de cada uno es __param0, __param1, etc)
                    var paramArray = MakingParameters();
                    //actualizando tipo de retorno
                    node = node.WithParameterList(parameters.AddParameters(paramArray)).WithReturnType(toDecorated.ReturnType);
                    node = node.WithBody(WorkingWithWrapperFunction(node.Body) as BlockSyntax);
                    currentparamsName = this.paramsName + (--this.deep);
                }
                currentArgsName = temp;
                return node;
            }



            //func<int,int,int>  y cambiando el nombre a __DecoratorToDecorate
            SimpleNameSyntax fun;
            if (hasModifierParams)
                fun = SyntaxTools.MakingDelegateName(toDecorated, toDecoratedMethodSymbol);
            else
                fun = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), MakingFuncDelegateTypeArguments());


            node = node.WithIdentifier(SyntaxFactory.Identifier("__" + decoratorMethod.Identifier.Text + toDecorated.Identifier.Text)).WithReturnType(fun);

            //generando el parametro para el decorador
            ParameterSyntax param = SyntaxFactory.Parameter(node.ParameterList.Parameters[0].Identifier).WithType(fun);

            node = node.WithParameterList(SyntaxFactory.ParameterList().AddParameters(param).WithTriviaFrom(node.ParameterList));

            //si hace falta generar clase, anado una annotation ("using", cantParams) para luego poder anadir la referencia correspondiente y generar la clase
            if (node.DescendantTokens().OfType<SyntaxToken>().Where(n => n.Kind() == SyntaxKind.IdentifierToken && n.Text == (paramClassGenerated + cantArgumentsToDecorated.ToString())).Any())
                node = node.WithAdditionalAnnotations(new SyntaxAnnotation("using", cantArgumentsToDecorated.ToString()));

            return node.WithConstraintClauses(toDecorated.ConstraintClauses).WithTypeParameterList(toDecorated.TypeParameterList).WithModifiers(SyntaxTools.AddingPrivateModifier(node.Modifiers));
        }
        
    }
}
