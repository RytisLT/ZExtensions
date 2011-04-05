using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZExtensions
{
    public class Tuple<T1, T2>
    {
        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }

        public Tuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        public static Tuple<T1, T2> Create(T1 item1, T2 item2)
        {
            var result = new Tuple<T1, T2>(item1, item2);            
            return result;
        }
    }
}
