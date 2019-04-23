using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL.DecoratorsClasses.DynamicTypes
{
    /// <summary>
    /// Tipo utilizado para representar el tipo de retorno de las funciones decoradas. Es sustituido por el tipo especifico de retorno una vez decorada una funcion.
    /// </summary>
    public class DynamicResult:DynamicType
    {
        public DynamicResult(object value) : base(value) { }

        public override bool Equals(object obj)
        {
            if (!(obj is DynamicResult other))
            {
                return false;
            }

            return Value.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static DynamicResult operator +(DynamicResult left, DynamicResult right)
        {
            return new DynamicResult(left.Value + right.Value);
        }

        public static DynamicResult operator -(DynamicResult left, DynamicResult right)
        {
            return new DynamicResult(left.Value + right.Value);
        }

        public static DynamicResult operator /(DynamicResult left, DynamicResult right)
        {
            return new DynamicResult(left.Value + right.Value);
        }

        public static DynamicResult operator *(DynamicResult left, DynamicResult right)
        {
            return new DynamicResult(left.Value + right.Value);
        }

        public static bool operator ==(DynamicResult left, DynamicResult right)
        {
            try { return left.Value == right.Value; }
            catch (Exception)
            {
                throw new Exception($"== is not defined between type {left.Value.GetType()} and {right.Value.GetType()}");
            }

        }
        public static bool operator !=(DynamicResult left, DynamicResult right)
        {
            try { return left.Value != right.Value; }
            catch (Exception)
            {
                throw new Exception($"!= is not defined between type {left.Value.GetType()} and {right.Value.GetType()}");
            }

        }

        //definir los comparadores

        public static DynamicResult operator ++(DynamicResult left)
        {
            left.Value += 1;
            return left;
        }
        public static DynamicResult operator --(DynamicResult left)
        {
            left.Value -= 1;
            return left;
        }

    }
}
