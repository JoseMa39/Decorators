using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities.ErrorLogger
{
    
    public class Diagnostic:IDiagnostic
    {
        public Diagnostic(int linePosition, string message, Severity severity)
        {
            this.Severity = severity;
            this.Message = message;
            this.LinePosition = linePosition;
        }
        public Severity Severity { get; set; }
        public string Message { get; set; }

        public int LinePosition { get; set; }

    }
}
