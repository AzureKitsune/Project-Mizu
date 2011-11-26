// -----------------------------------------------------------------------
// <copyright file="MizuLanguageContext.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Scripting.Runtime;
    using System.Linq.Expressions;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuLanguageContext : LanguageContext
    {
        public MizuLanguageContext(ScriptDomainManager sc): base(sc){}
        public override Microsoft.Scripting.ScriptCode CompileSourceCode(Microsoft.Scripting.SourceUnit sourceUnit, Microsoft.Scripting.CompilerOptions options, Microsoft.Scripting.ErrorSink errorSink)
        {
            var lamb = AstUtils.Lambda(typeof(void), "exec");

            lamb.Body = Expression.Block(Expression.Block(DLRASTBuilder.Parse(sourceUnit.GetCode(), ref lamb)));

            return new LegacyScriptCode(
                lamb.MakeLambda(), sourceUnit);
        }

        public override int ExecuteProgram(Microsoft.Scripting.SourceUnit program)
        {
            return base.ExecuteProgram(program);
        }
    }
}
