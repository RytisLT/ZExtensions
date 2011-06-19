using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZExtensions
{
    public static class ListExtensions
    {
        /// <summary>
        /// Adds item to the list if items is not already added
        /// </summary>
        /// <param name="list">Owner list</param>
        /// <param name="item">Item to add</param>
        public static void SafeAdd(this IList list, object item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Adds item to the list if items is not already added
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">Owner list</param>
        /// <param name="item">Item to add</param>
        public static void SafeAdd<T>(this IList<T> list, T item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Remove item from the list, if list contains the item
        /// </summary>
        /// <param name="list">Owner list</param>
        /// <param name="item">Item to remove</param>
        public static void SafeRemove(this IList list, object item)
        {
            if (list.Contains(item))
            {
                list.Remove(item);
            }
        }

        /// <summary>
        /// Remove item from the list, if list contains the item
        /// </summary>
        /// <param name="list">Owner list</param>
        /// <param name="item">Item to remove</param>
        public static void SafeRemove<T>(this IList<T> list, T item)
        {
            if (list.Contains(item))
            {
                list.Remove(item);
            }
        }
    }
}
