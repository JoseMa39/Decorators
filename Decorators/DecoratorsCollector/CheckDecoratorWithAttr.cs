using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Decorators.DecoratorsCollector
{
    //dice si un metodo es decorador si tiene el atributo [decorator]
    class CheckDecoratorWithAttr : IDecoratorChecker
    {
        public bool IsDecorator(MethodDeclarationSyntax node, SemanticModel model)
        {
            return true;
            IMethodSymbol methodSymbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            return methodSymbol.GetAttributes().Any(attr => attr.AttributeClass.Name == "DecoratorAttribute");
        }
    }
}
