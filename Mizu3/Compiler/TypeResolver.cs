// -----------------------------------------------------------------------
// <copyright file="TypeResolver.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class TypeResolver
    {
        public static bool IsValueType(Type typ)
        {
            return typ.BaseType == typeof(ValueType);
        }
        public static bool IsNamespaceAvailable(string ns)
        {
            // -> http://stackoverflow.com/questions/2606322/c-how-to-check-if-string-is-a-namespace
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetTypes().Any(type => type.Namespace == ns))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
