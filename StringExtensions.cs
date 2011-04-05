using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZExtensions
{
    public static class StringExtensions
    {
        public static bool ContainsCaseInsensitive(this string inputString, string value)
        {
            int index = inputString.IndexOf(value, StringComparison.InvariantCultureIgnoreCase);
            return index >= 0;
        }
    }
}
