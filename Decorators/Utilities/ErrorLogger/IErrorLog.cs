﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities.ErrorLogger
{
    public interface IErrorLog
    {
        void AddError(string filePath, int line, string message, Severity severity);

        void AddError(IDiagnostic diag);

        void AddErrors(IEnumerable<IDiagnostic> diag);

        IEnumerable<IDiagnostic> GetDiagnostics();

        IEnumerable<IDiagnostic> GetDiagnostics(Severity severity);

    }
}
