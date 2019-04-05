using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.DecoratorClass
{
    internal enum TypeDecorator {Function, Class}
    class Decorator
    {
        readonly SyntaxNode _decorator;
        TypeDecorator type;
        public Decorator(MethodDeclarationSyntax decorator)
        {
            this._decorator = decorator;
            type = TypeDecorator.Function;
        }
        public Decorator(ClassDeclarationSyntax decorator)
        {
            this._decorator = decorator;
            type = TypeDecorator.Class;
        }

        public TypeDecorator Type => type;

        public SyntaxNode DecoratorNode => _decorator;
    }
}
