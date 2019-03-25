using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Decorators.CodeInjections.ClassesToCreate;
using Decorators.DecoratorsCollector;
using Decorators.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace Decorators.CodeInjections
{
    class DecoratedCompilation
    {
        Project project;
        Compilation compilation;
        IEnumerable<MethodDeclarationSyntax> decorators;
        List<int> classesToGen;

        string namespaceClassesGenerated;


        public DecoratedCompilation(Compilation compilation)
        {
            this.compilation = compilation;
            this.namespaceClassesGenerated = "DecoratorsClassesGenerated";
        }

        public DecoratedCompilation(Project project)
        {
            this.project = project;
            this.namespaceClassesGenerated = "DecoratorsClassesGenerated";
        }

        public async Task<Project> DecoratingProjectAsync(string outputRelPathModifiedFiles)
        {
            classesToGen = new List<int>();
            this.decorators = await DecoratorCollector.GetDecorators(this.project, new CheckDecoratorWithAttr());
            var currentProject = this.project;
            this.compilation = await currentProject.GetCompilationAsync();

            string directoryOutput = IOUtilities.BasePath(project.FilePath) + "\\" + outputRelPathModifiedFiles;

            CleanDirectory(directoryOutput);

            foreach (var doc in this.project.Documents)
            {
                var currentRoot = await doc.GetSyntaxRootAsync();
                var oldSyntaxTree = currentRoot.SyntaxTree;

                foreach (var node in currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    currentRoot = DecoratingMethods(node, currentRoot);
                }


                if (oldSyntaxTree != currentRoot.SyntaxTree)  //si cambio el syntaxtree, creo un fichero nuevo con las modificaciones
                {
                    this.compilation = compilation.ReplaceSyntaxTree(oldSyntaxTree, currentRoot.SyntaxTree);
                    Directory.CreateDirectory(directoryOutput);
                    IOUtilities.WriteSyntaxTreeInFile(IOUtilities.BasePath(currentProject.FilePath) + "\\" + outputRelPathModifiedFiles + "\\" + Path.GetFileName(oldSyntaxTree.FilePath), currentRoot.SyntaxTree);

                    currentProject = currentProject.RemoveDocument(doc.Id);
                    currentProject = currentProject.AddDocument(doc.Name, currentRoot).Project;
                }

            }

            foreach (var cantParams in this.classesToGen)
            {
                string code = GenerateClass(cantParams, IOUtilities.BasePath(currentProject.FilePath) + "\\" + outputRelPathModifiedFiles);
                currentProject = currentProject.AddDocument($"ParamsGenerics{cantParams}.cs", code).Project;
            }
            classesToGen = null;
            return currentProject;
        }

        private void CleanDirectory(string directoryOutput)
        {
            try  //por si es la primera vez que no existe el directorio
            {
                foreach (var item in Directory.GetFiles(directoryOutput))
                {
                    File.Delete(item);
                }
                Directory.Delete(directoryOutput);
            }
            catch (Exception) {}
        }

        public Compilation Decorating()
        {
            this.decorators = DecoratorCollector.GetDecorators(compilation, new CheckDecoratorWithAttr());

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

            ClassDeclarationSyntax originalclass = node.Ancestors().OfType<ClassDeclarationSyntax>().First();   
            var modifiedClass = originalclass;
            List<string> decoratorsNames = new List<string>();   //para no crear el mismo decorador especifico dos veces
            //siempre se inicializan despues
            MethodDeclarationSyntax method = null;
            bool addUsing=false;

            var decoEnumerable = node.DescendantNodes().OfType<AttributeSyntax>().Where((item) =>(new DecoratorAttrChecker()).IsDecorateAttr(item));
            foreach (var decorator in decoEnumerable)
            {
                //Buscando nombre del decorador
                AttributeSyntax attr = node.DescendantNodes().OfType<AttributeSyntax>().First(item => item.Name.ToString() == "DecorateWith");
                string nombreDecorador = ExtractDecoratorFullName(attr);

                if (!decoratorsNames.Contains(nombreDecorador))
                {
                    decoratorsNames.Add(nombreDecorador); 
                    
                    //Buscando decorador
                    var decoratorMethod = LookingForDecorator(root, nombreDecorador);

                    //Creando decorador con los tipos especificos de la funcion decorada
                    method = CreateSpecificDecorator(decoratorMethod, node, root.SyntaxTree);

                    addUsing = addUsing || method.GetAnnotations("using").Any();

                    modifiedClass = modifiedClass.AddMembers(method);
                }    

            }

            //anadiendo funcion privada con el codigo de la funcion decorada
             method = CreatePrivateMethod(node);
             modifiedClass = modifiedClass.AddMembers(method);

            //Creando delegate estatico con la funcion decorada
            var field = CreateStaticDelegateDecorated(node, decoEnumerable);
            modifiedClass = modifiedClass.AddMembers(field);

            //Sustituyendo el codigo de la funcion a decorar (return staticDelegateDecorated(param1, ... , paramN))
            modifiedClass = modifiedClass.ReplaceNode(modifiedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n=> n.ToFullString() == node.ToFullString()).First(), ChangingToDecoratedCode(node));

            root  = root.ReplaceNode(originalclass, modifiedClass);
            
            //anadiendo los using necesarios en caso de que se genere la clase para los parametros
            if (addUsing)
            {
                //var annotation = specificDeco.GetAnnotations("using").First();
                var using1 = SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(this.namespaceClassesGenerated).WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" "))).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"));
                SyntaxNode[] temp = { using1 };
                root = root.InsertNodesBefore(root.ChildNodes().First(), temp);

                if (!this.classesToGen.Contains(node.ParameterList.Parameters.Count))  //guardando las clases que necesito generar
                    classesToGen.Add(node.ParameterList.Parameters.Count);
            }

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


        private FieldDeclarationSyntax CreateStaticDelegateDecorated(MethodDeclarationSyntax node, IEnumerable<AttributeSyntax> decoratorsAttrs)
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

            var decoratorsAttrsReverse = decoratorsAttrs.Reverse();
            InvocationExpressionSyntax inv = null;
            foreach (var item in decoratorsAttrs)
            {
                if (inv == null)
                    inv = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__" + ExtractDecoratorFullName(item) + node.Identifier.Text), SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("__" + node.Identifier.Text + "Private"))));
                else inv = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("__" + ExtractDecoratorFullName(item) + node.Identifier.Text), SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.Argument(inv)));
            }

            //__fibDecorator(__FibPrivate)
            var varInitialization = SyntaxFactory.EqualsValueClause(inv);


            //__FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("__" + node.Identifier.Text + "Decorated")).WithInitializer(varInitialization);


            //func<int,int,int> = __FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclaration = SyntaxFactory.VariableDeclaration(fun).AddVariables(varDeclarator);

            //anadiendo public y static
            return SyntaxFactory.FieldDeclaration(varDeclaration).AddModifiers(SyntaxFactory.Token(node.GetLeadingTrivia(), SyntaxKind.PublicKeyword, SyntaxFactory.ParseTrailingTrivia(" ")), SyntaxFactory.Token(SyntaxFactory.ParseLeadingTrivia(""), SyntaxKind.StaticKeyword, SyntaxFactory.ParseTrailingTrivia(" ")));

        }

        private MethodDeclarationSyntax CreateSpecificDecorator(MethodDeclarationSyntax decoratorMethod, MethodDeclarationSyntax toDecorated, SyntaxTree tree)
        {
            SpecificDecoratorRewriterVisitor deco = new SpecificDecoratorRewriterVisitor(compilation.GetSemanticModel(tree), decoratorMethod, toDecorated);
            var newDecorator = deco.Visit(decoratorMethod);
            return newDecorator as MethodDeclarationSyntax;
        }


        private string GenerateClass(int cantParams, string path)
        {
            DecoratorParamClassGeneretor page = new DecoratorParamClassGeneretor(cantParams);
            string pageContent = page.TransformText();
            File.WriteAllText(path + $"\\ParamsGenerics{cantParams}.cs", pageContent);
            return pageContent;
        }
        #endregion


        #region Useful functions
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

        #endregion

    }
}
