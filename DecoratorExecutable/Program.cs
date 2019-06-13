using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Decorators.CodeInjections;
using Microsoft.CodeAnalysis.MSBuild;
using Decorators.Utilities.ErrorLogger;
using System.Diagnostics;


namespace DecoratorExecutable
{
    class Program
    {
        static Stopwatch a;
        static void Main(string[] args)
        {
            a = new Stopwatch();
            a.Start();

            #region Otros proyectos
            //GenerateCodeFromProject(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Tesis Projects\19-3-4 Funciones Decoradoras\Probador\ProbadorFuncDecorator\ProbadorFuncDecorator\ProbadorFuncDecorator.csproj").Wait();

            //GenerateCodeFromProject(@"C:\Users\Laptop\Desktop\AcmeRentalCar(Roslyn)\AcmeRentalCAr\AcmeRentalCAr.csproj").Wait();

            //GenerateCodeFromProject(@"C:\Users\Laptop\Desktop\Roslyn-AOP\AOPExamples-master\AOPTutorial\AOPTutorial.csproj").Wait();

            //GenerateCodeFromProject(@"C:\Users\Laptop\Desktop\Roslyn-AOP\d1\d1.csproj").Wait();

            //GenerateCodeFromProject(@"C:\Users\Laptop\Desktop\Roslyn-AOP\PostSharp.Samples-master\Framework\PostSharp.Samples.Authorization\PostSharp.Samples.Authorization.csproj").Wait();

            //GenerateCodeFromProject(@"C:\Users\Laptop\Desktop\Roslyn-AOP\GeoWallE(PostSharp)\GSharpInterpreter\GSharpInterpreter.csproj").Wait();

            #endregion


            GenerateCodeFromProject(@"C:\Datos\Trabajando en la tesis\II Semestre\Tesis\Escrito\Expo\Predefensa FancyDeco\Predefensa FancyDeco\Predefensa FancyDeco.csproj").Wait();


            a.Stop();
            Console.WriteLine(a.ElapsedMilliseconds);

        }

        #region Funcion que se utiliza para decorar un project

        public static async Task GenerateCodeFromProject(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(path);
            var errors = new ErrorLog();

            var decorator = new DecoratedCompilation(errors);
            var newProject = decorator.DecoratingProject(project);

            foreach (var diag in errors.GetDiagnostics())
            {
                Console.WriteLine(diag);
                Console.WriteLine();
            }

            Console.WriteLine("Done!!!!");
        }
        #endregion



    }
}
