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
        public LocalType Type { get; set; }
        public Label LoopLabel { get; set; }
        public int LoopLow { get; set; }
        public int LoopHigh { get; set; }
        public Func<bool> LoopAction { get; set; }
        public LoopDirectionEnum LoopDirection { get; set; }
        public Type VariableType { get; set; }
    }
    public enum LocalType
    {
        Var = 0,
        LoopVar = 1,
    }
    public enum LoopDirectionEnum
    {
        Up = 0,
        Down = 1,
    }
}
