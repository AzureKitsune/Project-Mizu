// -----------------------------------------------------------------------
// <copyright file="LocalBuilderEx.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Reflection.Emit;

    /// <summary>
    /// Extended property for holding extra information.
    /// </summary>
    public class LocalBuilderEx
    {
        public LocalBuilder Base { get; set; }
        public string Name { get; set; }
    }
}
