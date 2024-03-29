﻿using Decorators.CodeInjections;
using Decorators.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.DecoratorClass
{
    public enum TypeDecorator {Function, Class}
    class DecoratorTypeFunctionToFunction:IDecorator
    {
        readonly MethodDeclarationSyntax _decorator;
        readonly SemanticModel semanticModel;
        TypeDecorator type;
        public DecoratorTypeFunctionToFunction(MethodDeclarationSyntax decorator = null, SemanticModel semanticModel = null)
        {
            this._decorator = decorator;
            this.semanticModel = semanticModel;
            type = TypeDecorator.Function;
        }



        #region IDecorator
        public SyntaxNode DecoratorNode => _decorator;

        public virtual string Identifier { get => this._decorator.Identifier.Text;}
        public TypeDecorator Type { get => type; }

        public virtual string CurrentNamespaces
        {
            get
            {
                return this._decorator.Ancestors().OfType<NamespaceDeclarationSyntax>().First().Name.WithoutTrivia().ToFullString();
            }
        }

        public ExpressionSyntax CreateInvocationToDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol, ExpressionSyntax expr, AttributeSyntax attr, SemanticModel modelTodecorated)
        {
            var node = toDecorated as MethodDeclarationSyntax;

            string nameDecorator = GetNameSpecificDecorator(node.Identifier.Text);
            TypeSyntax type;

            if (SyntaxTools.HasGenericTypes(node))
                type = SyntaxFactory.GenericName(SyntaxFactory.Identifier(nameDecorator), SyntaxTools.MakeArgsFromParams(node.TypeParameterList));
            else type = SyntaxFactory.IdentifierName(nameDecorator);


            return SyntaxFactory.InvocationExpression(type, SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(expr)));
        }

        public virtual MemberDeclarationSyntax CreateSpecificDecorator(SyntaxNode toDecorated, IMethodSymbol toDecoratedSymbol)
        {
            SpecificDecoratorFuncRewriterVisitor deco = new SpecificDecoratorFuncRewriterVisitor(this.semanticModel, toDecoratedSymbol, this._decorator , toDecorated as MethodDeclarationSyntax);
            var newDecorator = deco.Visit(_decorator);
            return newDecorator as MethodDeclarationSyntax;
        }

        public virtual IEnumerable<UsingDirectiveSyntax> GetUsingNamespaces()
        {
            return this._decorator.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>();
        }
        #endregion

        #region tools
        private string GetNameSpecificDecorator(string nameMethodToDecorated)  //devuelve el nombre con que se generaran los decoradores de este tipo
        {
            //return SyntaxTools.FormatterStringNames(_decorator.Identifier.Text, nameMethodToDecorated);
            return SyntaxTools.FormatterStringNames(this.Identifier, nameMethodToDecorated);

        }

        #endregion
    }
}
