﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities.ErrorLogger
{
    public class ErrorLog : IErrorLog
    {
        List<IDiagnostic> diagnostics;
        public ErrorLog()
        {
            diagnostics = new List<IDiagnostic>();
        }
        public ErrorLog(IEnumerable<IDiagnostic> ienumerable)
        {
            this.diagnostics = new List<IDiagnostic>(ienumerable);
        }


        public void AddError(string filePath, int line, string message, Severity severity)
        {
            this.diagnostics.Add(new DiagnosticMessage(filePath, line, message, severity));
        }

        public void AddError(IDiagnostic diag)
        {
            this.diagnostics.Add(diag);
        }

        public void AddErrors(IEnumerable<IDiagnostic> diags)
        {
            this.diagnostics.AddRange(diags);
        }

        public IEnumerable<IDiagnostic> GetDiagnostics()
        {
            return diagnostics;
        }

        public IEnumerable<IDiagnostic> GetDiagnostics(Severity severity)
        {
            return diagnostics.Where(n => n.Severity == severity);
        }
    }
}
