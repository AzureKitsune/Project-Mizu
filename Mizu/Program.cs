using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using Mizu.Parser;

namespace Mizu
{
    class Program
    {
        static void Main(string[] args)
        {
            //Mizu.Lib.Evaluator.Evaluator.Eval("var a=5;(2+2)");

            Console.WriteLine("Mizu Compiler v" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            if (args.Length >= 2)
            {
                if (System.IO.File.Exists(args[0]))
                {
                    var info  = new FileInfo(args[0]);
                    Console.WriteLine("Parsing: " + info.Name);


                    var scanner = new Mizu.Parser.Scanner();
                    var parser = new Mizu.Parser.Parser(scanner);

                    string code = File.ReadAllText(args[0]);

                    var parsetree = parser.Parse(code);
                    if (parsetree.Errors.Count > 0)
                    {
                        foreach (Mizu.Parser.ParseError err in parsetree.Errors)
                        {
                            Console.Error.WriteLine("{0}: {1} - {2},{3}", err.Message, err.Position, err.Line, err.Column);
                            return;
                        }
                    }
                    else
                    {
                        var output = new FileInfo(args[1]);

                        Console.WriteLine("Compiling: {0} -> {1}", info.Name, output.Name);

                        Compile(parsetree, output);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Input file doesn't exist.");
                }
            }
            else
            {
                Console.WriteLine("mizu <input file> <output file>");
            }
        }
        static void Compile(Mizu.Parser.ParseTree tree, FileInfo output)
        {
            var start = tree.Nodes[0];
            var statements = start.Nodes[0];

            //Declares the assembly and the entypoint
            var name = new AssemblyName(output.Name.Replace(output.Extension,""));
            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(name,AssemblyBuilderAccess.Save,output.DirectoryName);
           

            ModuleBuilder mb = ab.DefineDynamicModule(name.Name,name.Name + ".exe");
            TypeBuilder tb = mb.DefineType("App");
            MethodBuilder entrypoint = tb.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

            var ILgen = entrypoint.GetILGenerator(3072); //gets the IL generator

            
            List<LocalBuilderEx> locals = new List<LocalBuilderEx>(); //A list to hold variables.

            ILgen.BeginExceptionBlock();

            // Generate body IL
            bool err = false;
            foreach (Mizu.Parser.ParseNode statement in statements.Nodes)
            {
                //Iterate though the statements
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
                while(i != -1)
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

            ILgen.BeginCatchBlock(typeof(Exception));

            ILgen.Emit(OpCodes.Rethrow);

            ILgen.EndExceptionBlock(); 


            ILgen.Emit(OpCodes.Ret); //Finishes the statement by calling return

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
                                            ILgen.Emit(OpCodes.Ldc_I4, int.Parse(next.Token.Text)); //Sets the number
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
                    }
                case TokenType.PrintStatement:
                    {
                        ///Generates output by making a print statement.
                        var period = stmt.Nodes[0];
                        var outpt = stmt.Nodes[1];
                        if (outpt.Token.Type == TokenType.IDENTIFIER)
                        {
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
    }
}
