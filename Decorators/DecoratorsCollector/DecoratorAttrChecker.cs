using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector
{
    class DecoratorAttrChecker : IDecorateAttrChecker
    {
        public bool IsDecorateAttr(AttributeSyntax attr)
        {
            return attr.Name.ToString() == "DecorateWith";
        }
    }
}
