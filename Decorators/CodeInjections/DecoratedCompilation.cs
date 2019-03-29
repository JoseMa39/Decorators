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

                currentRoot = DecoratingSyntaxTree(currentRoot);
                
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

        private SyntaxNode DecoratingSyntaxTree(SyntaxNode currentRoot)
        {
            foreach (var node in currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var other = compilation.GetSemanticModel(currentRoot.SyntaxTree);
            }
            foreach (var node in currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n => IsDecorated(n)))
            {
                

                currentRoot = DecoratingMethods(node, currentRoot);
            }
            //si hace falta insertar el using
            if(currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(n=>n.GetAnnotations("using").Any()))
            {
                currentRoot = AddUsing(currentRoot);
            }
            return currentRoot;
        }


        private SyntaxNode DecoratingMethods(MethodDeclarationSyntax node,SyntaxNode root)
        {
            var originalclass = GetOriginalClass(node, root);
            var modifiedClass = originalclass;  //los cambios se realizan sobre esta, necesito la clase sin cambios para poder reemplazarla

            List<string> decoratorsNames = new List<string>();   //para no crear el mismo decorador especifico dos veces
            //siempre se inicializan despues
            MethodDeclarationSyntax method = null;

            var decoEnumerable = node.DescendantNodes().OfType<AttributeSyntax>().Where((item) =>(new DecoratorAttrChecker()).IsDecorateAttr(item));////////////////////////////
            foreach (var decorator in decoEnumerable)
            {
                //Buscando nombre del decorador
                AttributeSyntax attr = node.DescendantNodes().OfType<AttributeSyntax>().First(item => item.Name.ToString() == "DecorateWith");

                string nombreDecorador = ExtractDecoratorFullName(attr);
                
                if (!decoratorsNames.Contains(nombreDecorador))
                {
                    decoratorsNames.Add(nombreDecorador);

                    //Buscando decorador
                    var decoratorMethod = LookingForDecorator(nombreDecorador);

                    //Creando decorador con los tipos especificos de la funcion decorada
                    method = CreateSpecificDecorator(decoratorMethod, node);

                    if(method.GetAnnotations("using").Any())
                    {
                        foreach (var item in method.GetAnnotations("using"))
                        {
                            int cantArgs = int.Parse(item.Data);
                            
                            if (!this.classesToGen.Contains(cantArgs))  //guardando las clases que necesito generar
                                classesToGen.Add(cantArgs);
                        }
                    }

                    modifiedClass = modifiedClass.AddMembers(method);
                }    

            }

            //anadiendo funcion privada con el codigo de la funcion decorada
             method = CreatePrivateMethod(node);
             modifiedClass = modifiedClass.AddMembers(method);

            //Creando delegate estatico con la funcion decorada ///////////////////////////////////////////////////////
            var field = CreateStaticDelegateDecorated(node, decoEnumerable);
            modifiedClass = modifiedClass.AddMembers(field);

            //Sustituyendo el codigo de la funcion a decorar (return staticDelegateDecorated(param1, ... , paramN))
            modifiedClass = modifiedClass.ReplaceNode(modifiedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n=> n.ToFullString() == node.ToFullString()).First(), ChangingToDecoratedCode(node));

            root  = root.ReplaceNode(originalclass, modifiedClass);
            
            //Console.WriteLine(root.ToFullString());

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

        //return __FuncPrivate(param1,param2,...)
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
            temp1 = temp1.WithReturnKeyword(temp1.ReturnKeyword.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "))).WithTriviaFrom(node.Body.Statements[0]);
            SyntaxList<StatementSyntax> stmt = new SyntaxList<StatementSyntax>(temp1);
            return node.WithBody(node.Body.WithStatements(stmt));
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

        private MethodDeclarationSyntax CreateSpecificDecorator(MethodDeclarationSyntax decoratorMethod, MethodDeclarationSyntax toDecorated)
        {
            SpecificDecoratorRewriterVisitor deco = new SpecificDecoratorRewriterVisitor(compilation.GetSemanticModel(decoratorMethod.SyntaxTree), compilation.GetSemanticModel(toDecorated.SyntaxTree), decoratorMethod, toDecorated);
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
        private MethodDeclarationSyntax LookingForDecorator(string nameDecorator)
        {
            return decorators.Where(n => n.Identifier.Text == nameDecorator).First();
        }

        private bool IsDecorated(MethodDeclarationSyntax node)
        {
            return (node.DescendantNodes().OfType<AttributeSyntax>().Any(item => item.Name.ToString() == "DecorateWith"));
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
            catch (Exception) { }
        }

        /// <summary>
        /// Add using directive with generated classes namespace
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private SyntaxNode AddUsing(SyntaxNode root)
        {
            var using1 = SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(this.namespaceClassesGenerated).WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" "))).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"));
            SyntaxNode[] temp = { using1 };
            root = root.InsertNodesBefore(root.ChildNodes().First(), temp);
            return root;
        }
        private ClassDeclarationSyntax GetOriginalClass(MethodDeclarationSyntax method, SyntaxNode currentRoot)
        {
            ClassDeclarationSyntax originalclass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
            return currentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(n => GetOriginalDefinition(n)== GetOriginalDefinition(originalclass) ).First();
        }

        private string GetOriginalDefinition(ClassDeclarationSyntax classNode)   //devuelve el nombre completo de la clase (Ej:mynamespace.class1.class2)
        {
            var containsClasses = classNode.Ancestors().OfType<ClassDeclarationSyntax>();
            string originalDefinition = classNode.Identifier.Text;
            if (containsClasses!=null)
            {
                foreach (var item in containsClasses)
                {
                    originalDefinition = item.Identifier.Text + "." + originalDefinition;
                }
            }
            return classNode.Ancestors().OfType<NamespaceDeclarationSyntax>().First().Name.ToFullString() + "." + originalDefinition;
        }

        #endregion

    }
}
