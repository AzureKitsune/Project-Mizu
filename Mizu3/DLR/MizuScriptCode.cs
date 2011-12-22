// -----------------------------------------------------------------------
// <copyright file="MizuScriptCode.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Scripting;
    using System.Linq.Expressions;
    using Microsoft.Scripting.Ast;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuScriptCode : ScriptCode
    {
        internal SourceUnit Unit {get;set;}
        internal LambdaBuilder Builder { get; set; }
        public MizuScriptCode(LambdaBuilder build, SourceUnit sourceUnit)
            : base(sourceUnit)
        {
            Unit = sourceUnit;
            Builder = build;
        }
        public override object Run(Microsoft.Scripting.Runtime.Scope scope)
        {
            Builder.
            var del = Expression.
            return del.DynamicInvoke(scope, null);
        }
    }
}
