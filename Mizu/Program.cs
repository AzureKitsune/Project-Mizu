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
            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(name,AssemblyBuilderAccess.Save);
            ModuleBuilder mb = ab.DefineDynamicModule(name.Name,name.Name + ".exe");
            TypeBuilder tb = mb.DefineType("App");
            MethodBuilder entrypoint = tb.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

            var ilGen = entrypoint.GetILGenerator(); //gets the IL generator


            List<LocalBuilderEx> locals = new List<LocalBuilderEx>(); //A list to hold variables.

            bool err = false;
            foreach (Mizu.Parser.ParseNode statement in statements.Nodes)
            {
                //Iterate though the statements
                var basestmt = statement.Nodes[0];
                HandleStatement(basestmt, ilGen, ref locals, out err);
                if (err == true)
                {
                    return;
                }
            }

            ilGen.Emit(OpCodes.Ret); //Finishes the statement by calling return

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
                                var set = stmt.Nodes[i + 1];
                                if (set.Token.Type == TokenType.SET)
                                {
                                    i += 1;
                                    var next = stmt.Nodes[i + 1];
                                    if (next.Token.Type == TokenType.NUMBER)
                                    {
                                        //Declares a variable and leaves a reference to it.

                                        if (locals.Find(it => it.Name == token.Token.Text) == null)
                                        {

                                            ILgen.DefineLabel();
                                            LocalBuilderEx local = new LocalBuilderEx();
                                            local.Base = ILgen.DeclareLocal(typeof(int));
                                            ILgen.Emit(OpCodes.Ldc_I4, int.Parse(next.Token.Text)); //Sets the number
                                            ILgen.Emit(OpCodes.Stloc, (LocalBuilder)local.Base); //Assigns the number to the variable.

                                            local.Name = token.Token.Text;

                                            locals.Add(local); //Remmebers the variable.
                                            i += 1;
                                        }
                                        else
                                        {
                                            //Report an error and stop compile process.
                                            err = true;
                                            Console.Error.WriteLine("'{0}' already exist!", token.Token.Text);
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        //Its a range
                                        var lowNum = stmt.Nodes[i + 2];
                                        var highNum = stmt.Nodes[i + 5];

                                        var local = ILgen.DeclareLocal(typeof(int[]));
                                        i += 6;
                                    }
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
                            ILgen.EmitWriteLine(locals.Find(it => it.Name == outpt.Token.Text).Base);
                        }
                        break;
                    }
            }
            err = false;
        }
    }
}
