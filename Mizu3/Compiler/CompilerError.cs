// -----------------------------------------------------------------------
// <copyright file="CompilerError.cs" company="">
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
    public class CompilerError
    {
        public string Message { get; set; }
        public string Filename { get; set; }
        public int Line { get; set; }
        public int Col { get; set; }
    }
}
