using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecoratorsDLL.DecoratorsClasses.DynamicTypes
{
    /// <summary>
    /// Tipo utilizado para representar los parametros de las funciones decoradas. Es sustituido por el tipo especifico del parametro una vez decorada una funcion.
    /// 
    /// En caso de ser utilizado un array de DynamicParams entonces el array sera realmente creado de el LCA de todos los parametros de la funcion de la funcion decorada
    /// DynamicParams[] en los parametros de una funcion sera sustituido por los tipos de los parametros especificos de la funcion a la que decora.
    /// </summary>
    public class DynamicParam:DynamicType
    {
        public DynamicParam(object value) : base(value) { }

        public static DynamicParam operator +(DynamicParam left, DynamicParam right)
        {
            return new DynamicParam(left.Value + right.Value);
        }

        public static DynamicParam operator -(DynamicParam left, DynamicParam right)
        {
            return new DynamicParam(left.Value + right.Value);
        }

        public static DynamicParam operator /(DynamicParam left, DynamicParam right)
        {
            return new DynamicParam(left.Value + right.Value);
        }

        public static DynamicParam operator *(DynamicParam left, DynamicParam right)
        {
            return new DynamicParam(left.Value + right.Value);
        }

        public static bool operator ==(DynamicParam left, DynamicParam right)
        {
            try { return left.Value == right.Value; }
            catch (Exception)
            {
                throw new Exception($"== is not defined between type {left.Value.GetType()} and {right.Value.GetType()}");
            }

        }
        public static bool operator !=(DynamicParam left, DynamicParam right)
        {
            try { return left.Value != right.Value; }
            catch (Exception)
            {
                throw new Exception($"!= is not defined between type {left.Value.GetType()} and {right.Value.GetType()}");
            }

        }

        //definir los comparadores

        public static DynamicParam operator ++(DynamicParam left)
        {
            left.Value += 1;
            return left;
        }
        public static DynamicParam operator --(DynamicParam left)
        {
            left.Value -= 1;
            return left;
        }
    }
}
