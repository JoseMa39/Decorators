using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities
{
    public static class IOUtilities
    {
        public static string BasePath(string filePath)
        {
            var path = filePath.Split('\\');
            string result = path[0];
            for (int i = 1; i < path.Length - 1; i++)
                result += $"\\{path[i]}";
            return result;
        }

        public static void WriteSyntaxTreeInFile(string path, SyntaxTree syntaxTree)
        {
            var newSourceOutput = new StreamWriter(path);
            newSourceOutput.Write(syntaxTree.GetRoot().ToFullString());
            newSourceOutput.Flush();
            newSourceOutput.Close();
        }
    }
}
