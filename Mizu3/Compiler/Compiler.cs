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
    [Obsolete("Please use the DLR compiler. This is kept for historic reasons!",true)]
    public class Compiler
    {
        private CompilerParameters params_in = null;
        public CompileOperationResult Compile(CompilerParameters param)
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
                params_in = param;
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
                        
                        //Handle all of the code statements
                        foreach (ParseNode stmt in stmts.FindAll(it => it.Token.Type == TokenType.Statement))
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

                result.Successful = errors.FindAll(it => it != null).Count == 0;
                if (result.Successful)
                    ab.Save(new FileInfo(param.OutputFilename).Name);

            }

            result.Errors = errors.ToArray();
            return result;
        }
        private void HandleStmt(ParseNode pn, ILGenerator gen, CompilerParameters info, ref List<CompilerLocalBuilder> locals, string src, string filename, ISymbolDocumentWriter doc, TypeBuilder ty, out CompilerError[] errs)
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

                            typ = HandleRightSideOfEqual(data, ref gen, ref locals, ref ty, doc, src, filename, out ee);

                            if (ee != null)
                                foreach (CompilerError e in ee)
                                    err.Add(e);

                            local = new CompilerLocalBuilder(name.Token.Text, gen, typ, info, src,name);

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

                                CompilerError ee;

                                //Type typ = HandleRightSideOfEqual(arg,ref  gen, ref locals, ref ty, src, filename, out ee);
                                // ^^ above is not needed. We know its gonna be 'Argument' which is either a string, int (number) or float.

                                switch (arg.Nodes[0].Token.Type)
                                {
                                    case TokenType.IDENTIFIER:
                                        {
                                            Type typ = null;
                                            

                                            if (arg.Nodes.Count > 1)
                                            {
                                                //A variable array.
                                                LoadLocal(arg.Nodes[0], ref locals, ref gen, src, filename, out ee, out typ, false);

                                                var num = arg.Nodes[1].Nodes[1];

                                                //gen.Emit(OpCodes.Ldc_I4, int.Parse(num.Token.Text));
                                                CompilerError[] n; //Discarded
                                                HandleRightSideOfEqual(num, ref gen, ref locals, ref ty, doc, src, filename, out n);

                                                gen.Emit(OpCodes.Ldelem,typeof(object));
                                                typ = typeof(object);
                                                gen.Emit(OpCodes.Callvirt, typ.GetMethod("ToString", new Type[] { }));
                                            }
                                            else
                                            {
                                                //Just a variable.
                                                LoadLocal(arg.Nodes[0], ref locals, ref gen, src, filename, out ee, out typ, true);
                                                if (typ != typeof(string))
                                                {
                                                    if (TypeResolver.IsValueType(typ))
                                                    {
                                                        gen.Emit(OpCodes.Call, typ.GetMethod("ToString", new Type[] { })); //Value types can have their own ToString method called.
                                                        //gen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typ }));
                                                    }
                                                    else
                                                    {
                                                        //Call the local's 'ToString' method if it has one. Otherwise, fail.
                                                        gen.Emit(OpCodes.Callvirt, typ.GetMethod("ToString", new Type[] { }));
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            CompilerError[] e2;
                                            Type typ = HandleRightSideOfEqual(arg, ref  gen, ref locals, ref ty, doc, src, filename, out e2);

                                            if (typ != typeof(string))
                                            {
                                                if (TypeResolver.IsValueType(typ))
                                                {
                                                    //its a non-variable value type, we can call convert.
                                                    gen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typ }));
                                                }
                                                else
                                                {
                                                    //Handle constants that not variables nor value types.
                                                    throw new NotImplementedException();
                                                }
                                            }
                                            break;
                                        }

                                }

                                gen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) }));

                            }
                            #endregion
                            break;
                        }
                    case TokenType.VariableReassignmentStatement:
                        {
                            //Assignment of a value in an array.
                            var arryname = stmt.Nodes[0];
                            var num = stmt.Nodes[1].Nodes[1];

                            CompilerError e;
                            CompilerError[] es;
                            Type t;
                            LoadLocal(arryname, ref locals, ref gen, src, filename, out e, out t, false);
                            gen.Emit(OpCodes.Ldc_I4, int.Parse(num.Token.Text));
                            t = HandleRightSideOfEqual(stmt.Nodes[3],ref gen, ref locals,ref ty, doc, src, filename,out es);
                            if (TypeResolver.IsValueType(t))
                                gen.Emit(OpCodes.Box, t);
                            gen.Emit(OpCodes.Stelem,typeof(object));
                            //gen.Emit(OpCodes.Stloc, locals.Find(it => it.Name == arryname.Token.Text).Local);
                            break;
                        }
                }
            }
        }
        private Type HandleMathExpr(ParseNode pn, ref ILGenerator gen, ref List<CompilerLocalBuilder> locals, string src, string filename, out CompilerError[] errs)
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
                        var flo = double.Parse(num1.Token.Text);
                        gen.Emit(OpCodes.Ldc_R8, flo);
                        break;
                    }
                case TokenType.IDENTIFIER:
                    {
                        CompilerError ce = null;
                        Type t = null;
                        bool err = LoadLocal(num1, ref locals, ref gen, src, filename, out ce, out t);

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
                        var flo = double.Parse(num2.Token.Text);
                        gen.Emit(OpCodes.Ldc_R8, flo);
                        break;
                    }
                case TokenType.IDENTIFIER:
                    {
                        CompilerError ce = null;
                        Type t = null;
                        bool err = LoadLocal(num2, ref locals, ref gen, src, filename, out ce, out t);

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

            switch (op.Nodes[0].Token.Type)
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
                        return typeof(double);
                    }
                default:
                    {
                        var pos = op.GetLineAndCol(src);
                        errs[0] = new CompilerError()
                        {
                            Message = "Unsupported operator '" + op.Token.Text +"'.",
                            Line = pos.Line,
                            Col = pos.Col,
                            Filename = filename
                        };
                        break;
                    }
            }
            return typeof(int);

        }

        private bool LoadLocal(ParseNode p, ref List<CompilerLocalBuilder> locals, ref ILGenerator gen, string src, string filename, out CompilerError err, out Type ty, bool ldaddress = false)
        {
            var pos = p.GetLineAndCol(src);

            var lc = locals.Find(it => it.Name == p.Token.Text);

            ty = null;

            if (lc == null)
            {
                err = new CompilerError()
                {
                    Message = "Local '" + p.Token.Text + "' doesn't exist!",
                    Line = pos.Line,
                    Col = pos.Col,
                    Filename = filename
                };
                ty = null;
                return false;
            }
            else
            {
                err = null;

                ty = lc.LocalType;

                if (ldaddress)
                    gen.Emit(OpCodes.Ldloca, lc.Local);
                else
                    gen.Emit(OpCodes.Ldloc, lc.Local);
                return true;
            }
        }
        //Was return Type, then IEnumerable<CompilerError. To lazy to fix.
        private Type HandleRightSideOfEqual(ParseNode pn, ref ILGenerator gen, ref List<CompilerLocalBuilder> locals, ref TypeBuilder ty, ISymbolDocumentWriter doc, string src, string filename, out CompilerError[] ee)
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
                                }
                            case TokenType.NUMBER:
                                {
                                    var num = int.Parse(inner.Token.Text);
                                    gen.Emit(OpCodes.Ldc_I4, num);

                                    return typeof(int);
                                }
                            case TokenType.FLOAT:
                                {
                                    var flo = double.Parse(inner.Token.Text);
                                    gen.Emit(OpCodes.Ldc_R8, flo);

                                    return typeof(double);
                                }
                            case TokenType.IDENTIFIER:
                                {

                                    if (pn.Nodes.Count > 1)
                                    {
                                        //getting a value from an array.
                                        throw new NotImplementedException();
                                    }
                                    else
                                    {
                                        //copying/loading a value from a variable.
                                        CompilerError e = null;
                                        Type t = null;
                                        var res = LoadLocal(inner, ref locals, ref gen, src, filename, out e, out t);

                                        if (res == false)
                                        {
                                            ee[ee.Length] = e;
                                        }
                                        else
                                        {
                                            return t;
                                        }
                                    }
                                    return null;
                                }
                        }
                        break;
                    }
                case TokenType.FuncStatement:
                    {
                        var parameters = pn.Nodes.FindAll(it => it.Token.Type == TokenType.Parameter);
                        var methodname = pn.Parent.Nodes[1];
                        var body = pn.Nodes.Find(it => it.Token.Type == TokenType.Statement || it.Token.Type == TokenType.Statements);




                        Type rttype = null;

                        if (body.Token.Type == TokenType.Statement)
                            if (body.Nodes.Find(it => it.Token.Type == TokenType.RetStatement) == null)
                                rttype = typeof(void);
                            else
                                rttype = typeof(System.Object);
                        else
                            foreach(var p in body.Nodes)
                                if (p.Nodes.Find(it => it.Token.Type == TokenType.RetStatement) == null)
                                    rttype = typeof(void);
                                else
                                    rttype = typeof(System.Object);


                        string typestr = null;
                        if (rttype == typeof(void))
                        {
                            typestr += "System.Action`";
                            typestr += (parameters.Count).ToString() + "[";
                        }
                        else
                        {
                            typestr += "System.Func`";
                            typestr += (parameters.Count + 1).ToString() + "[";
                        }
                        typestr += "System.Object";

                        if (parameters.Count > 1)
                        {
                            for (int i = 1; i < parameters.Count; i++)
                            {
                                typestr += ",System.Object";
                            }
                        }

                        if (rttype != typeof(void))
                            typestr += ",System.Object]";
                        else
                            typestr += "]";

                        var func = Type.GetType(typestr); 


                        MethodBuilder meth = ty.DefineMethod(methodname.Token.Text, MethodAttributes.Private | MethodAttributes.Static, rttype, TypeResolver.CreateTypeArray(typeof(Object),parameters.Count));

                        var ig = meth.GetILGenerator();
                        switch (body.Token.Type)
                        {
                            case TokenType.Statement:
                                {
                                    CompilerError[] e;

                                    List<CompilerLocalBuilder> l2 = new List<CompilerLocalBuilder>();
                                    l2.AddRange(locals);

                                    HandleStmt(body, ig, params_in, ref l2, src, filename, doc, ty, out e);
                                    break;
                                }
                            case TokenType.Statements:
                                {
                                    CompilerError[] e;

                                    List<CompilerLocalBuilder> l2 = new List<CompilerLocalBuilder>();
                                    l2.AddRange(locals);

                                    foreach(ParseNode p in body.Nodes)
                                        HandleStmt(p, ig, params_in, ref l2, src, filename, doc, ty, out e);

                                    break;
                                }
                        }

                        if (rttype == typeof(void))
                            ig.Emit(OpCodes.Ret);

                        gen.Emit(OpCodes.Ldftn, meth);

                        //return func;

                        
                        return func;
                    }
                case TokenType.ArrayIndexExpr:
                    {
                        //Creating an array.
                        //Single dimension arrays only
                        ParseNode num = pn.Nodes[1];
                        gen.Emit(OpCodes.Ldc_I4, int.Parse(num.Token.Text));
                        gen.Emit(OpCodes.Newarr, typeof(object));
                        //
                        return typeof(object[]);
                    }
                case TokenType.MathExpr:
                    {

                        CompilerError[] ce = null;
                        Type t = HandleMathExpr(pn, ref gen, ref locals, src, filename, out ce);

                        ee = ce;
                        return t;
                    }
            }
            return typeof(object);
        }

        private AssemblyBuilder GenerateAssembly(CompilerParameters info, Func<CompilerParameters,ISymbolDocumentWriter,ModuleBuilder,string,CompilerError[]> act, ref List<CompilerError> Errors)
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

            MethodInfo point = null;

            try 
            {
                point = ab.GetType(info.MainClass).GetMethod("Main");
            }
            catch (Exception)
            {
                foreach (Type t in ab.GetTypes())
                {
                    //Find a type with a suitable method and use it.
                    var m = t.GetMethod("Main");
                    if (m != null)
                    {
                        point = m;
                        break;
                    }
                }
            }

            ab.SetEntryPoint(point, PEFileKinds.ConsoleApplication); //Sets entry point

            //var finishedtype = TBuilder.CreateType(); //Compile the type

            return ab;
        }
    }
}
