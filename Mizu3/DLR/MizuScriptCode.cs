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
    using Microsoft.Scripting.Runtime;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuScriptCode : ScriptCode
    {
        internal SourceUnit Unit {get;set;}
        internal LambdaExpression Exp { get; set; }
        internal LambdaBuilder Builder { get; set; }
        public MizuScriptCode(LambdaExpression exp, SourceUnit sourceUnit, LambdaBuilder build)
            : base(sourceUnit)
        {
            Unit = sourceUnit;
            Exp = exp;
            Builder = build;
        }
        public override object Run(Microsoft.Scripting.Runtime.Scope scope)
        {
            var del = Exp.Compile();
            return del.DynamicInvoke(scope);
        }

        public override object Run()
        {
            return this.Run(new Scope());
        }
    }
}
