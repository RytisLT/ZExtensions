using System;
using System.Linq.Expressions;

namespace ZExtensions
{
    public static class LambdaHelper
    {
        /// <summary>
        /// Get name of passed lambda variable.
        /// Example GetParameterName(() => string.Empty.Length) will return "Length"
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expr"></param>
        /// <returns></returns>
        public static string GetParameterName<T>(Expression<Func<T>>  expr)
        {
            var memberExpression = (MemberExpression) expr.Body;
            return memberExpression.Member.Name;
        }
    }
}
