﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Decorators.DecoratorsCollector.DecoratorClass;
using Decorators.DecoratorsCollector.PredefinedDecorators;
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
        readonly string funcDecAttr = typeof(DecorateWithAttribute).FullName;


        #region Tools
        bool IsDecoratorMethod(MethodDeclarationSyntax node, SemanticModel model)  //comprueba que tenga un parametro func<...> y tipo de retorno igual y sea estatica
        {
            IMethodSymbol methodSymbol = model.GetDeclaredSymbol(node) as IMethodSymbol;
            return (methodSymbol.IsStatic && methodSymbol.Parameters.Count() == 1 && methodSymbol.Parameters[0].OriginalDefinition.Type.ToDisplayString() == decoratorParamType && methodSymbol.ReturnType.ToDisplayString() == decoratorParamType);
        }
        bool IsDecoratorClass(ClassDeclarationSyntax node, SemanticModel model)
        {
            INamedTypeSymbol classSymbol = model.GetDeclaredSymbol(node) as INamedTypeSymbol;
            return IsDecoratorType(classSymbol, model);
        }

        //busca si el tipo es descendiente de la baseClass
        bool IsDecoratorType(INamedTypeSymbol classSymbol, SemanticModel model)
        {
            var current = classSymbol;
            while (current != null)
            {
                if (current.OriginalDefinition.ToDisplayString() == baseClass)
                    return true;
                current = current.BaseType;
            }
            return false;
        }
        #endregion

        #region IDecoratorChecker Methods
        public bool IsDecorator(SyntaxNode node, SemanticModel model)
        {
            if (node is ClassDeclarationSyntax)
                return IsDecoratorClass(node as ClassDeclarationSyntax, model);
            if (node is MethodDeclarationSyntax)
                return IsDecoratorMethod(node as MethodDeclarationSyntax, model);
            return false;
        }

        public bool IsDecorateAttr(AttributeSyntax attr, SemanticModel semanticModel)
        {
            INamedTypeSymbol attrSymbol = semanticModel.GetTypeInfo(attr).Type as INamedTypeSymbol;
            return IsDecoratorType(attrSymbol, semanticModel) || attrSymbol.OriginalDefinition.ToDisplayString() == this.funcDecAttr;
        }

        public string ExtractDecoratorFullNameFromAttr(AttributeSyntax attr, SemanticModel semanticModel)
        {
            var typeAttr = semanticModel.GetTypeInfo(attr).Type as INamedTypeSymbol;

            if (IsDecoratorType(typeAttr, semanticModel))   //decorador tipo clase
                return typeAttr.Name;


            return attr.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault()?.Token.ValueText ??    //decorador tipo funcion
                   attr.DescendantNodes()
                       .OfType<InvocationExpressionSyntax>().FirstOrDefault(exp => exp.DescendantNodes().FirstOrDefault() is IdentifierNameSyntax identifierNode &&
                           identifierNode.Identifier.Text == "nameof")?.ArgumentList.Arguments.First()
                       ?.Expression.WithoutTrivia().ToFullString().Split('.').Last();
        }

        public async Task<IEnumerable<IDecorator>> GetDecorators(Project project)
        {
            var compilation = await project.GetCompilationAsync();
            List<IDecorator> decorators = new List<IDecorator>();
            foreach (var docId in project.DocumentIds)
            {
                var doc = project.GetDocument(docId);
                var syntaxTree = await doc.GetSyntaxTreeAsync();
                var root = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                decorators.AddRange(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(node => this.IsDecorator(node, semanticModel)).Select(n => new DecoratorTypeFunctionToFunction(n, semanticModel)));
                decorators.AddRange(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(node => this.IsDecorator(node, semanticModel)).Select(n => new DecoratorTypeClassToFunction(n, semanticModel, this)));
            }

            decorators.AddRange(AddPredefinedDecorators());
            return decorators;
        }

        //revisar estructura
        private IEnumerable<IDecorator> AddPredefinedDecorators()
        {
            List<IDecorator> decorators = new List<IDecorator>();
            decorators.Add(new MemoizeDeco());

            return decorators;


        }
        #endregion
    }
}

