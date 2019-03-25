using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Decorators.CodeInjections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.MSBuild;
using System.Runtime;
using Microsoft.CodeAnalysis.Formatting;
using Decorators.CodeInjections.ClassesToCreate;
using Decorators.DecoratorsCollector;
using DecoratorsDLL;
using DecoratorsDLL.DecoratorsClasses.DynamicTypes;

namespace Decorators
{
    class Program
    {
        static void Main(string[] args)
        {
            GenerateCodeFromProject(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator\ProbadorFuncDecorator.csproj").Wait();
            //CompileSolution(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator.sln", "..\\..\\outFolder");
            //GenerateCode();
           // GenerateCode(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator.sln").Wait();
        }

        #region Funcion que se utiliza para decorar un project

        public static async Task GenerateCodeFromProject(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(path);
            var decorator = new DecoratedCompilation(project);

            var newProject = await decorator.DecoratingProjectAsync("outFolder");

            var compilation = await newProject.GetCompilationAsync();

            try
            {
                foreach (var item in compilation.GetDiagnostics().Where(diag => diag.Severity == DiagnosticSeverity.Error).Select(diag => diag.Location))
                {
                    Console.WriteLine(item);

                }

                Console.WriteLine($"Diagnostics: {compilation.GetDiagnostics().Count()}" + $"\n {compilation.GetDiagnostics().Where(diag => diag.Severity == DiagnosticSeverity.Error).Select(diag => diag.GetMessage()).Aggregate((a, b) => $"{a}\n{b}")}");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("No errors");
            }

            Console.WriteLine("Done!!!!");
        }
        #endregion

        
        #region Otros generateCode() usados para probar
        //mio para probar
        private static void GenerateCode()
        {
            var code = new StreamReader("..\\..\\inFolder\\inFileParamsCollection.cs").ReadToEnd();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

            var root = (CompilationUnitSyntax)tree.GetRoot();

            //var collector = new Metodos_decorados();
            //collector.Visit(root);

            MetadataReference[] references =
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                        .Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DecorateWithAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Func<int, int>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Dictionary<object[], dynamic>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DynamicParam).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DynamicResult).Assembly.Location),
                };

            var compilation = CSharpCompilation.Create(
                assemblyName: "HelloWorld",
                syntaxTrees: new[] { tree },
                references: references);

            DecoratedCompilation rewriter2 = new DecoratedCompilation(compilation);

            var newSource = rewriter2.Decorating();
            var newSourceOutput = new StreamWriter("..\\..\\outFolder\\outFile.cs");

            foreach (var syntaxTree in newSource.SyntaxTrees)
            {
                newSourceOutput.Write(syntaxTree.GetRoot().ToFullString());
            }

            newSourceOutput.Flush();
            newSourceOutput.Close();
            
        }


        //muy parecido al final que utilizare
        private async static Task GenerateCode(Project project)
        {
            Directory.CreateDirectory(project.FilePath + "\\outFolder");

            var compilation = await project.GetCompilationAsync();

            DecoratedCompilation rewriter2 = new DecoratedCompilation(compilation);
            var newCompilation = rewriter2.Decorating();

            foreach (var currentSyntaxTree in newCompilation.SyntaxTrees)
            {
                Console.WriteLine($"Diagnostics: {compilation.GetDiagnostics().Count()}" + $"\n {compilation.GetDiagnostics().Select(diag => diag.GetMessage()).Aggregate((a, b) => $"{a}\n{b}")}");
                
                var newSourceOutput = new StreamWriter($"..\\..\\outFolder\\{currentSyntaxTree.FilePath.Split('\\').Last()}");
                Console.WriteLine(currentSyntaxTree.GetRoot().ToFullString());
                newSourceOutput.Write(currentSyntaxTree.GetRoot().ToFullString());
                newSourceOutput.Flush();
                newSourceOutput.Close();

            }
        }
        #endregion


       
    }
}
