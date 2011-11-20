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
                            CompilerError[] e = null;
                            HandleStmt(stmt, il, info, ref locals, src, file, doc, ty, out e);

                            if (e != null)
                                errors.AddRange(e);

                        }

                        il.Emit(OpCodes.Ret);

                        var final = ty.CreateType();

                        return null;
                    }, ref errors);

                result.Successful = errors.Count == 0;
                ab.Save(param.OutputFilename);

            }

            result.Errors = errors.ToArray();
            return result;
        }
        private static void HandleStmt(ParseNode pn, ILGenerator gen, CompilerParameters info, ref List<CompilerLocalBuilder> locals, string src, string filename, ISymbolDocumentWriter doc, TypeBuilder ty, out CompilerError[] errs)
        {
            errs = null;
            if (pn.Token.Type == TokenType.Statement)
            {
                if (info.IsDebugMode)
                {
                    var start = pn.GetLineAndCol(src);
                    var end = pn.GetLineAndColEnd(src);

                    gen.MarkSequencePoint(doc, start.Line, start.Col, end.Line, end.Col);
                }

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

                            Type typ = null;

                            List<CompilerError> err = new List<CompilerError>();
                            CompilerError[] ee = null;

                            typ = HandleRightSideOfEqual(data, ref gen, ref locals, ref ty, src, filename, out ee);

                            if (ee != null)
                                foreach(CompilerError e in ee)    
                                    err.Add(e);

                            local = new CompilerLocalBuilder(name.Text, gen, typ, info);

                            gen.Emit(OpCodes.Stloc, local.Local);

                            locals.Add(local);

                            #endregion
                            errs = err.ToArray();
                            break;
                        }
                    case TokenType.OutStatement:
                        {
                            #region OUT
                            //Normally, I'd use ILGenerator.EmitWriteLine but I don't wanna.
                            if (stmt.Nodes.Count == 2)
                            {
                                //Write an empty line.
                                gen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { }));
                            }
                            else
                            {
                                var arg = stmt.Nodes[1];

                                CompilerError[] ee;

                                Type typ = HandleRightSideOfEqual(arg,ref  gen, ref locals, ref ty, src, filename, out ee);

                                if (typ != typeof(string))
                                {
                                    if (TypeResolver.IsValueType(typ))
                                    {
                                        gen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typ }));
                                    }
                                    else
                                    {
                                        //Call the local's 'ToString' method if it has one. Otherwise, fail.
                                        throw new NotImplementedException();
                                    }
                                }

                                gen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] {typeof(string) }));

                            }
                            #endregion
                            break;
                        }
                }
            }
        }
        private static Type HandleMathExpr(ParseNode pn, ref ILGenerator gen, ref List<CompilerLocalBuilder> locals, string src, string filename, out CompilerError[] errs)
        {
            errs = new CompilerError[4];
            //type = typeof(float); //Just as a pre-caution.

            var op = pn.Nodes[0];

            var num1 = pn.Nodes[1];

            var num2 = pn.Nodes[2];

            switch (num1.Token.Type)
            {
                case TokenType.NUMBER:
                    {
                        var num = int.Parse(num1.Token.Text);
                        gen.Emit(OpCodes.Ldc_I4, num);
                        break;
                    }
                case TokenType.FLOAT:
                    {
                        var flo = float.Parse(num1.Token.Text);
                        gen.Emit(OpCodes.Ldc_R4, flo);
                        break;
                    }
                case TokenType.IDENTIFIER:
                    {
                        CompilerError ce = null;
                        bool err = LoadLocal(num1, ref locals, ref gen, src, filename, out ce);

                        if (!err)
                        {
                            errs[0] = ce;
                        }
                        else
                        {
                            break;
                        }

                        break;
                    }
                case TokenType.MathExpr:
                    {
                        Type t = null;
                        CompilerError[] ce = null;
                        t = HandleMathExpr(num1, ref gen, ref locals, src, filename, out ce);
                        errs = ce;
                        break;
                    }
            }

            switch (num2.Token.Type)
            {
                case TokenType.NUMBER:
                    {
                        var num = int.Parse(num2.Token.Text);
                        gen.Emit(OpCodes.Ldc_I4, num);
                        break;
                    }
                case TokenType.FLOAT:
                    {
                        var flo = float.Parse(num2.Token.Text);
                        gen.Emit(OpCodes.Ldc_R4, flo);
                        break;
                    }
                case TokenType.IDENTIFIER:
                    {
                        CompilerError ce = null;
                        bool err = LoadLocal(num2, ref locals, ref gen, src, filename, out ce);

                        if (!err)
                        {
                            errs[0] = ce;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                case TokenType.MathExpr:
                    {
                        Type t = null;
                        CompilerError[] ce = null;
                        t = HandleMathExpr(num2, ref gen, ref locals, src, filename, out ce);
                        errs = ce;
                        break;
                    }
            }

            switch (op.Token.Type)
            {
                case TokenType.PLUS:
                    {
                        gen.Emit(OpCodes.Add);
                        break;
                    }
                case TokenType.MINUS:
                    {
                        gen.Emit(OpCodes.Sub);
                        break;
                    }
                case TokenType.MULTI:
                    {
                        gen.Emit(OpCodes.Mul);
                        break;
                    }
                case TokenType.DIV:
                    {
                        gen.Emit(OpCodes.Div);
                        break;
                    }
                default:
                    {
                        var pos = op.GetLineAndCol(src);
                        errs[0] = new CompilerError()
                        {
                            Message = "Unsupported operator.",
                            Line = pos.Line,
                            Col = pos.Col,
                            Filename = filename
                        };
                        break;
                    }
            }
            return typeof(float);

        }

        private static bool LoadLocal(ParseNode p, ref List<CompilerLocalBuilder> locals, ref ILGenerator gen, string src, string filename, out CompilerError err)
        {
            var pos = p.GetLineAndCol(src);

            var lc = locals.Find(it => it.Name == p.Token.Text);

            if (lc == null)
            {
                err = new CompilerError()
                {
                    Message = "Local '" + p.Token.Text + "' doesn't exist!",
                    Line = pos.Line,
                    Col = pos.Col,
                    Filename = filename
                };
                return false;
            }
            else
            {
                err = null;

                gen.Emit(OpCodes.Ldloc, lc.Local);
                return true;
            }
        }
        //Was return Type, then IEnumerable<CompilerError. To lazy to fix.
        private static Type HandleRightSideOfEqual(ParseNode pn, ref ILGenerator gen, ref List<CompilerLocalBuilder> locals, ref TypeBuilder ty, string src, string filename, out CompilerError[] ee)
        {
            ee = null;
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
                                    var str = inner.Token.Text.Substring(1);
                                    str = str.Remove(str.Length - 1);

                                    gen.Emit(OpCodes.Ldstr, str); //pushes the string onto the stack.

                                    return typeof(string);
                                    break;
                                }
                            case TokenType.NUMBER:
                                {
                                    var num = int.Parse(inner.Token.Text);
                                    gen.Emit(OpCodes.Ldc_I4, num);

                                    return  typeof(int);
                                    break;
                                }
                            case TokenType.FLOAT:
                                {
                                    var flo = float.Parse(inner.Token.Text);
                                    gen.Emit(OpCodes.Ldc_R4, flo);

                                    return  typeof(float);
                                    break;
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
                                    return null;
                                }
                        }
                        break;
                    }
                case TokenType.ArrayIndexExpr:
                    {
                        //Creating an array.
                        ParseNode inner = pn.Nodes[0];
                        break;
                    }
                case TokenType.MathExpr:
                    {

                        CompilerError[] ce = null;
                        Type t = HandleMathExpr(pn, ref gen, ref locals, src,filename,out ce);

                        ee = ce;
                        break;
                    }
            }
            return typeof(object);
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

                var res = act(info,
                    doc,
                    mb, file);

                if (res != null)
                    Errors.AddRange(res);
            }
            ab.SetEntryPoint(ab.GetType(info.MainClass).GetMethod("Main"), PEFileKinds.ConsoleApplication); //Sets entry point

            //var finishedtype = TBuilder.CreateType(); //Compile the type

            return ab;
        }
    }
}
