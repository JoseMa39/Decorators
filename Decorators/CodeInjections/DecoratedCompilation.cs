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
using Decorators.Utilities.ErrorLogger;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PluginCake;

namespace Decorators.CodeInjections
{
    public class DecoratedCompilation:IProjectDecorator,IPlugin
    {
        //Project project;
        Compilation compilation;
        IEnumerable<IDecorator> decorators;
        List<int> classesToGen;
        IDecoratorChecker checker;
        IErrorLog log;
        
        string namespaceClassesGenerated;
        string paramClassGenerated;


        List<string> predependecias;
        List<string> postdependencies;


        

        public DecoratedCompilation(IErrorLog log = null)
        {
            this.predependecias = new List<string>();
            this.postdependencies = new List<string>();
            this.namespaceClassesGenerated = "DecoratorsClassesGenerated";
            this.paramClassGenerated = "ParamsGenerics";
            this.log = (log==null)?new ErrorLog():log;
        }

        #region
        public Project Execute(Project sol)
        {
            return DecoratingProjectAsync(sol, new CheckIsDecorator(), "outFolder").Result;
        }

        public List<string> Predependencies { get => predependecias; set => predependecias = value; }
        public List<string> Postdependencies { get => postdependencies; set => postdependencies = value; }
        public string Name { get => "FancyDeco"; }
        public string Version { get => "1.00"; }

        string IPlugin.Name { get => "FancyDeco"; set => throw new NotImplementedException(); }
        string IPlugin.Version { get => "1.00"; set => throw new NotImplementedException(); }
        #endregion

        public Project DecoratingProject(Project project)
        {
            return DecoratingProjectAsync(project, new CheckIsDecorator(), "outFolder").Result;
        }

        #region Decorating Functions
        internal async Task<Project> DecoratingProjectAsync(Project project, IDecoratorChecker decoratorRecognize, string outputRealPathModifiedFiles)
        {
            
            this.checker = decoratorRecognize;
            classesToGen = new List<int>();
            this.decorators = await this.checker.GetDecorators(project);

            var currentProject = project;
            this.compilation = await currentProject.GetCompilationAsync();

            if (SyntaxTools.CheckErrors(compilation,log))   //si el project tiene algun error de compilacion
                return project;

            string directoryOutput = IOUtilities.BasePath(project.FilePath) + "\\" + outputRealPathModifiedFiles;
            CleanDirectory(directoryOutput);  //limpiando carpeta de salida


            foreach (var doc in project.Documents)
            {
                var currentRoot = await doc.GetSyntaxRootAsync();
                var oldSyntaxTree = currentRoot.SyntaxTree;

                currentRoot = DecoratingSyntaxTree(currentRoot);

                if (oldSyntaxTree != currentRoot.SyntaxTree)  //si cambio el syntaxtree, creo un fichero nuevo con las modificaciones
                {
                    this.compilation = compilation.ReplaceSyntaxTree(oldSyntaxTree, currentRoot.SyntaxTree);
                    Directory.CreateDirectory(directoryOutput);
                    IOUtilities.WriteSyntaxTreeInFile(IOUtilities.BasePath(currentProject.FilePath) + "\\" + outputRealPathModifiedFiles + "\\" + Path.GetFileName(oldSyntaxTree.FilePath), currentRoot.SyntaxTree);

                    currentProject = currentProject.RemoveDocument(doc.Id);
                    currentProject = currentProject.AddDocument(doc.Name, currentRoot).Project;
                }
            }


            currentProject = GenerateAllNeededClasses(currentProject, outputRealPathModifiedFiles);  //generando clases

            var newCompilation = await currentProject.GetCompilationAsync();    //chequeando errores
            SyntaxTools.CheckErrors(newCompilation, log);

            return currentProject;
        }

        private SyntaxNode DecoratingSyntaxTree(SyntaxNode currentRoot)
        {
            var semanticModel = compilation.GetSemanticModel(currentRoot.SyntaxTree);

            foreach (var node in currentRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n => n.Body!=null && n.DescendantNodes().OfType<AttributeSyntax>().Any(attr => this.checker.IsDecorateAttr(attr,semanticModel))))
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
            var modifiedClass = originalclass;  //los cambios se realizan sobre esta, necesito la clase sin cambios para poder reemplazarla ()

            List<string> decoratorsNames = new List<string>();
            List<UsingDirectiveSyntax> usingsDirectives = new List<UsingDirectiveSyntax>();   //para no crear el mismo decorador especifico dos veces

            var methodSymbol = semanticModel.GetDeclaredSymbol(node);   //obteniendo la informacion semantica del metodo a decorar

            bool hasParamsModifiers = SyntaxTools.HasParamsModifiers(methodSymbol);

            #region Generating specific decorators
            var decoEnumerable = node.DescendantNodes().OfType<AttributeSyntax>().Where((item) => this.checker.IsDecorateAttr(item,semanticModel));
            foreach (var decoratorAttr in decoEnumerable)   //puede estar decorada con mas de un decorador
            {
                //Buscando nombre del decorador
                string nombreDecorador = this.checker.ExtractDecoratorFullNameFromAttr(decoratorAttr,semanticModel);
                
                if (!decoratorsNames.Contains(nombreDecorador))
                {
                    decoratorsNames.Add(nombreDecorador);
                    
                    //Buscando decorador
                    var decoratorMethod = LookingForDecorator(nombreDecorador, decoratorAttr);
                    if (decoratorMethod == null)
                        return root;

                    //Creando decorador con los tipos especificos de la funcion decorada
                    MemberDeclarationSyntax memberToAdd = decoratorMethod.CreateSpecificDecorator(node, methodSymbol);

                    AddDecoUsingToList(usingsDirectives, decoratorMethod);    //anade los using del decorador al cs donde es aplicado

                    UpdateToGenerateClasses(memberToAdd);   //actualiza la lista de clases paramsGenerics a generar
                   
                    modifiedClass = modifiedClass.AddMembers(memberToAdd);  //anadiendo el decorador especifico
                }    
            }
            #endregion

            #region adding private function which contain decorated function code

            var method = CreatePrivateMethod(node, semanticModel, methodSymbol);  //anadiendo funcion privada con el codigo de la funcion decorada
            modifiedClass = modifiedClass.AddMembers(method);

            #endregion

            #region generating static delegate which value is decorated function
            //Creando delegate estatico con la funcion decorada 
            var field = CreateStaticFieldDelegate(node, decoEnumerable, methodSymbol,semanticModel);
            modifiedClass = modifiedClass.AddMembers(field);

            #endregion

            #region Creating delegate if its necessary

            if (hasParamsModifiers)
                modifiedClass = modifiedClass.AddMembers(CreateDelegateConstr(node,methodSymbol));

            #endregion

            #region Doing code substitution inside to decorated original function body

            //Sustituyendo el codigo de la funcion a decorar (return staticDelegateDecorated(param1, ... , paramN))
            modifiedClass = modifiedClass.ReplaceNode(modifiedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(n=> n.ToFullString() == node.ToFullString()).First(), ChangingToDecoratedCode(node,methodSymbol));

            #endregion


            root = root.ReplaceNode(originalclass, modifiedClass);
            root = AddUsingsWithoutRepetition(root, usingsDirectives, node.Ancestors().OfType<NamespaceDeclarationSyntax>().First().Name.WithoutTrivia().ToString());
            return root;
        }

        

        #endregion

        #region functions that change original code



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

            var invocacion = MakingInvocationExpresionForToDecorated(node, methodSymbol);

            StatementSyntax firststatement;
            if (methodSymbol.ReturnsVoid)
            {
                firststatement = SyntaxFactory.ExpressionStatement(invocacion);
            }
            else
            {
                var temp1 = SyntaxFactory.ReturnStatement(invocacion);
                firststatement = temp1.WithReturnKeyword(temp1.ReturnKeyword.WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
            }
            firststatement = firststatement.WithLeadingTrivia(node.Body.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia("    "))).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia("\n"));
            SyntaxList<StatementSyntax> stmt = new SyntaxList<StatementSyntax>(firststatement);
            return node.WithBody(node.Body.WithStatements(stmt));
        }

        //crea un delegado estatico dentro de una clase o sin ella en dependencia de si es gnerica en algun tipo la funcion decorada
        private MemberDeclarationSyntax CreateStaticFieldDelegate(MethodDeclarationSyntax node, IEnumerable<AttributeSyntax> decoratorsAttrs, IMethodSymbol methodSymbol, SemanticModel model)
        {
            var funcDelegate = CreateStaticDelegateDecorated(node, decoratorsAttrs, methodSymbol, model);

            //aqui tambien es necesario por si pasan lambda en los parametros del decorador que utilizan DynamicResult y DynamicCollection
            int cantArgs = (methodSymbol.IsStatic)?node.ParameterList.Parameters.Count: node.ParameterList.Parameters.Count+1;   //si es estatico tiene un parametro mas
            if (funcDelegate.DescendantTokens().OfType<SyntaxToken>().Where(n => n.Kind() == SyntaxKind.IdentifierToken && n.Text == (paramClassGenerated + cantArgs.ToString())).Any())
            {
                funcDelegate = funcDelegate.WithAdditionalAnnotations(new SyntaxAnnotation("using"));
                if (!this.classesToGen.Contains(cantArgs))  //guardando las clases que necesito generar
                    classesToGen.Add(cantArgs);
            }

            if (SyntaxTools.HasGenericTypes(node))   //para el caso donde hay tipos genericos   (static class ****PrivateClass {delegate})
            {
                var classDeclaration = SyntaxFactory.ClassDeclaration(SyntaxFactory.Identifier(SyntaxTools.GetStaticClassPrivateName(node.Identifier.Text)).WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" ")));
                classDeclaration = classDeclaration.WithConstraintClauses(node.ConstraintClauses).WithTypeParameterList(node.TypeParameterList);
                classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" ")));
                return classDeclaration.AddMembers(funcDelegate).WithTriviaFrom(node);
            }
            return funcDelegate;
        }

        //crea un delegado estatico que guarda la funcion decorada
        private FieldDeclarationSyntax CreateStaticDelegateDecorated(MethodDeclarationSyntax node, IEnumerable<AttributeSyntax> decoratorsAttrs, IMethodSymbol methodSymbol, SemanticModel model)
        {
            //tiene en cuenta si hay parametros ref u out
            TypeSyntax fun = (SyntaxTools.HasParamsModifiers(methodSymbol))? SyntaxTools.MakingDelegateName(node,methodSymbol) : CreateFuncGeneric(node, methodSymbol);

            ExpressionSyntax inv = null;
            foreach (var item in decoratorsAttrs.Reverse())
            {
                var decorator = LookingForDecorator(this.checker.ExtractDecoratorFullNameFromAttr(item, model));
                if (inv == null)  //si es el primero entonces recibe como parametro la funcion privada generada
                {
                    string namePrivateFunc = SyntaxTools.GetFuncPrivateName(node.Identifier.Text);
                    var exp = (SyntaxTools.HasGenericTypes(node)) ? (ExpressionSyntax)SyntaxFactory.GenericName(SyntaxFactory.Identifier(namePrivateFunc),SyntaxTools.MakeArgsFromParams(node.TypeParameterList)): SyntaxFactory.IdentifierName(namePrivateFunc) ;
                    inv = decorator.CreateInvocationToDecorator(node, methodSymbol, exp , item);    // creando la invocacion al decorador
                }
                else inv = decorator.CreateInvocationToDecorator(node, methodSymbol, inv, item); 
            }

            //__fibDecorator(__FibPrivate)
            var varInitialization = SyntaxFactory.EqualsValueClause(inv);

            //__FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(SyntaxTools.GetStaticDelegatePrivateName(node.Identifier.Text))).WithInitializer(varInitialization);

            //func<int,int,int> = __FibDecorated = __fibMemoize(__FibPrivate)
            var varDeclaration = SyntaxFactory.VariableDeclaration(fun).AddVariables(varDeclarator);

            //anadiendo public y static
            return SyntaxFactory.FieldDeclaration(varDeclaration).AddModifiers(SyntaxFactory.Token(node.GetLeadingTrivia(), (methodSymbol.IsGenericMethod)? SyntaxKind.PublicKeyword : SyntaxKind.PrivateKeyword, SyntaxFactory.ParseTrailingTrivia(" ")), SyntaxFactory.Token(SyntaxFactory.ParseLeadingTrivia(""), SyntaxKind.StaticKeyword, SyntaxFactory.ParseTrailingTrivia(" "))).WithTriviaFrom(node);

        }

        //static delegate TResult __methodDelegateConstr(...)
        private DelegateDeclarationSyntax CreateDelegateConstr(MethodDeclarationSyntax node, IMethodSymbol methodSymbol)
        {
            var deleg = SyntaxFactory.DelegateDeclaration((methodSymbol.ReturnsVoid) ? SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)).WithTriviaFrom(node.ReturnType) : node.ReturnType.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" ")), SyntaxTools.GetDelegateConstrName(node.Identifier.Text));
            return deleg.WithDelegateKeyword(SyntaxFactory.Token(SyntaxKind.DelegateKeyword).WithTriviaFrom(deleg.DelegateKeyword).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia(" "))).WithParameterList(SyntaxTools.MakeNewParametersList(node,methodSymbol,"instance")).WithTypeParameterList(node.TypeParameterList).WithConstraintClauses(node.ConstraintClauses).WithTriviaFrom(node);
        }


        #endregion

        #region ClassGeneratorsMethods

        //genera todas las clases para los parametros de las funciones decoradas necesarias
        private Project GenerateAllNeededClasses(Project currentProject, string outputRealPathModifiedFiles)
        {
            foreach (var cantParams in this.classesToGen)
            {
                string code = GenerateClass(cantParams, IOUtilities.BasePath(currentProject.FilePath) + "\\" + outputRealPathModifiedFiles);
                currentProject = currentProject.AddDocument($"ParamsGenerics{cantParams}.cs", code).Project;
            }
            return currentProject;
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
        private IDecorator LookingForDecorator(string nameDecorator, AttributeSyntax decoratorAttr = null)
        {
            try
            {
                return decorators.Where(n => n.Identifier == nameDecorator).First();
            }
            catch (Exception)   //si no existe el decorador
            {
                if (decoratorAttr!=null)
                {
                    var location = decoratorAttr.GetLocation();
                    this.log.AddError(location.SourceTree.FilePath,location.GetLineSpan().StartLinePosition.Line, $"Decorator {nameDecorator} don't exist", Severity.Error);
                }
                return null;
            }
        }

        private InvocationExpressionSyntax MakingInvocationExpresionForToDecorated(MethodDeclarationSyntax node, IMethodSymbol methodSymbol)
        {
            //Construyendo instruccion return decorador
            var argumentos = SyntaxFactory.ArgumentList();

            if (!methodSymbol.IsStatic)  //cuando es de instancia hay que anadir this como parametro
            {
                argumentos = argumentos.AddArguments(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
            }

            foreach (var item in node.ParameterList.Parameters)
            {
                var arg = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(item.Identifier.Text));

                var mod = item.Modifiers;   //para anadir ref o out en caso de que se necesite
                if (mod.Count != 0)
                    arg = arg.WithRefKindKeyword(mod.First());

                argumentos = argumentos.AddArguments(arg);
            }

            ExpressionSyntax expr = SyntaxFactory.IdentifierName(SyntaxTools.GetStaticDelegatePrivateName(node.Identifier.Text));
            if (SyntaxTools.HasGenericTypes(node))   //si es generica entonces tengo hacer classgenerated<T1>.delegate(bla, bla2)
            {
                var genericExpr = SyntaxFactory.GenericName(SyntaxFactory.Identifier(SyntaxTools.GetStaticClassPrivateName(node.Identifier.Text)), SyntaxTools.MakeArgsFromParams(node.TypeParameterList));
                expr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,genericExpr ,expr as SimpleNameSyntax);
            }
            return SyntaxFactory.InvocationExpression(expr, argumentos);
        }

        //Limpia directorio de salida
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

        //Add using directive with generated classes namespace
        private SyntaxNode AddUsing(SyntaxNode root)
        {
            var using1 = BuildUsingStatement(this.namespaceClassesGenerated);
            SyntaxNode[] temp = { using1 };
            root = root.InsertNodesBefore(root.ChildNodes().First(), temp);
            return root;
        }

        //añade los using de los cs de los decoradores sin repetir los que ya estan
        private SyntaxNode AddUsingsWithoutRepetition(SyntaxNode root, List<UsingDirectiveSyntax> references, string currentNamespace)
        {
            var currentRootUsings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            root = root.InsertNodesBefore(root.ChildNodes().First(), references.Where(n => !SyntaxTools.BelongTo(currentRootUsings, n) && n.Name.WithoutTrivia().GetText().ToString() != currentNamespace));
            return root;
        }

        //construye using namespaceName;
        private UsingDirectiveSyntax BuildUsingStatement(string namespaceName)
        {
            return SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(namespaceName).WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(" "))).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\r\n"));
        }

        private ClassDeclarationSyntax GetOriginalClass(MethodDeclarationSyntax method, SyntaxNode currentRoot)
        {
            ClassDeclarationSyntax originalclass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();    //esta es la clase original del project
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

        //Func<int,int,int>
        private GenericNameSyntax CreateFuncGeneric(MethodDeclarationSyntax node, IMethodSymbol methodSymbol)
        {
            var argumentList = SyntaxFactory.TypeArgumentList();

            if (!methodSymbol.IsStatic)
                argumentList = argumentList.AddArguments(SyntaxTools.GetTargetType(node,methodSymbol));

            foreach (var item in node.ParameterList.Parameters)
            {
                argumentList = argumentList.AddArguments(item.Type);
            }

            //si es void añade bool porque el metodo privado generado sera bool
            argumentList = argumentList.AddArguments((methodSymbol.ReturnsVoid) ? SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)).WithTriviaFrom(node.ReturnType) : node.ReturnType);

            //func<int,int,int>
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier("Func"), argumentList);
        }

        //anade los using de los cs de los decoradores al cs donde son aplicados
        private void AddDecoUsingToList(List<UsingDirectiveSyntax> usingsDirectives, IDecorator decoratorMethod)
        {
            foreach (var item in decoratorMethod.GetUsingNamespaces())
            {
                if (!SyntaxTools.BelongTo(usingsDirectives, item))
                    usingsDirectives.Add(item);
            }
            var decoCurrentNamespace = BuildUsingStatement(decoratorMethod.CurrentNamespaces);
            if (!SyntaxTools.BelongTo(usingsDirectives, decoCurrentNamespace))
                usingsDirectives.Add(decoCurrentNamespace);
        }

        //actualiza la lista de clases ParamGenerics a generar
        private void UpdateToGenerateClasses(MemberDeclarationSyntax memberToAdd)
        {
            if (memberToAdd.GetAnnotations("using").Any())
            {
                foreach (var item in memberToAdd.GetAnnotations("using"))
                {
                    int cantArgs = int.Parse(item.Data);

                    if (!this.classesToGen.Contains(cantArgs))  //guardando las clases que necesito generar
                        this.classesToGen.Add(cantArgs);
                }
            }
        }

        #endregion

    }
}
