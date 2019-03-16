using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Decorators.CodeInjections;
using Decorators.DynamicTypes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.MSBuild;
using System.Runtime;
using Microsoft.CodeAnalysis.Formatting;

namespace Decorators
{
    class Program
    {
        static void Main(string[] args)
        {
            //CompileSolution(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator.sln", "..\\..\\outFolder");
            GenerateCode();
            //GenerateCode(@"C:\Datos\ProbadorFuncDecorator\ProbadorFuncDecorator.sln").Wait();
        }
        
        private async static Task GenerateCode(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            var project2 = await workspace.OpenProjectAsync(@"C:\Datos\ProbadorFuncDecorator\ProbadorFuncDecorator\ProbadorFuncDecorator.csproj");


            var solution = await workspace.OpenSolutionAsync(path);

            foreach (var projectId in solution.ProjectIds)
            {
                var project = solution.GetProject(projectId);

                var compilation = await project.GetCompilationAsync();


                Console.WriteLine(project.Name);
                MakingDecoratedCompilation rewriter2 = new MakingDecoratedCompilation(compilation);
               
                var newCompilation = rewriter2.Decorating();

                foreach (var currentSyntaxtree in newCompilation.SyntaxTrees)
                {
                    var newSourceOutput = new StreamWriter("..\\..\\outFolder\\outFile.cs");
                    Console.WriteLine(currentSyntaxtree.GetRoot().ToFullString());
                    newSourceOutput.Write(currentSyntaxtree.GetRoot().ToFullString());

                    newSourceOutput.Flush();
                    newSourceOutput.Close();
                }

            }
        }

        private static void GenerateCode()
        {
            var code = new StreamReader("..\\..\\inFolder\\inFile.cs").ReadToEnd();
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

           
                MakingDecoratedCompilation rewriter2 = new MakingDecoratedCompilation(compilation);

                var newSource = rewriter2.Decorating();

                var newSourceOutput = new StreamWriter("..\\..\\outFolder\\outFile.cs");

            foreach (var syntaxTree in newSource.SyntaxTrees)
            {
                newSourceOutput.Write(syntaxTree.GetRoot().ToFullString());
            }

                newSourceOutput.Flush();
                newSourceOutput.Close();
              
        }



        private static bool CompileSolution(string solutionUrl, string outputDir)
        {
            bool success = true;

            MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            Solution solution = workspace.OpenSolutionAsync(solutionUrl).Result;
            ProjectDependencyGraph projectGraph = solution.GetProjectDependencyGraph();
            Dictionary<string, Stream> assemblies = new Dictionary<string, Stream>();

            foreach (ProjectId projectId in projectGraph.GetTopologicallySortedProjects())
            {
                Compilation projectCompilation = solution.GetProject(projectId).GetCompilationAsync().Result;
                if (null != projectCompilation && !string.IsNullOrEmpty(projectCompilation.AssemblyName))
                {
                    using (var stream = new MemoryStream())
                    {
                        
                        EmitResult result = projectCompilation.Emit(stream);
                        if (result.Success)
                        {
                            string fileName = string.Format("{0}.dll", projectCompilation.AssemblyName);

                            using (FileStream file = File.Create(outputDir + '\\' + fileName))
                            {
                                stream.Seek(0, SeekOrigin.Begin);
                                stream.CopyTo(file);
                            }
                        }
                        else
                        {
                            success = false;
                        }
                    }
                }
                else
                {
                    success = false;
                }
            }

            return success;
        }
    }
}
