using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsCollector.DecoratorClass
{
    internal interface IDecorator
    {
        TypeDecorator Type { get; set; }
    }
}
