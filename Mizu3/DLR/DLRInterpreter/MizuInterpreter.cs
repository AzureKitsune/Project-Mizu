// -----------------------------------------------------------------------
// <copyright file="MizuInterpreter.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR.DLRInterpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Scripting.Hosting;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuInterpreter : IDisposable
    {
        private ScriptRuntimeSetup runset = null;
        private ScriptRuntime runtime = null;
        public ScriptEngine Engine { get; private set; }
        private ScriptScope scope = null;

        public MizuInterpreter()
        {

            runset = new ScriptRuntimeSetup();
            //runset.DebugMode = info.IsDebugMode;
            runset.LanguageSetups.Add(
                new LanguageSetup(
                    typeof(Mizu3.DLR.MizuLanguageContext).AssemblyQualifiedName,
                    "Mizu",
                    new string[] { "Mizu", "mz", "miz" },
                    new string[] { ".miz" }));

            // Create runtime
            runtime = new ScriptRuntime(runset);
            
            // Load Engine
            Engine = runtime.GetEngine("Mizu");
            scope = Engine.CreateScope();
        }
        public object ExecuteCode(string code)
        {
            // Execute command
            //ScriptSource src = engine.CreateScriptSourceFromString("out \"Hello World\";", Microsoft.Scripting.SourceCodeKind.InteractiveCode);
            return Engine.Execute(code, scope);

        }
        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                // Shutdown engine
                runtime.Shutdown();
            }
            catch (Exception) { }
        }

        #endregion
    }
}
