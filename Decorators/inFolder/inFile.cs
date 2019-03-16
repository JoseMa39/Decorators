using Decorators;
using Decorators.DynamicTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InFile
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

        static int args()
        { return 1;}
        static Func<DynamicParam[], DynamicResult> Memoize(Func<DynamicParam[], DynamicResult> d)
        {
            Func<DynamicParam[], DynamicResult> f;
            Dictionary<DynamicParam[], DynamicResult> memory = new Dictionary<DynamicParam[], DynamicResult>(new ObjectArrayEqualityComparer());

            return (args) =>
            {
                DynamicParam b1 = new DynamicParam("");
                DynamicParam c3 = new DynamicParam(9);
                DynamicParam a4,a5;

                a4= new DynamicParam(9);

                int i = 1;

                var a = args[0];
                var b = args.Length;
                var c = args[i];

                DynamicResult res;
                if (memory.TryGetValue(args, out res))
                {
                    Console.WriteLine($"Using memoized value for {args[0]}: {res}");
                    return res;
                }
                var t = args;
                res = d(t);
                memory[args] = res;
                return res;
            };
        }

    }
}