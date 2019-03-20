using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL.DecoratorsClasses.DynamicParamsCollection
{
    public class DynamicParamsCollection: IGenericTuple, IEnumerable
    {
        object[]values;
        private DynamicParamsCollection()
        {
        }

        public dynamic ToTuple()
        {
            return values;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return values.GetEnumerator();
        }

        public object this[int index]
        {
            get { return values[index]; }
            set { value = values[index]; }
        }

        public int Length { get { return values.Length; } }
        


        


    }
}
