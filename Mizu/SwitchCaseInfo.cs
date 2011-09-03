// -----------------------------------------------------------------------
// <copyright file="SwitchCaseInfo.cs" company="">
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
    using Mizu.Parser;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class SwitchCaseInfo
    {
        public Label Label { get; set; }
        public int Number { get; set; }
        public SwitchCase_TypeEnum CaseType { get; set; }
        public ParseNode Node { get; set; }
        public ParseNode CaseName { get; set; }
    }
    public enum SwitchCase_TypeEnum
    {
        Number = 0,
        Default = 1,
    }
}
