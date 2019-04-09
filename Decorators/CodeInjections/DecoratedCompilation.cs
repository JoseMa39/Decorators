using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Decorators.CodeInjections.ClassesToCreate;
using Decorators.DecoratorsCollector;
using Decorators.DecoratorsCollector.DecoratorClass;
using Decorators.DecoratorsCollector.IsDecoratorChecker;
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
        IEnumerable<IDecorator> decorators;
        List<int> classesToGen;
        IDecoratorChecker checker;

        string namespaceClassesGenerated;


        public DecoratedCompilation(Project project)
        {
            this.project = project;
            this.namespaceClassesGenerated = "DecoratorsClassesGenerated";
            this.checker = new CheckIsDecorator();
        }

        public async Task<Project> DecoratingProjectAsync(string outputRelPathModifiedFiles)
        {
            classesToGen = new List<int>();
            this.decorators = await DecoratorCollector.GetDecorators(this.project, this.checker);
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
            var semanticModel = compilation.GetSemanticModel(currentRoot.SyntaxTree);

            foreach (var node in currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n => n.DescendantNodes().OfType<AttributeSyntax>().Any(attr => this.checker.IsDecorateAttr(attr,semanticModel))))
            {
                currentRoot = DecoratingMethods(node, currentRoot, semanticModel);
            }
            //si hace falta insertar el using
            if(currentRoot.DescendantNodes().OfType<MemberDeclarationSyntax>().Any(n=>n.GetAnnotations("using").Any()))
            {
                currentRoot = AddUsing(currentRoot);
            }
            return currentRoot;
        }


        private SyntaxNode DecoratingMethods(MethodDeclarationSyntax node,SyntaxNode root, SemanticModel semanticModel)
        {
            var originalclass = GetOriginalClass(node, root);
            var modifiedClass = originalclass;  //los cambios se realizan sobre esta, necesito la clase sin cambios para poder reemplazarla

            List<string> decoratorsNames = new List<string>();   //para no crear el mismo decorador especifico dos veces
            var methodSymbol = semanticModel.GetDeclaredSymbol(node);   //obteniendo la informacion semantica del metodo a decorar

            var decoEnumerable = node.DescendantNodes().OfType<AttributeSyntax>().Where((item) => this.checker.IsDecorateAttr(item,semanticModel));
            foreach (var decoratorAttr in decoEnumerable)
            {
                //Buscando nombre del decorador
                string nombreDecorador = this.checker.ExtractDecoratorFullNameFromAttr(decoratorAttr,semanticModel);
                
                if (!decoratorsNames.Contains(nombreDecorador))
                {
                    decoratorsNames.Add(nombreDecorador);
                    
                    //Buscando decorador
                    var decoratorMethod = LookingForDecorator(nombreDecorador);

                    //Creando decorador con los tipos especificos de la funcion decorada
                    MemberDeclarationSyntax memberToAdd = decoratorMethod.CreateSpecificDecorator(node, methodSymbol);

                    if (memberToAdd.GetAnnotations("using").Any())
                    {
                        foreach (var item in memberToAdd.GetAnnotations("using"))
                        {
                            int cantArgs = int.Parse(item.Data);
                            
                            if (!this.classesToGen.Contains(cantArgs))  //guardando las clases que necesito generar
                                classesToGen.Add(cantArgs);
                        }
                    }
                    modifiedClass = modifiedClass.AddMembers(memberToAdd);  //revisar
                }    

            }

            //anadiendo funcion privada con el codigo de la funcion decorada
            var method = CreatePrivateMethod(node, semanticModel, methodSymbol);
            modifiedClass = modifiedClass.AddMembers(method);

            //Creando delegate estatico con la funcion decorada ///////////////////////////////////////////////////////
            var field = CreateStaticDelegateDecorated(node, decoEnumerable, methodSymbol,semanticModel);
            modifiedClass = modifiedClass.AddMembers(field);

            //Sustituyendo el codigo de la funcion a decorar (return staticDelegateDecorated(param1, ... , paramN))
            modifiedClass = modifiedClass.ReplaceNode(modifiedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n=> n.ToFullString() == node.ToFullString()).First(), ChangingToDecoratedCode(node,methodSymbol));

            root  = root.ReplaceNode(originalclass, modifiedClass);
            
            //Console.WriteLine(root.ToFullString());

            return root;
        }



        #region Funciones que editan el codigo

        //anade el metodo privado con el mismo codigo q la funcion decorada y devuelve un classSyntaxNode con esa modificacion
        private MethodDeclarationSyntax CreatePrivateMethod(MethodDeclarationSyntax node, SemanticModel toDecoratedSemanticModel, IMethodSymbol toDecoratedSymbol)
        {
            ToDecoratedPrivateRewriter rewriter = new ToDecoratedPrivateRewriter(node, toDecoratedSemanticModel, toDecoratedSymbol, this.checker);
            return rewriter.Visit(node) as MethodDeclarationSyntax;
        }

        //return __FuncPrivate(param1,param2,...)
        private MethodDeclarationSyntax ChangingToDecoratedCode(MethodDeclarationSyntax node, IMethodSymbol methodSymbol)
        {
            //quitando atributos de la funcion a decorar 
            node = node.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

            //Construyendo instruccion return decorador
            var argumentos = SyntaxFactory.ArgumentList();

            if (!methodSymbol.IsStatic)  //cuando es de instancia hay que anadir this como parametro
            {
                argumentos = argumentos.AddArguments(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
            }

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
        private FieldDeclarationSyntax CreateStaticDelegateDecorated(MethodDeclarationSyntax node, IEnumerable<AttributeSyntax> decoratorsAttrs, IMethodSymbol methodSymbol, SemanticModel model)
        {
            
            //creando lista con los argumentos de la funcion para crear el delegado
            var argumentList = SyntaxFactory.TypeArgumentList();

            if (!methodSymbol.IsStatic)
                argumentList = argumentList.AddArguments(SyntaxFactory.IdentifierName(methodSymbol.ReceiverType.Name));

            foreach (var item in node.ParameterList.Parameters)
            {
                argumentList = argumentList.AddArguments(item.Type);
            }
            argumentList = argumentList.AddArguments(node.ReturnType);

            //func<int,int,int>
            var fun = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), argumentList);

            var decoratorsAttrsReverse = decoratorsAttrs.Reverse();
            ExpressionSyntax inv = null;
            foreach (var item in decoratorsAttrs.Reverse())
            {
                var decorator = LookingForDecorator(this.checker.ExtractDecoratorFullNameFromAttr(item, model));
                if (inv == null)
                    inv = decorator.CreateInvocationToDecorator(node, methodSymbol, SyntaxFactory.IdentifierName("__" + node.Identifier.Text + "Private"), item);    // creando la invocacion al decorador
                else inv = decorator.CreateInvocationToDecorator(node, methodSymbol, inv, item); 
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

        private string GenerateClass(int cantParams, string path)
        {
            DecoratorParamClassGeneretor page = new DecoratorParamClassGeneretor(cantParams);
            string pageContent = page.TransformText();
            File.WriteAllText(path + $"\\ParamsGenerics{cantParams}.cs", pageContent);
            return pageContent;
        }
        #endregion


        #region Useful functions
        //Busca el decorador
        private IDecorator LookingForDecorator(string nameDecorator)
        {
            return decorators.Where(n => n.Identifier == nameDecorator).First();
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
            ClassDeclarationSyntax originalclass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();    //esta es la clase origimal del project
            return currentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(n => GetOriginalDefinition(n)== GetOriginalDefinition(originalclass) ).First();  //esta es la equivalente despues de haber hecho algun cambio
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
