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
    class MakingDecoratedCompilation
    {
        Compilation compilation;
        IEnumerable<MethodDeclarationSyntax> decorators;

        public MakingDecoratedCompilation(Compilation compilation, IEnumerable<MethodDeclarationSyntax> decorators)
        {
            this.compilation = compilation;
            this.decorators = decorators;
        }

        public Compilation Decorating()
        {
            foreach (var oldSyntaxTree in compilation.SyntaxTrees)
            {
                var root = oldSyntaxTree.GetRoot();
                foreach (var item in root.DescendantNodes())
                {
                    if (item is MethodDeclarationSyntax)
                        root = DecoratingMethods(item as MethodDeclarationSyntax, root);
                }
                this.compilation = compilation.ReplaceSyntaxTree(oldSyntaxTree, root.SyntaxTree);
            }
            
            return this.compilation;
        }
        //Para cada declaracion de metodo
        private SyntaxNode DecoratingMethods(MethodDeclarationSyntax node,SyntaxNode root)
        {
            //Revisar si esta decorado
            if (!node.DescendantNodes().OfType<AttributeSyntax>().Any(item => item.Name.ToString() == "DecorateWith"))
                return root;

            //Buscando nombre del decorador
            AttributeSyntax attr = node.DescendantNodes().OfType<AttributeSyntax>().First(item => item.Name.ToString() == "DecorateWith");
            string nombreDecorador = ExtractDecoratorFullName(attr);

            //Buscando decorador
            var decoratorMethod = LookingForDecorator(root, nombreDecorador);
            var originalclass = node.Ancestors().OfType<ClassDeclarationSyntax>().First();


            //Creando decorador con los tipos especificos de la funcion decorada
            var method = CreateSpecificDecorator(decoratorMethod, node, root.SyntaxTree);
            var modifiedClass = originalclass.AddMembers(method);

            //anadiendo funcion privada con el codigo de la funcion decorada
             method = CreatePrivateMethod(node);
             modifiedClass = modifiedClass.AddMembers(method);


            //Creando delegate estatico con la funcion decorada
            var field = CreateStaticDelegateDecorated(node, nombreDecorador);
            modifiedClass = modifiedClass.AddMembers(field);

            //Sustituyendo el codigo de la funcion a decorar (return staticDelegateDecorated(param1, ... , paramN))
            modifiedClass = modifiedClass.ReplaceNode(modifiedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n=> n.ToFullString() == node.ToFullString()).First(), ChangingToDecoratedCode(node));

            //Console.WriteLine(originalclass.ToFullString());
            //Console.WriteLine("---------------------------");
            //Console.WriteLine(modifiedClass.ToFullString());

            root  = root.ReplaceNode(originalclass, modifiedClass);
            return root;
        }

        #region Funciones que editan el codigo

        //anade el metodo privado con el mismo codigo q la funcion decorada y devuelve un classSyntaxNode con esa modificacion
        private MethodDeclarationSyntax CreatePrivateMethod(MethodDeclarationSyntax node)
        {
            //agregando metodo privado y guardando en él el metodo a decorar
            SyntaxToken name = SyntaxFactory.Identifier("__" + node.Identifier.ToString() + "Private");

            //quitando el decorador en el nuevo metodo
            var atributos = SyntaxFactory.SeparatedList<AttributeSyntax>(node.DescendantNodes().OfType<AttributeSyntax>().Where(n => n.Name.ToString() != "DecorateWith"));
            AttributeListSyntax listaAtr = SyntaxFactory.AttributeList(atributos);
            List<AttributeListSyntax> lista = new List<AttributeListSyntax>();
            lista.Add(listaAtr);
            SyntaxList<AttributeListSyntax> aux = SyntaxFactory.List<AttributeListSyntax>();

            if (lista.Count>0)
                aux.AddRange(lista);

            return SyntaxFactory.MethodDeclaration(aux, node.Modifiers, node.ReturnType, node.ExplicitInterfaceSpecifier, name, node.TypeParameterList, node.ParameterList, node.ConstraintClauses, node.Body, node.SemicolonToken);

        }


        private MethodDeclarationSyntax ChangingToDecoratedCode(MethodDeclarationSyntax node)
        {
            //quitando atributos de la funcion a decorar 
            node = node.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

            //Construyendo instruccion return decorador
            var argumentos = SyntaxFactory.ArgumentList();

            foreach (var item in node.ParameterList.Parameters)
            {
                var arg = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(item.Identifier.Text));
                argumentos = argumentos.AddArguments(arg);
            }

            var invocacion = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__"+node.Identifier.Text + "Decorated"), argumentos);
            var temp1 = SyntaxFactory.ReturnStatement(invocacion);
            temp1 = temp1.WithReturnKeyword(temp1.ReturnKeyword.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
            BlockSyntax body = SyntaxFactory.Block(temp1);
            return node.WithBody(body);
        }


        //crea un delegado estatico que guarda la funcion decorada
        private FieldDeclarationSyntax CreateStaticDelegateDecorated(MethodDeclarationSyntax node, string decoratorName)
        {
            //creando lista con los argumentos de la funcion para crear el delegado
            var argumentList = SyntaxFactory.TypeArgumentList();
            foreach (var item in node.ParameterList.Parameters)
            {
                argumentList = argumentList.AddArguments(item.Type);
            }
            argumentList = argumentList.AddArguments(node.ReturnType);

            //func<int,int,int>
            var fun = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), argumentList);

            //__fibDecorator(__FibPrivate)
            var varInitialization = SyntaxFactory.EqualsValueClause(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__" + decoratorName + node.Identifier.Text), SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("__" + node.Identifier.Text + "Private")))));
            //__FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("__" + node.Identifier.Text + "Decorated")).WithInitializer(varInitialization);
            //func<int,int,int> = __FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclaration = SyntaxFactory.VariableDeclaration(fun).AddVariables(varDeclarator);

            //anadiendo public y static
            return SyntaxFactory.FieldDeclaration(varDeclaration).AddModifiers(SyntaxFactory.Token(node.GetLeadingTrivia(), SyntaxKind.PublicKeyword,SyntaxFactory.ParseTrailingTrivia(" ")), SyntaxFactory.Token(SyntaxFactory.ParseLeadingTrivia(""), SyntaxKind.StaticKeyword, SyntaxFactory.ParseTrailingTrivia(" ")));

        }
        
        private MethodDeclarationSyntax CreateSpecificDecorator(MethodDeclarationSyntax decoratorMethod, MethodDeclarationSyntax toDecorated, SyntaxTree tree)
        {
            //DecoratorRewriter deco = new DecoratorRewriter(compilation.GetSemanticModel(tree), decoratorMethod, toDecorated);
            SpecificDecoratorRewriterVisitor deco = new SpecificDecoratorRewriterVisitor(compilation.GetSemanticModel(tree), decoratorMethod, toDecorated);
            var newDecorator = deco.Visit(decoratorMethod);
            //Console.WriteLine(newDecorator.ToFullString());
            return newDecorator as MethodDeclarationSyntax;
        }

        #endregion

        //Extrae el nombre del decorador
        internal static string ExtractDecoratorFullName(AttributeSyntax attr)
        {
            //var decoratorParameterName = attr.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault();

            return attr.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault()?.Token.ValueText ??
                   attr.DescendantNodes()
                       .OfType<InvocationExpressionSyntax>().FirstOrDefault(exp => exp.DescendantNodes().FirstOrDefault() is IdentifierNameSyntax identifierNode &&
                           identifierNode.Identifier.Text == "nameof")?.ArgumentList.Arguments.First()
                       ?.Expression.ToFullString();
        }
        //Busca el decorador
        private MethodDeclarationSyntax LookingForDecorator(SyntaxNode root, string nameDecorator)
        {
            return decorators.Where(n => n.Identifier.Text == nameDecorator).First();
        }

        
       
    }
}
