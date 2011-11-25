// -----------------------------------------------------------------------
// <copyright file="MizuLanguageContext.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLRCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Scripting.Runtime;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuLanguageContext : LanguageContext
    {
        public MizuLanguageContext(ScriptDomainManager sc): base(sc){}
        public override Microsoft.Scripting.ScriptCode CompileSourceCode(Microsoft.Scripting.SourceUnit sourceUnit, Microsoft.Scripting.CompilerOptions options, Microsoft.Scripting.ErrorSink errorSink)
        {
            throw new NotImplementedException();
        }
    }
}
