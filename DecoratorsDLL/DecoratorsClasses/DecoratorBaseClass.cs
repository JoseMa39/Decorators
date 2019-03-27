using DecoratorsDLL.DecoratorsClasses.DynamicTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL.DecoratorsClasses
{
    [AttributeUsage(AttributeTargets.Method,AllowMultiple = true)]
    public abstract class DecoratorBaseClass: Attribute
    {
        public abstract Func<DynamicParamsCollection, DynamicResult> Decorator(Func<DynamicParamsCollection, DynamicResult> fun);
    }
}
