using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using Mizu.Parser;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;

namespace Mizu
{
    class Program
    {
        static bool IsDebug = false;
        static bool IsRun = false;
        static bool IsInvalid = false; //To generate a invalid exe.
        static ISymbolDocumentWriter doc = null; //Debug info from  -> http://blogs.msdn.com/b/jmstall/archive/2005/02/03/366429.aspx
        static string code = null;
        static string mode = null;
        static void Main(string[] args)
        {
#if DEBUG
            mode = "Debug";
#else
            mode = "Release";
#endif

            //Mizu.Lib.Evaluator.Evaluator.Eval("var a=5;(2+2)");
#if !DEBUG
            try
            {
#endif
                Console.WriteLine("Mizu Compiler v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " " + mode + " build.");
                if (args.Length >= 2)
                {
                    if (System.IO.File.Exists(args[0]))
                    {
                        var info = new FileInfo(args[0]); //Loads info on the input file.
                        Console.WriteLine("Parsing: " + info.Name);


                        var scanner = new Mizu.Parser.Scanner(); //Makes a scanner.
                        var parser = new Mizu.Parser.Parser(scanner); //Makes a parser

                        code = File.ReadAllText(args[0]); //Reads all the code.

                        var parsetree = parser.Parse(code); //Parse the code.
                        if (parsetree.Errors.Count > 0)
                        {
                            foreach (Mizu.Parser.ParseError err in parsetree.Errors) //Report all errors.
                            {
                                Console.Error.WriteLine("{0}: {1} - {2},{3}", err.Message, err.Position, err.Line, err.Column);
                                return;
                            }
                        }
                        else
                        {
                            var output = new FileInfo(args[1]); //Get info on the future output file.

                            Console.WriteLine("Compiling: {0} -> {1}", info.Name, output.Name);

                            if (args.Length > 2)
                            {
                                for (int i = 2; i < args.Length; i++)
                                {
                                    switch (args[i].ToLower())
                                    {
                                        case "/debug":
                                            {
                                                if (!IsDebug)
                                                    Console.WriteLine("- Emitting debugging information.");

                                                IsDebug = true;
                                                break;
                                            }
                                        case "/invalid":
                                            {
                                                if (!IsInvalid)
                                                    Console.WriteLine("- Executable will be invalid on purpose.");

                                                IsInvalid = true;
                                                break;
                                            }
                                        case "/run":
                                            {
                                                if (!IsRun)
                                                    Console.WriteLine("- Executable will run when completed.");

                                                IsRun = true;
                                                break;
                                            }
                                    }
                                }
                            }


                            Compile(parsetree, info, output); //Start the compiler process.

                            if (IsRun)
                                Process.Start(output.FullName);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Input file doesn't exist.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Accepted Input: mizu <input file> <output file> <switchs?>");
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return;
            }
#endif
        }
        static void Compile(Mizu.Parser.ParseTree tree, FileInfo input, FileInfo output)
        {
            var start = tree.Nodes[0];
            var statements = start.Nodes[0];

            //Declares the assembly and the entypoint
            var name = new AssemblyName(output.Name.Replace(output.Extension,""));
            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(name,AssemblyBuilderAccess.Save,output.DirectoryName);

            if (IsDebug)
            {
                //Make assembly debug-able.
                Type debugattr = typeof(DebuggableAttribute);
                ConstructorInfo db_const = debugattr.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
                CustomAttributeBuilder db_builder = new CustomAttributeBuilder(db_const, new object[] { 
            DebuggableAttribute.DebuggingModes.DisableOptimizations | 
            DebuggableAttribute.DebuggingModes.Default });

                ab.SetCustomAttribute(db_builder);
            }

            //Defines main module.
            ModuleBuilder mb = ab.DefineDynamicModule(name.Name, name.Name + ".exe", IsDebug);

            if (IsDebug)
            {
                //Define the source code file.
                doc = mb.DefineDocument(input.FullName, Guid.Empty, Guid.Empty, Guid.Empty);
            }

            TypeBuilder tb = mb.DefineType("App"); //Defines main type.
            MethodBuilder entrypoint = tb.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static); //Makes the main method.

            var ILgen = entrypoint.GetILGenerator(3072); //gets the IL generator

            
            List<LocalBuilderEx> locals = new List<LocalBuilderEx>(); //A list to hold variables.

            ILgen.BeginExceptionBlock(); //Start a try statement.

            // Generate body IL
            bool err = false;
            foreach (Mizu.Parser.ParseNode statement in statements.Nodes)
            {
                //Iterate though the statements, generating IL.
                var basestmt = statement.Nodes[0];
                HandleStatement(basestmt, ILgen, ref locals, out err);
                if (err == true)
                {
                    return;
                }
            }
            var loops = locals.FindAll(it => it.Type == LocalType.LoopVar);
            if (loops.Count > 0)
            {
                int i = loops.Count -1;
                while(i != -1) //Iterates through any loops backwards, closing all of the opened loops.
                {
                    var lastloop = loops[i];
                    if (lastloop != null)
                    {

                        lastloop.LoopAction(); //Calls actions to be called after all of the statements.

                        //Checks if the current iterator is equal to the higher number.
                        ILgen.Emit(OpCodes.Ldloc, lastloop.Base.LocalIndex);
                        ILgen.Emit(OpCodes.Ldc_I4, lastloop.LoopHigh);
                        ILgen.Emit(OpCodes.Clt);
                        ILgen.Emit(OpCodes.Brtrue, lastloop.LoopLabel);

                    }
                    i -= 1;
                }
            }

            ILgen.BeginCatchBlock(typeof(Exception)); //Ends the try statement and starts the catch section.

            ILgen.Emit(OpCodes.Rethrow); //Rethrows the exception

            ILgen.EndExceptionBlock();  //Ends the catch section.


            if (!IsInvalid) ILgen.Emit(OpCodes.Ret); //Finishes the statement by calling return. If a invalid exe is wanted, it omits this statement.

            ab.SetEntryPoint(entrypoint); //Sets entry point

            Type finishedtype =  tb.CreateType(); //Compile the type

            ab.Save(output.Name); //Save

        }
        static void HandleStatement(Mizu.Parser.ParseNode stmt, ILGenerator ILgen, ref List<LocalBuilderEx> locals, out bool err)
        {
            switch (stmt.Token.Type)
            {
                case Parser.TokenType.VarStatement:
                    {
                        #region VAR
                        int i = 0;
                        while (i != stmt.Nodes.Count - 1)
                        {
                            var token = stmt.Nodes[i];

                            if (token.Token.Type == TokenType.IDENTIFIER) //If its a var declaration.
                            {
                                if (locals.Find(it => it.Name == token.Token.Text) == null)
                                {
                                    var set = stmt.Nodes[i + 1];
                                    if (set.Token.Type == TokenType.SET)
                                    {
                                        i += 1;
                                        var next = stmt.Nodes[i + 1];
                                        if (next.Token.Type == TokenType.NUMBER)
                                        {
                                            //Declares a variable and leaves a reference to it.

                                            LocalBuilderEx local = new LocalBuilderEx();
                                            local.Base = ILgen.DeclareLocal(typeof(int));

                                            if(IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Ldc_I4, int.Parse(next.Token.Text)); //Sets the number
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.Var;

                                            locals.Add(local); //Remembers the variable.
                                            i += 1;
                                        }
                                        else if (next.Token.Type == TokenType.UPPER)
                                        {
                                            //A variable that reads from stdin (Console.ReadLne)
                                            //Declares a variable and leaves a reference to it.

                                            LocalBuilderEx local = new LocalBuilderEx();
                                            local.Base = ILgen.DeclareLocal(typeof(int));

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("ReadLine")); //Sets the number from STDIN.
                                            //ILgen.Emit(OpCodes.Call,typeof(int).GetMethod("Parse", new Type[]{typeof(string)})); //Parses it into an integer.
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.Var;

                                            locals.Add(local); //Remembers the variable.
                                            i += 1;
                                        }
                                        else
                                        {
                                            //Its a range
                                            var lowNum = stmt.Nodes[i + 2];
                                            var highNum = stmt.Nodes[i + 5];



                                            LocalBuilderEx local = new LocalBuilderEx();

                                            local.LoopHigh = int.Parse(highNum.Token.Text);
                                            local.LoopLow = int.Parse(lowNum.Token.Text);

                                            var looplab = ILgen.DefineLabel();
                                            local.Base = ILgen.DeclareLocal(typeof(int));

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Ldc_I4, local.LoopLow); //Sets the number
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            ILgen.MarkLabel(looplab);
                                            //this is where the IL will execute.

                                            local.LoopAction = () =>
                                            {
                                             
                                                //Updates the iterator by +1
                                                ILgen.Emit(OpCodes.Ldloc, local.Base.LocalIndex);
                                                ILgen.Emit(OpCodes.Ldc_I4_1);
                                                ILgen.Emit(OpCodes.Add);
                                                ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                            };


                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.LoopVar;
                                            local.LoopLabel = looplab;

                                            locals.Add(local); //Remembers the variable.

                                            i += 6;
                                        }
                                    }
                                }
                                else
                                {
                                    //Report an error and stop compile process.
                                    err = true;
                                    Console.Error.WriteLine("'{0}' already exist!", token.Token.Text);
                                    break;
                                }
                            }
                        }
                        break;
                        #endregion
                    }
                case TokenType.PrintStatement:
                    {
                        ///Generates output by making a print statement.
                        var period = stmt.Nodes[0];
                        var outpt = stmt.Nodes[1];
                        if (outpt.Token.Type == TokenType.IDENTIFIER)
                        {
                            if (IsDebug)
                            {
                                int sline = 0, scol = 0;

                                FindLineAndCol(code,stmt.Token.StartPos,ref sline,ref scol);

                                int eline = 0, ecol = 0;

                                FindLineAndCol(code, stmt.Token.EndPos, ref eline, ref ecol);

                                ILgen.MarkSequencePoint(doc, sline, scol, eline, ecol);
                            }

                            ILgen.Emit(OpCodes.Ldloc, locals.Find(it => it.Name == outpt.Token.Text).Base);
                            ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) }));
                            //ILgen.Emit(OpCodes.Pop);
                        }
                        break;
                    }
                case TokenType.EvalStatement:
                    {
                        var identifier = stmt.Nodes[1];
                        var expr = stmt.Nodes[3];

                        var exprstr = "";
                        bool localsadded = false;


                        List<LocalBuilder> tmplocals = new List<LocalBuilder>();

                        foreach (LocalBuilderEx lc in locals)
                        {

                            ILgen.Emit(OpCodes.Ldstr, "var " + lc.Name + "={0}");

                            ILgen.Emit(OpCodes.Ldloc, (LocalBuilder)lc.Base);
                            ILgen.Emit(OpCodes.Box, typeof(int));
                            ILgen.Emit(OpCodes.Castclass, typeof(object));

                            ILgen.Emit(OpCodes.Call,
                                typeof(System.String).GetMethod("Format",
                                new Type[] { typeof(string), typeof(string) }));

                            LocalBuilder lb = ILgen.DeclareLocal(typeof(string));
                            ILgen.Emit(OpCodes.Stloc, lb);

                            tmplocals.Add(lb);

                            localsadded = true;
                        }

                        int arrymax = (tmplocals.Count*2) + 1;
                        ILgen.Emit(OpCodes.Ldc_I4, arrymax);
                        ILgen.Emit(OpCodes.Newarr, typeof(string));

                        int arry_i = 0;

                        if (localsadded)
                        {
                            foreach (LocalBuilder tmploc in tmplocals)
                            {
                                ILgen.Emit(OpCodes.Dup);
                                ILgen.Emit(OpCodes.Ldc_I4, arry_i);
                                ILgen.Emit(OpCodes.Ldloc, tmploc);
                                ILgen.Emit(OpCodes.Stelem_Ref);

                                arry_i += 1;


                                ILgen.Emit(OpCodes.Dup);
                                ILgen.Emit(OpCodes.Ldc_I4, arry_i);
                                ILgen.Emit(OpCodes.Ldstr, ";");
                                ILgen.Emit(OpCodes.Stelem_Ref);

                                arry_i += 1;
                            }
                        }

                        ILgen.Emit(OpCodes.Dup);
                        ILgen.Emit(OpCodes.Ldc_I4, arry_i);

                        exprstr += GenerateExprStr(expr);
                        ILgen.Emit(OpCodes.Ldstr, exprstr);

                        ILgen.Emit(OpCodes.Stelem_Ref);
                        arry_i += 1;

                        ILgen.Emit(OpCodes.Call,
    typeof(System.String).GetMethod("Concat",
    new Type[] { typeof(string[]) }));

                        ILgen.Emit(OpCodes.Call, typeof(Mizu.Lib.Evaluator.Evaluator).GetMethod("Eval"));

                        LocalBuilderEx local = new LocalBuilderEx();
                        local.Base = ILgen.DeclareLocal(typeof(string)); //Sets the number

                        if (IsDebug) local.Base.SetLocalSymInfo(identifier.Token.Text);

                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                        local.Name = identifier.Token.Text;
                        local.Type = LocalType.Var;

                        locals.Add(local); //Remembers the variable. 

                        break;
                    }
                case TokenType.MathCMDStatement:
                    {
                        var cmd = stmt.Nodes[0];
                        var input = stmt.Nodes[2];
                        var local = locals.Find(it => it.Name == input.Token.Text);
                        if (local != null)
                        {
                            if (IsDebug)
                            {
                                int sline = 0, scol = 0;

                                FindLineAndCol(code, cmd.Token.StartPos, ref sline, ref scol);

                                int eline = 0, ecol = 0;

                                FindLineAndCol(code, cmd.Token.EndPos, ref eline, ref ecol);

                                ILgen.MarkSequencePoint(doc, sline, scol, eline, ecol);
                            }

                            switch (cmd.Token.Type)
                            {
                                case TokenType.SIN:
                                    {
                                        ILgen.Emit(OpCodes.Ldloc, local.Base.LocalIndex);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Sin"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.ABS:
                                    {
                                        ILgen.Emit(OpCodes.Ldloc, local.Base.LocalIndex);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Abs", new Type[] { typeof(int) }));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.TAN:
                                    {
                                        ILgen.Emit(OpCodes.Ldloc, local.Base.LocalIndex);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Tan"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.COS:
                                    {
                                        ILgen.Emit(OpCodes.Ldloc, local.Base.LocalIndex);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Cos"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            //Report an error and stop compile process.
                            err = true;
                            Console.Error.WriteLine("'{0}' doesn't exist!", input.Token.Text);
                            break;
                        }

                        break;
                    }
                default:
                    {
                        //Report an error and stop compile process.
                        err = true;
                        Console.Error.WriteLine("Unsupported statement: {0}", stmt.ToString());
                        break;
                    }
            }
            err = false;
        }
        static string GenerateExprStr(ParseNode expr)
        {
            string res = expr.Token.Text;
            foreach (ParseNode pn in expr.Nodes)
            {
                res += GenerateExprStr(pn);
            }
            return res;
        }
        private static void FindLineAndCol(string src, int pos, ref int line, ref int col)
        {
            //http://www.codeproject.com/Messages/3852786/Re-ParseError-line-numbers-always-0.aspx

            line = 1;
            col = 0;

            for (int i = 0; i < pos; i++)
            {
                if (src[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
        }
    }
}
