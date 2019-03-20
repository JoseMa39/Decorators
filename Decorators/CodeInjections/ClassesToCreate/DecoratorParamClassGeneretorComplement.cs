using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.CodeInjections.ClassesToCreate
{
    public partial class DecoratorParamClassGeneretor
    {
        
        public DecoratorParamClassGeneretor(int cantParams)
        {
            this.CantParams = cantParams;
        }

        public int CantParams { get; set; }
    }
}
