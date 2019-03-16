using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace Decorators.CodeInjections
{
    class DecoratorsSyntaxWalkers : CSharpSyntaxWalker
    {
        public readonly List<MethodDeclarationSyntax> decorators;
        public DecoratorsSyntaxWalkers()
        {
            decorators = new List<MethodDeclarationSyntax>();
        }


        public IEnumerable<MethodDeclarationSyntax> DecoratorsMethods { get { return decorators; } }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if(IsDecorator(node))
            {
                decorators.Add(node);
            }
        }

        public bool IsDecorator(MethodDeclarationSyntax node)
        {
            return true;
        }
    }
}
