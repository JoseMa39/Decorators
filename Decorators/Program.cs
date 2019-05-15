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
using DecoratorsDLL.DecoratorsClasses;
using Decorators.Utilities.ErrorLogger;
using Decorators.DecoratorsCollector.IsDecoratorChecker;
using System.Diagnostics;

namespace Decorators
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch a = new Stopwatch();
            a.Start();
            GenerateCodeFromProject(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator\ProbadorFuncDecorator.csproj").Wait();
            a.Stop();
            Console.WriteLine( a.ElapsedMilliseconds);
            
            //CompileSolution(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator.sln", "..\\..\\outFolder");
            //GenerateCode();
           // GenerateCode(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator.sln").Wait();
        }

        #region Funcion que se utiliza para decorar un project

        public static async Task GenerateCodeFromProject(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(path);
            var decorator = new DecoratedCompilation();

            var errors = new ErrorLog();
            var newProject = await decorator.DecoratingProjectAsync(project,new CheckIsDecorator(),"outFolder", errors);

            var compilation = await newProject.GetCompilationAsync();

            try
            {
                foreach (var item in errors.GetDiagnostics().Where(diag => diag.Severity == Severity.Error))
                {
                    Console.WriteLine($"Error:  File: {item.FilePath} ,  Line {item.LinePosition}  -->  {item.Message}");
                }

            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("No errors");
            }

            Console.WriteLine("Done!!!!");
        }
        #endregion

       

       
    }
}
