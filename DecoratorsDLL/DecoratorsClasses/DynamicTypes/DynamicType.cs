using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL.DecoratorsClasses.DynamicTypes
{
    /// <summary>
    /// Tipo base para los parametros y return de los decoradores
    /// </summary>
    public class DynamicType
    {
        dynamic value;
        protected DynamicType(dynamic value)
        {
            this.value = value;
        }

        public dynamic Value { get { return value; } set { this.value = value; } }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value.ToString();
        }
       
    }
}
