using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mizu3.Compiler;
using Microsoft.Scripting.Hosting;

namespace Mizu3
{
    class Program
    {
        static void Main(string[] args)
        {
            
            CompilerParameters info = new CompilerParameters();
            info.AssemblyName = "Example"; 

            info.MainClass = "Default"; /* If not found, 
                                         * the compiler will search for a suitable method in the assembly 
                                         * and use the first one it finds.
                                         */
             info.SourceCodeFiles = new string[]{args[0]};
            info.OutputFilename = new System.IO.FileInfo(args[0]).DirectoryName + "/Example.exe";
            info.IsDebugMode = true;


            new DLR.DLRCompiler.DLRCompiler().Compile(info);
            object br = null;
            
            
            /*ScriptRuntimeSetup info = new ScriptRuntimeSetup();
            
            // Create runtime
            ScriptRuntime runtime = new ScriptRuntime(info);

            // Load Engine
            ScriptEngine engine = runtime.GetEngine("miz");

            // Execute command
            ScriptSource src = engine.CreateScriptSourceFromString("out \"Hello World\";");
            src.Execute(); */

            // Shutdown engine
        }
    }
}
