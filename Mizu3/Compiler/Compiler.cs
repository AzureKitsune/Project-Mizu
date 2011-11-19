// -----------------------------------------------------------------------
// <copyright file="Compiler.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Reflection;
    using System.Diagnostics.SymbolStore;
    using Mizu3.Parser;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Compiler
    {
        public static CompileOperationResult Compile(CompilerParameters param)
        {
            var result = new CompileOperationResult();

            var errors = new List<CompilerError>();

            if (param.OutputFilename == null || param.SourceCodeFiles == null|| param.MainClass == null)
            {
                result.Successful = false;
                errors.Add(new CompilerError() { 
                    Message = "OutputFilename, SourceCodeFiles, or MainClass was not specified."
                });

            }

            foreach (string file in param.SourceCodeFiles)
            {
                if (!System.IO.File.Exists(file))
                {
                    errors.Add(new CompilerError()
                    {
                        Message = String.Format("File: '{0}' does not exist!",file)
                    });
                    result.Successful = false;
                }
            }

            if (errors.Count == 0)
            {
                AssemblyBuilder ab = GenerateAssembly(param,
                    (info, doc, mb, err,file) =>
                    {



                        var scanner = new Scanner();
                        var parser = new Parser(scanner);

                        var tree = parser.Parse(
                            System.IO.File.ReadAllText(file));



                    }, ref errors);

            }

            result.Errors = errors.ToArray();
            return result;
        }
        private static AssemblyBuilder GenerateAssembly(CompilerParameters info, Func<CompilerParameters,ISymbolDocumentWriter,ModuleBuilder,string,CompilerError[]> act, ref List<CompilerError> Errors)
        {
            if (Errors == null)
                Errors = new List<CompilerError>();

            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(info.AssemblyName),
                AssemblyBuilderAccess.Save);

            if (info.IsDebugMode)
            {

                ///Make assembly debug-able.
                var debugattr = typeof(DebuggableAttribute);
                var db_const = debugattr.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
                var db_builder = new CustomAttributeBuilder(db_const, new Object[] {DebuggableAttribute.DebuggingModes.DisableOptimizations |
             DebuggableAttribute.DebuggingModes.Default});
                ab.SetCustomAttribute(db_builder);
            }

            // Defines main module.
            var mb = ab.DefineDynamicModule(info.AssemblyName, info.AssemblyName + ".exe", info.IsDebugMode);

            foreach (string file in info.SourceCodeFiles)
            {
                ISymbolDocumentWriter doc = null;
                if (info.IsDebugMode)
                {
                    //Define the source code file.
                    doc = mb.DefineDocument(file, Guid.Empty, Guid.Empty, Guid.Empty);
                }

                Errors.AddRange(act(info,
                    doc,
                    mb,file));
            }
            ab.SetEntryPoint(ab.GetType(info.MainClass).GetMethod("Main"), PEFileKinds.ConsoleApplication); //Sets entry point

            //var finishedtype = TBuilder.CreateType(); //Compile the type

            return ab;
        }
    }
}
