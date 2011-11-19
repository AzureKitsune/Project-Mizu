// -----------------------------------------------------------------------
// <copyright file="CompileOperationResult.cs" company="">
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
    public class CompileOperationResult
    {
        public bool Successful { get; set; }
        public string OutputFilename { get; set; }
        public CompilerError[] Errors { get; set; }
    }
}
