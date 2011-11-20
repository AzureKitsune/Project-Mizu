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
    }
}
