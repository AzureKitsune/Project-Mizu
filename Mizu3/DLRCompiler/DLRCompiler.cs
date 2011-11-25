// -----------------------------------------------------------------------
// <copyright file="DLRCompiler.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLRCompiler
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

            var locals = new List<ParameterExpression>();
            var statements = new List<Expression>();

            {

                string src = System.IO.File.ReadAllText(file);
                var scanner = new Scanner();
                var parser = new Parser(scanner);
                var tree = parser.Parse(src);

                var stmts = tree.Nodes[0].Nodes[0].Nodes;

                foreach (ParseNode pn in stmts)
                {
                    var x = HandleStmt(pn.Nodes[0], ref main, ref locals, src);
                    if (x != null)
                        statements.Add(x); //TODO: Handle this better
                        
                }
                //statements.Add(Expressions.
            }
            var exps = statements.ToArray();
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
        private Expression HandleStmt(ParseNode pn, ref LambdaBuilder func,ref List<ParameterExpression> locals, string src)
        {
            switch (pn.Token.Type)
            {
                case TokenType.LetStatement:
                    {
                        #region LET
                        var start = pn.GetLineAndCol(src);
                        var end = pn.GetLineAndColEnd(src);

                        var nam = pn.Nodes[1].Token.Text;
                        Type ty = null;

                        Expression exp = null;

                        var value =pn.Nodes[3];
                        switch (value.Token.Type)
                        {
                            case TokenType.Argument:
                                {
                                    var inner = value.Nodes[0];
                                    switch (inner.Token.Type)
                                    {
                                        case TokenType.STRING:
                                            ty = typeof(string);

                                            //Remove quotes from string.
                                            string str = inner.Token.Text.TrimStart('\"').TrimEnd('\"');

                                            exp = Expression.Constant(str);
                                            break;
                                        case TokenType.NUMBER:
                                            ty = typeof(int);

                                            exp = Expression.Constant(int.Parse(inner.Token.Text));
                                            break;
                                        case TokenType.FLOAT:
                                            ty = typeof(double);

                                            exp = Expression.Constant(double.Parse(inner.Token.Text));
                                            break;
                                    }
                                }
                                break;
                            case TokenType.MathExpr:
                                {

                                    exp = HandleMathExpr(value, ref func, locals);
                                    ty = exp.Type;
                                    break;
                                }
                            case TokenType.FuncStatement:
                                {
                                    //TODO: Implement this.
                                }
                            default:
                                ty = typeof(object);
                                break;
                        }

                        //TODO: Check if variable exist!

                        ParameterExpression var = (ParameterExpression)func.Variable(ty,nam);

                        locals.Add(var);



                        return Expression.Assign(var, exp);

                        #endregion
                    }
                    
            }
            return null;
        }
        private Expression HandleMathExpr(ParseNode pn, ref LambdaBuilder func, List<ParameterExpression> locals)
        {
            var op = pn.Nodes[0];
            var left = pn.Nodes[1];
            var right = pn.Nodes[2];

            Expression lexp = null;
            Expression rexp = null;

            switch (left.Token.Type)
            {
                case TokenType.MathExpr:
                    {
                        lexp = HandleMathExpr(left, ref func, locals);
                        break;
                    }
                case TokenType.NUMBER:
                    lexp = Expression.Constant(int.Parse(left.Token.Text));
                    break;
                case TokenType.FLOAT:
                    lexp = Expression.Constant(double.Parse(left.Token.Text));
                    break;
                case TokenType.IDENTIFIER:
                    lexp = func.Locals.Find(it => it.Name == left.Token.Text);
                    break;
            }

            switch (right.Token.Type)
            {
                case TokenType.MathExpr:
                    {
                        rexp = HandleMathExpr(right, ref func, locals);
                        break;
                    }
                case TokenType.NUMBER:
                    rexp = Expression.Constant(int.Parse(right.Token.Text));
                    break;
                case TokenType.FLOAT:
                    rexp = Expression.Constant(double.Parse(right.Token.Text));
                    break;
                case TokenType.IDENTIFIER:
                    rexp = func.Locals.Find(it => it.Name == right.Token.Text);
                    break;
            }

            switch (op.Nodes[0].Token.Type)
            {
                case TokenType.PLUS:
                    {
                        return Expression.Add(lexp, rexp);
                    }
                case TokenType.MINUS:
                    {
                        return Expression.Subtract(lexp, rexp);
                    }
                case TokenType.MULTI:
                    {
                        return Expression.Multiply(lexp, rexp);
                    }
                case TokenType.DIV:
                    {
                        return Expression.Divide(lexp, rexp);
                    }
            }
            return Expression.Constant(0);
        }
    }
}
