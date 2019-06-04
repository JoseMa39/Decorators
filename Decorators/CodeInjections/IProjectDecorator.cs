using Decorators.DecoratorsCollector.IsDecoratorChecker;
using Decorators.Utilities.ErrorLogger;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.CodeInjections
{
    internal interface IProjectDecorator
    {
        Project DecoratingProject(Project project);
    }
}
