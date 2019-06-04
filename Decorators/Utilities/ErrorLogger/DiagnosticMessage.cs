using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities.ErrorLogger
{
    
    public class DiagnosticMessage : IDiagnostic
    {
        public DiagnosticMessage(string filePath , int linePosition, string message, Severity severity)
        {
            this.Severity = severity;
            this.Message = message;
            this.LinePosition = linePosition;
            this.FilePath = filePath;
        }
        public Severity Severity { get; set; }
        public string Message { get; set; }

        public int LinePosition { get; set; }

        public string FilePath { get; set; }

        public override string ToString()
        {
            return $"Error:  File: {this.FilePath} ,  Line {this.LinePosition}  -->  {this.Message}";
        }

    }
}
