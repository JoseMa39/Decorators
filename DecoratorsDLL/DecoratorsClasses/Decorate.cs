using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DecorateWithAttribute:Attribute
    {
        private string method_name;
        public DecorateWithAttribute(string method)
        {
            method_name = method;
        }
    }
}
