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
using System.Dynamic;

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

                        if (code.Length == 0)
                        {
                            Console.Error.WriteLine("Error: Input file cannot be empty.");
                            return;
                        }

                        var parsetree = parser.Parse(code); //Parse the code.
                        if (parsetree.Errors.Count > 0)
                        {
                            foreach (Mizu.Parser.ParseError err in parsetree.Errors) //Report all errors.
                            {
                                Console.Error.WriteLine("Error: {0} - On pos: {1}, line: {2}, col: {3}.", err.Message, err.Position, err.Line, err.Column);
                                return;
                            }
                        }
                        else
                        {
                            var output = new FileInfo(args[1]); //Get info on the future output file.

                            if (output.Extension.Length == 0)
                            {
                                Console.Error.WriteLine("Error: Output filename must have an extension.");
                                return;
                            }

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
                        Console.Error.WriteLine("Error: Input file doesn't exist.");
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

           ILgen.BeginScope();

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

                        if (lastloop.LoopAction()) //Calls actions to be called after all of the statements.
                            return; //An error has occurred. Abort.

                        //Checks if the current iterator is equal to the higher number.
                        ILgen.Emit(OpCodes.Ldloc, lastloop.Base.LocalIndex);

                        if (lastloop.LoopDirection == LoopDirectionEnum.Up)
                        {
                            ILgen.Emit(OpCodes.Ldc_I4, lastloop.LoopHigh);
                            ILgen.Emit(OpCodes.Clt);
                        }
                        else
                        {
                            ILgen.Emit(OpCodes.Ldc_I4, lastloop.LoopHigh);
                            ILgen.Emit(OpCodes.Cgt);
                        }
                        ILgen.Emit(OpCodes.Brtrue, lastloop.LoopLabel);

                    }
                    i -= 1;
                }
            }

            ILgen.EndScope();

            ILgen.BeginCatchBlock(typeof(Exception)); //Ends the try statement and starts the catch section.

            /*ILgen.Emit(OpCodes.Call, typeof(Exception).GetMethod("ToString", new Type[] { })); //Gets the exception as a string.

            ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })); */

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

                            if (IsDebug)
                            {
                                int sline = 0, scol = 0;

                                FindLineAndCol(code, stmt.Token.StartPos, ref sline, ref scol);

                                int eline = 0, ecol = 0;

                                FindLineAndCol(code, stmt.Token.EndPos, ref eline, ref ecol);

                                ILgen.MarkSequencePoint(doc, sline, scol, eline, ecol);
                            }

                            if (token.Token.Type == TokenType.IDENTIFIER) //If its a var declaration.
                            {
                                if (locals.Find(it => it.Name == token.Token.Text) == null)
                                {
                                    var set = stmt.Nodes[i + 1];
                                    if (set.Token.Type == TokenType.SET)
                                    {
                                        i += 1;
                                        var next = stmt.Nodes[i + 1];
                                        if (next.Token.Type == TokenType.NUMBER) //Integers
                                        {
                                            //Declares a variable and leaves a reference to it.

                                            LocalBuilderEx local = new LocalBuilderEx();
                                            local.VariableType = typeof(int);
                                            local.Base = ILgen.DeclareLocal(local.VariableType);

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Ldc_I4, int.Parse(next.Token.Text)); //Sets the number
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.Var;

                                            locals.Add(local); //Remembers the variable.
                                            i += 1;
                                        }
                                        else if (next.Token.Type == TokenType.FLOAT)
                                        {
                                            //Declares a variable and leaves a reference to it.

                                            LocalBuilderEx local = new LocalBuilderEx();
                                            local.VariableType = typeof(float);
                                            local.Base = ILgen.DeclareLocal(local.VariableType);

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Ldc_R4, float.Parse(next.Token.Text)); //Sets the number
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
                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.Var;

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            try
                                            {
                                                //If theres a WAVEY symbol, print the variable name.
                                                var wavey = stmt.Nodes[i + 2];
                                                ILgen.Emit(OpCodes.Ldstr, local.Name + " = ");
                                                ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("Write", new Type[] { typeof(string) }));
                                                i += 1;
                                            }
                                            catch (Exception) { }

                                            ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("ReadLine")); //Sets the number from STDIN.
                                            ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToInt32", new Type[] { typeof(string) })); //Parses it into an integer.
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            locals.Add(local); //Remembers the variable.
                                            i += 1;
                                        }
                                        else if (next.Token.Type == TokenType.COMMA)
                                        {
                                            //An array.
                                            Console.Error.WriteLine("Error: Static arrays are not supported at this time.");
                                            err = true;
                                            return;
                                        }
                                        else
                                        {
                                            #region Iterating Variable
                                            //Its a range
                                            var lowNum = stmt.Nodes[i + 2]; //Name is mis-informing. This is really the first number.
                                            var highNum = stmt.Nodes[i + 5]; //Same ^^, this is the second number.



                                            LocalBuilderEx local = new LocalBuilderEx();

                                            local.LoopHigh = int.Parse(highNum.Token.Text);
                                            local.LoopLow = int.Parse(lowNum.Token.Text);

                                            local.Name = token.Token.Text;
                                            local.Type = LocalType.LoopVar;
                                            local.VariableType = typeof(int);


                                            var looplab = ILgen.DefineLabel();
                                            local.Base = ILgen.DeclareLocal(local.VariableType);

                                            local.LoopLabel = looplab;

                                            if (IsDebug) local.Base.SetLocalSymInfo(token.Token.Text); //Set variable name for debug info.

                                            ILgen.Emit(OpCodes.Ldc_I4, local.LoopLow); //Sets the number
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            ILgen.MarkLabel(looplab);
                                            //this is where the IL will execute.

                                            local.LoopAction = () =>
                                            {

                                                //Updates the iterator by 1
                                                ILgen.Emit(OpCodes.Ldloc, local.Base);
                                                ILgen.Emit(OpCodes.Ldc_I4_1);

                                                if (local.LoopLow < local.LoopHigh)
                                                {
                                                    ILgen.Emit(OpCodes.Add); //Loop up
                                                    local.LoopDirection = LoopDirectionEnum.Up;
                                                }
                                                else if (local.LoopLow > local.LoopHigh)
                                                {
                                                    ILgen.Emit(OpCodes.Sub); //Loop down.
                                                    local.LoopDirection = LoopDirectionEnum.Down;
                                                }
                                                else
                                                {
                                                    Console.Error.WriteLine("Error: Variable '{0}' should be set as {1}. In this case, looping is not allowed.", local.Name, local.LoopLow.ToString());
                                                    return true; //Abort because of error.
                                                }

                                                ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                                return false;
                                            };


                                            locals.Add(local); //Remembers the variable.

                                            i += 6;
                                            #endregion
                                        }
                                    }
                                }
                                else
                                {
                                    //Report an error and stop compile process.
                                    err = true;
                                    Console.Error.WriteLine("Error: '{0}' already exist!", token.Token.Text);
                                    break;
                                }
                            }
                        }
                        break;
                        #endregion
                    }
                case TokenType.PrintStatement:
                    {
                        #region Printing
                        if (IsDebug)
                        {
                            int sline = 0, scol = 0;

                            FindLineAndCol(code, stmt.Token.StartPos, ref sline, ref scol);

                            int eline = 0, ecol = 0;

                            FindLineAndCol(code, stmt.Token.EndPos, ref eline, ref ecol);

                            ILgen.MarkSequencePoint(doc, sline, scol, eline, ecol);
                        }

                        ///Generates output by making a print statement.
                        var period = stmt.Nodes[0];
                        var outpt = stmt.Nodes[1];
                        switch (outpt.Token.Type)
                        {
                            case TokenType.IDENTIFIER:
                                {
                                    //Prints a variable.
                                    LocalBuilderEx local = locals.Find(it => it.Name == outpt.Token.Text);

                                    if (local == null)
                                    {
                                        err = true;
                                        Console.Error.WriteLine("Error: '{0}' doesn't exist!", outpt.Token.Text);
                                        return;
                                    }

                                    ILgen.Emit(OpCodes.Ldloc, local.Base);

                                    ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { local.VariableType })); //Converts the integer to a string.

                                    ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })); //Prints the newly formed string.
                                    break;
                                }
                            case TokenType.FLOAT:
                            case TokenType.NUMBER:
                                {
                                    //Prints a integer or float (decimal) number.
                                    if (outpt.Token.Type == TokenType.NUMBER) //if its a integer
                                    {
                                        ILgen.Emit(OpCodes.Ldc_I4, int.Parse(outpt.Token.Text));

                                        ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typeof(int) })); //Converts the integer to a string.
                                    }
                                    else
                                    {
                                        //Otherwise, its a float (decimal).
                                        ILgen.Emit(OpCodes.Ldc_R4, float.Parse(outpt.Token.Text));

                                        ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typeof(float) })); //Converts the integer to a string.
                                    }

                                    ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })); //Prints the newly formed string.
                                    break;
                                }
                            case TokenType.STRING:
                                {
                                    //Prints just a plain string. See the next case for a format string.

                                    string formt = outpt.Token.Text.Substring(1); formt = formt.Remove(formt.Length - 1); //Removes surrounding quotes.

                                    ILgen.Emit(OpCodes.Ldstr, formt); //Loads the string onto the stack

                                    ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })); //Prints the newly formed string.
                                    break;
                                }
                            case TokenType.SIN:
                                {

                                    //Prints a format string

                                    ParseNode formtnd = stmt.Nodes[2];

                                    string formt = formtnd.Token.Text.Substring(1); formt = formt.Remove(formt.Length - 1); //Removes surrounding quotes.

                                    ILgen.Emit(OpCodes.Ldstr, formt); //Loads the format string.

                                    int arrymax = (stmt.Nodes.Count / 2 - 1);
                                    ILgen.Emit(OpCodes.Ldc_I4, arrymax);
                                    ILgen.Emit(OpCodes.Newarr, typeof(string));

                                    int arry_i = 0;



                                    for (int i = 3; i < stmt.Nodes.Count; i++)
                                    {
                                        if (stmt.Nodes[i].Token.Type == TokenType.WHITESPACE)
                                        {
                                            if (stmt.Nodes.Count - 1 == i)
                                                break;

                                            continue;
                                        }
                                        else if (stmt.Nodes[i].Token.Type == TokenType.PERIOD)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            ParseNode nd = stmt.Nodes[i];

                                            ILgen.Emit(OpCodes.Dup);
                                            ILgen.Emit(OpCodes.Ldc_I4, arry_i);

                                            switch (nd.Token.Type)
                                            {
                                                case TokenType.IDENTIFIER:
                                                    {
                                                        LocalBuilderEx local = locals.Find(it => it.Name == nd.Token.Text);

                                                        if (local == null)
                                                        {
                                                            err = true;
                                                            Console.Error.WriteLine("Error: '{0}' doesn't exist!", nd.Token.Text);
                                                            return;
                                                        }

                                                        ILgen.Emit(OpCodes.Ldloc, local.Base);
                                                        ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typeof(int) }));
                                                        break;
                                                    }
                                                case TokenType.NUMBER:
                                                    {
                                                        ILgen.Emit(OpCodes.Ldc_I4, int.Parse(nd.Token.Text));
                                                        ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", new Type[] { typeof(int) }));
                                                        break;
                                                    }
                                            }

                                            ILgen.Emit(OpCodes.Stelem_Ref);

                                            arry_i += 1;

                                        }
                                    }


                                    ILgen.Emit(OpCodes.Call,
                                        typeof(System.String).GetMethod("Format",
                                            new Type[] { typeof(string), typeof(string[]) }));

                                    ILgen.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) })); //Prints the newly formed string.
                                    break;
                                }
                        }
                        break;
                        #endregion
                    }
                case TokenType.EvalStatement:
                    {
                        #region EvalStatement
                        var identifier = stmt.Nodes[1];
                        var expr = stmt.Nodes[3];

                        var exprstr = "";
                        bool localsadded = false;

                        LocalBuilderEx lct = locals.Find(it => it.Name == identifier.Token.Text);
                        if (lct != null)
                        {
                            Console.Error.WriteLine("Error: {0} variable already exist!", identifier.Token.Text);

                            if (lct.Type == LocalType.LoopVar)
                                Console.Error.WriteLine("- Error: Iterating variables are readonly!");

                            err = true;
                            return;
                        }


                        List<LocalBuilder> tmplocals = new List<LocalBuilder>();

                        foreach (LocalBuilderEx lc in locals)
                        {

                            ILgen.Emit(OpCodes.Ldstr, "var " + lc.Name + "={0}");

                            ILgen.Emit(OpCodes.Ldloc, (LocalBuilder)lc.Base);

                            ILgen.Emit(OpCodes.Call,
                                typeof(Convert).GetMethod("ToString", new Type[] { typeof(int) }));

                            ILgen.Emit(OpCodes.Call,
                                typeof(System.String).GetMethod("Format",
                                new Type[] { typeof(string), typeof(string) }));

                            LocalBuilder lb = ILgen.DeclareLocal(typeof(string));
                            ILgen.Emit(OpCodes.Stloc, lb);

                            tmplocals.Add(lb);

                            localsadded = true;
                        }

                        //Creates an array to store all of the variables for the String.Concat call.
                        int arrymax = (tmplocals.Count * 2) + 1;
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
                        local.Base = ILgen.DeclareLocal(typeof(int)); //Sets the number

                        if (IsDebug) local.Base.SetLocalSymInfo(identifier.Token.Text);

                        ILgen.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToInt32", new Type[] { typeof(string) })); //Parses it into an integer.

                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                        local.Name = identifier.Token.Text;
                        local.Type = LocalType.Var;

                        locals.Add(local); //Remembers the variable. 

                        break;
                        #endregion
                    }
                case TokenType.MathCMDStatement:
                    {
                        #region MathCommandStatement
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
                                case TokenType.SQRT:
                                    {
                                        ILgen.Emit(OpCodes.Ldloca, local.Base);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Sqrt"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.SIN:
                                    {
                                        ILgen.Emit(OpCodes.Ldloca, local.Base);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Sin"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.ABS:
                                    {
                                        ILgen.Emit(OpCodes.Ldloca, local.Base);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Abs", new Type[] { typeof(int) }));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.TAN:
                                    {
                                        ILgen.Emit(OpCodes.Ldloca, local.Base);
                                        ILgen.Emit(OpCodes.Call, typeof(Math).GetMethod("Tan"));
                                        ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base);
                                        break;
                                    }
                                case TokenType.COS:
                                    {
                                        ILgen.Emit(OpCodes.Ldloca, local.Base);
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
                            Console.Error.WriteLine("Error: '{0}' doesn't exist!", input.Token.Text);
                            break;
                        }

                        break;
                        #endregion
                    }
                case TokenType.IfStatement:
                    {
                        #region If Statement
                        bool hasElse = false;

                        var left = stmt.Nodes[1];
                        var com = stmt.Nodes[2];
                        var right = stmt.Nodes[3];


                        if (IsDebug)
                        {
                            int sline = 0, scol = 0;

                            FindLineAndCol(code, left.Token.StartPos, ref sline, ref scol);

                            int eline = 0, ecol = 0;

                            FindLineAndCol(code, right.Token.EndPos, ref eline, ref ecol);

                            ILgen.MarkSequencePoint(doc, sline, scol, eline, ecol);
                        }


                        var bodies = stmt.Nodes.FindAll(it => it.Token.Type == TokenType.Statements);

                        hasElse = bodies.Count == 2;

                        //Load the 'left' hand type onto the stack.
                        switch (left.Token.Type)
                        {
                            case TokenType.IDENTIFIER:
                                {
                                    LocalBuilderEx ident = locals.Find(it => it.Name == left.Token.Text);

                                    if (ident == null)
                                    {
                                        err = true;

                                        Console.Error.WriteLine("Variable '{0}' doesn't exist.", left.Token.Text);

                                        return;
                                    }

                                    ILgen.Emit(OpCodes.Ldloc, ident.Base);

                                    break;
                                }
                            case TokenType.NUMBER:
                                {
                                    ILgen.Emit(OpCodes.Ldc_I4, int.Parse(left.Token.Text));
                                    break;
                                }
                            case TokenType.FLOAT:
                                {
                                    ILgen.Emit(OpCodes.Ldc_R4, float.Parse(left.Token.Text));
                                    break;
                                }
                        }

                        //Load the 'right' hand type to the stack.
                        switch (right.Token.Type)
                        {
                            case TokenType.IDENTIFIER:
                                {
                                    LocalBuilderEx ident = locals.Find(it => it.Name == right.Token.Text);

                                    if (ident == null)
                                    {
                                        err = true;

                                        Console.Error.WriteLine("Variable '{0}' doesn't exist.", right.Token.Text);

                                        return;
                                    }

                                    ILgen.Emit(OpCodes.Ldloc, ident.Base);

                                    break;
                                }
                            case TokenType.NUMBER:
                                {
                                    ILgen.Emit(OpCodes.Ldc_I4, int.Parse(right.Token.Text));
                                    break;
                                }
                            case TokenType.FLOAT:
                                {
                                    ILgen.Emit(OpCodes.Ldc_R4, float.Parse(right.Token.Text));
                                    break;
                                }
                        }

                        //Load the 'comparison' function onto the stack.

                        switch (com.Token.Type)
                        {
                            case TokenType.DEQUAL:
                                {
                                    // == 

                                    ILgen.Emit(OpCodes.Ceq);

                                    break;
                                }
                            case TokenType.GT:
                                {
                                    // <
                                    ILgen.Emit(OpCodes.Cgt);
                                    break;
                                }
                            case TokenType.LT:
                                {
                                    // >
                                    ILgen.Emit(OpCodes.Clt);
                                    break;
                                }
                            case TokenType.LTE:
                                {
                                    // >=
                                    ILgen.Emit(OpCodes.Clt);
                                    ILgen.Emit(OpCodes.Ldc_I4_0);
                                    ILgen.Emit(OpCodes.Ceq);
                                    break;
                                }
                            case TokenType.GTE:
                                {
                                    // <=
                                    ILgen.Emit(OpCodes.Cgt);
                                    ILgen.Emit(OpCodes.Ldc_I4_0);
                                    ILgen.Emit(OpCodes.Ceq);
                                    break;
                                }
                            case TokenType.NOTEQUAL:
                                {
                                    // <> and != are accepted.

                                    ILgen.Emit(OpCodes.Ceq);
                                    ILgen.Emit(OpCodes.Ldc_I4_0);
                                    ILgen.Emit(OpCodes.Ceq);
                                    break;
                                }
                        }

                        Label endofifblock = ILgen.DefineLabel();

                        Label ifbodyloc = ILgen.DefineLabel();

                        Label elsebodyloc = ILgen.DefineLabel();

                        if (!hasElse)
                        {
                            //No else block.
                            ILgen.Emit(OpCodes.Brfalse, endofifblock);
                        }
                        else
                        {
                            //Has an else block.

                            //ILgen.Emit(OpCodes.Brtrue, ifbodyloc);
                            //ILgen.Emit(OpCodes.Brtrue, ifbodyloc);
                            ILgen.Emit(OpCodes.Brfalse, elsebodyloc);
                            //ILgen.Emit(OpCodes.Br, ifbodyloc);
                        }

                        // Handle the first body of an if statement.

                        ILgen.MarkLabel(ifbodyloc);
                        ILgen.BeginScope();
                        var ifbody = bodies[0];

                        List<LocalBuilderEx> ifbody_locals = new List<LocalBuilderEx>();
                        locals.ForEach((it) => ifbody_locals.Add(it));

                        foreach (ParseNode pn in ifbody.Nodes)
                        {
                            bool iferr = false;
                            HandleStatement(pn.Nodes[0], ILgen, ref ifbody_locals, out iferr);

                            if (iferr)
                            {
                                err = true;
                                return;
                            }
                        }

                        ILgen.Emit(OpCodes.Br, endofifblock);
                        ILgen.EndScope();

                        //Handle the else bit (if any)
                        if (hasElse)
                        {
                            ILgen.MarkLabel(elsebodyloc);
                            ILgen.BeginScope();

                            var elsebody = bodies[1];

                            List<LocalBuilderEx> elsebody_locals = new List<LocalBuilderEx>();
                            locals.ForEach((it) => elsebody_locals.Add(it));

                            foreach (ParseNode pn in elsebody.Nodes)
                            {
                                bool elerr = false;
                                HandleStatement(pn.Nodes[0], ILgen, ref elsebody_locals, out elerr);

                                if (elerr)
                                {
                                    err = true;
                                    return;
                                }
                            }

                            ILgen.Emit(OpCodes.Br, endofifblock);
                            ILgen.EndScope();

                        }

                        ILgen.MarkLabel(endofifblock);

                        break;
                        #endregion
                    }
                default:
                    {
                        //Report an error and stop compile process.
                        err = true;

                        dynamic info = GetLineAndCol(code, stmt.Token.StartPos);

                        Console.Error.WriteLine("Error: Unsupported statement: {0}. Line: {1}, Col: {2}", stmt.ToString(), info.Line, info.Col);
                        return;
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

        private static ExpandoObject GetLineAndCol(string src, int pos)
        {
            int line = 0, col = 0;

            dynamic eo = new ExpandoObject();
            FindLineAndCol(src, pos, ref line, ref col);

            eo.Line = line;
            eo.Col = col;
            return eo;
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
