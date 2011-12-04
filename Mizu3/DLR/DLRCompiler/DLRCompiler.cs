// -----------------------------------------------------------------------
// <copyright file="DLRCompiler.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR.DLRCompiler
{
    //http://www.codeproject.com/KB/codegen/astdlrtest.aspx
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Dynamic;
    using System.Linq.Expressions;
    using Microsoft.Scripting.Ast;
    using Microsoft.Scripting.Generation;
    using Microsoft.Scripting.Utils;
    using AstUtils = Microsoft.Scripting.Ast.Utils;
    using Microsoft.Scripting;
    using System.IO;
    using Mizu3.Parser;
    using System.Reflection;
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class DLRCompiler
    {
        public void Compile(Compiler.CompilerParameters info)
        {
            var gen = new AssemblyGen(new System.Reflection.AssemblyName(info.AssemblyName), new FileInfo(info.OutputFilename).DirectoryName, ".exe", info.IsDebugMode);
            var file = info.SourceCodeFiles[0];

            var main =  AstUtils.Lambda(typeof(void),"Main");

            

            var exps = Mizu3.DLR.DLRASTBuilder.Parse(new FileInfo(file), ref main,info.IsDebugMode);
            main.Body = Expression.Block(typeof(void),exps);


            var type= gen.DefinePublicType("Application",typeof(Object), true);
            var meth = type.DefineMethod("Main", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static);
            Microsoft.Scripting.Generation.CompilerHelpers.CompileToMethod(main.MakeLambda(), meth, info.IsDebugMode); ;//CompileToMethod(meth, false);
            type.CreateType();
            gen.AssemblyBuilder.SetEntryPoint(meth);
            gen.SaveAssembly();
            
        }
        private void PushError(string msg, int line, int col)
        {
           //TODO: Implement
        }
        private void PushWarning (string msg,int line, int col)
        {
            //TODO: Implement
        }
        
    }
}
