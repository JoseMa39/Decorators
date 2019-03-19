using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decorators.DecoratorsClasses.ClassesToCreate
{
    public class ParamsGenerics2<T1, T2> : IEnumerable<object>
    {
        T1 item1;
        T2 item2;

        public ParamsGenerics2(T1 item1,T2 item2)
        {
            this.item1 = item1;
            this.item2 = item2;
        }

        public T1 Item1 { get => item1; set => item1 = value; }
        public T2 Item2 { get => item2; set => item2 = value; }

        public IEnumerator<object> GetEnumerator()
        {
            return new Params2Enumerator<T1,T2>(this);
        }

        public object this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return Item1;
                    case 1:
                        return Item2;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        Item1 = (T1)value;
                        break;
                    case 1:
                        Item2 = (T2)value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public (T1,T2) ToTuple()
        {
            return (Item1, Item2);
        }

        public override int GetHashCode()
        {
            return this.ToTuple().GetHashCode();
        }
        public override string ToString()
        {
            return this.ToTuple().ToString();
        }
    }

    class Params2Enumerator<T1,T2> : IEnumerator<object>
    {
        object current;
        bool hasMoveNext;
        int pos;
        ParamsGenerics2<T1, T2> e;
        public Params2Enumerator(ParamsGenerics2<T1, T2> e)
        {
            this.e = e;
            pos = 0;
            hasMoveNext = false;
        }
        public object Current
        { get
            {
                if (hasMoveNext)
                    return current;
                throw new Exception();
            }
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            switch (pos)
            {
                case 0:
                    current = e.Item1;
                    break;
                case 1:
                    current = e.Item2;
                    break;
                default:
                    return hasMoveNext = false;
            }
            pos++;
            return hasMoveNext = true;
        }

        public void Reset()
        {
        }
    }
}
