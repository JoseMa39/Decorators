using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DecorateWithAttribute:Attribute
    {
        private string method_name;
        public DecorateWithAttribute(string method)
        {
            method_name = method;
        }
    }
}
