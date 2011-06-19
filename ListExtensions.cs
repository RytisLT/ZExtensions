using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZExtensions
{
    public static class ListExtensions
    {
        public static void SafeAdd(this IList list, object value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        public static void SafeAdd<T>(this IList<T> list, T value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        public static void SafeRemove(this IList list, object value)
        {
            if (list.Contains(value))
            {
                list.Remove(value);
            }
        }

        public static void SafeRemove<T>(this IList<T> list, T value)
        {
            if (list.Contains(value))
            {
                list.Remove(value);
            }
        }
    }
}
