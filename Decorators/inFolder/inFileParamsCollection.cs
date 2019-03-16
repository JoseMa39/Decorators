using Decorators;
using Decorators.DecoratorsClasses.DynamicParamsCollection;
using Decorators.DynamicTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InFile2
{
    
    class ObjectArrayEqualityComparer : IEqualityComparer<object[]>
    {
        public bool Equals(object[] x, object[] y)
        {
            if (x.Length != y.Length)
                return false;

            var xEnumerator = x.GetEnumerator();
            var yEnumerator = y.GetEnumerator();

            while (xEnumerator.MoveNext() && yEnumerator.MoveNext())
            {
                if (!xEnumerator.Current.Equals(yEnumerator.Current))
                    return false;
            }

            return true;
        }

        public int GetHashCode(object[] obj)
        {
            int hash = 0;

            for (int i = 0; i < obj.Length; i++)
            {
                hash += obj[i].GetHashCode() * 23 ^ i;
            }

            return hash;
        }
    }

    class Program
    {
        static void Main2(string[] args)
        {
            Console.WriteLine(Fib(10,11));
            Console.ReadLine();
        }


        [DecorateWith(nameof(Memoize))]
        public static int Fib(int n, int t) { return n == 0 || n == 1 ? 1 : Fib(n - 1,10) + Fib(n - 2,10); }

        
        static Func<DynamicParamsCollection, DynamicResult> Memoize(Func<DynamicParamsCollection, DynamicResult> d)
        {
            Dictionary<DynamicParamsCollection, DynamicResult> memory = new Dictionary<DynamicParamsCollection, DynamicResult>();

            return (args) =>
            {
                DynamicResult res;
                if (memory.TryGetValue(args, out res))
                {
                    Console.WriteLine($"Using memoized value for {args[0]}: {res}");
                    return res;
                }
                memory[args] = res;
                return res;
            };
        }

    }
}