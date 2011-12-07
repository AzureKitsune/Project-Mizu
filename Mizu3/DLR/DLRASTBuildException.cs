// -----------------------------------------------------------------------
// <copyright file="DLRASTSyntaxException.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class DLRASTBuildException : Exception
    {
        public DLRASTBuildException(string msg, int line = 0, int col = 0) : base(msg)
        {
            Line = line;
            Column = col;
        }
        public DLRASTBuildException(string msg, int line = 0, int col = 0,Exception inner = null) : base(msg,inner)
        {
            Line = line;
            Column = col;
        }
        public int Line { get; private set; }
        public int Column { get; private set; }
    }
}
