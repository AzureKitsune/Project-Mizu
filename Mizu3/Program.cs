using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mizu3.Compiler;
using Microsoft.Scripting.Hosting;
using Mizu3.DLR.DLRInterpreter;

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


            /*new DLR.DLRCompiler.DLRCompiler().Compile(info);
            object br = null; */

            using (var miz = new MizuInterpreter())
            {
                miz.ExecuteCode("let myStr = \"test\"; out myStr;");
            }
            Console.ReadLine();
        }
    }
}
