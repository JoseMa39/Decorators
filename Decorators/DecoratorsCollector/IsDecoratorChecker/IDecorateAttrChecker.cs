using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.IsDecoratorChecker
{
    internal interface IDecorateAttrChecker
    {
        bool IsDecorateAttr(AttributeSyntax attr);
    }
}
