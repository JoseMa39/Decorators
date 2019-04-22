using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.Utilities.ErrorLogger
{

    public enum Severity { Warning, Error }

    interface IDiagnostic
    {
        Severity Severity { get; set; }
        string Message { get; set; }

        int LinePosition { get; set; }

    }
}
