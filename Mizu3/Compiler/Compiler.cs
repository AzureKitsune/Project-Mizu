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
    using System.IO;

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
                    (info, doc, mb, file) =>
                    {

                        var finfo = new FileInfo(file);
                        var ty = mb.DefineType(finfo.Name.Replace(finfo.Extension, ""));

                        var scanner = new Scanner();
                        var parser = new Parser(scanner);

                        string src = System.IO.File.ReadAllText(file);

                        var tree = parser.Parse(
                            src);

                        var stmts = tree.Nodes[0].Nodes[0].Nodes;

                        var method = ty.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

                        var il = method.GetILGenerator();

                        var locals = new List<CompilerLocalBuilder>();

                        foreach (ParseNode stmt in stmts)
                        {
                            HandleStmt(stmt, il, info, ref locals, src);
                        }

                        return null;
                    }, ref errors);

            }

            result.Errors = errors.ToArray();
            return result;
        }
        private static void HandleStmt(ParseNode pn,ILGenerator gen,CompilerParameters info,ref List<CompilerLocalBuilder> locals, string src)
        {
            var stmt = pn.Nodes[0];
            switch (stmt.Token.Type)
            {
                case TokenType.LetStatement:
                    {
                        #region LET
                        var name = stmt.Nodes[1];

                        //Debugging info
                        var start = name.GetLineAndCol(src);
                        var end = name.GetLineAndColEnd(src);

                        CompilerLocalBuilder local = null;

                        var data = stmt.Nodes[3];
                        #endregion
                        break;
                    }
            }
        }
        private static void HandleMathExpr(ParseNode pn, ILGenerator gen)
        {

        }
        private static Type HandleRightSideOfEqual(ParseNode pn, ILGenerator gen, ref List<CompilerLocalBuilder> locals)
        {
            switch (pn.Token.Type)
            {
                case TokenType.Argument:
                    {
                        ParseNode inner = pn.Nodes[0];
                        switch (inner.Token.Type)
                        {
                            case TokenType.STRING:
                                {
                                    //Get the string and remove quotes
                                    var str = inner.Text.Substring(1);
                                    str = str.Remove(str.Length - 1);

                                    gen.Emit(OpCodes.Ldstr, str); //pushes the string onto the stack.

                                    return typeof(string);
                                }
                            case TokenType.NUMBER:
                                {
                                    var num = int.Parse(inner.Text);
                                    gen.Emit(OpCodes.Ldc_I4, num);

                                    return typeof(int);
                                }
                            case TokenType.FLOAT:
                                {
                                    var flo = float.Parse(inner.Text);
                                    gen.Emit(OpCodes.Ldc_R4, flo);

                                    return typeof(float);
                                }
                            case TokenType.IDENTIFIER:
                                {
                                    throw new NotImplementedException();
                                    if (pn.Nodes.Count > 1)
                                    {
                                        //getting a value from an array.
                                    }
                                    else
                                    {
                                        //copying a value from a variable.
                                    }
                        }
                        break;
                    }
            }
            return null;
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
