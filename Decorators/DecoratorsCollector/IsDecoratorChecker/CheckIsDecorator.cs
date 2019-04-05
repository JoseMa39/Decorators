using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DecoratorsDLL.DecoratorsClasses;
using DecoratorsDLL.DecoratorsClasses.DynamicTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Decorators.DecoratorsCollector.IsDecoratorChecker
{
    //dice si un metodo es decorador si tiene el atributo [decorator]
    class CheckIsDecorator : IDecoratorChecker
    {
        readonly string decoratorParamType = $"System.Func<{typeof(DynamicParamsCollection).FullName}, {typeof(DynamicResult).FullName}>";
        readonly string baseClass = typeof(DecoratorBaseClass).FullName;
        bool IsDecoratorMethod(MethodDeclarationSyntax node, SemanticModel model)  //comprueba que tenga un parametro func<...> y tipo de retorno igual y sea estatica
        {
            IMethodSymbol methodSymbol = model.GetDeclaredSymbol(node) as IMethodSymbol;
            return (methodSymbol.IsStatic && methodSymbol.Parameters.Count() == 1 && methodSymbol.Parameters[0].OriginalDefinition.Type.ToDisplayString() == decoratorParamType && methodSymbol.ReturnType.ToDisplayString() == decoratorParamType);
        }

        bool IsDecoratorClass(ClassDeclarationSyntax node, SemanticModel model)
        {
            INamedTypeSymbol classSymbol = model.GetDeclaredSymbol(node) as INamedTypeSymbol;
            var current = classSymbol;
            while (current != null)
            {
                if (current.OriginalDefinition.ToDisplayString() == "Decorator")
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        public bool IsDecorator(INamedTypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                if (current.Name == "Decorator")
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        public bool IsDecorator(SyntaxNode node, SemanticModel model)
        {
            if (node is ClassDeclarationSyntax)
                return IsDecoratorClass(node as ClassDeclarationSyntax, model);
            if (node is MethodDeclarationSyntax)
                return IsDecoratorMethod(node as MethodDeclarationSyntax, model);
            return false;
        }
    }
}
