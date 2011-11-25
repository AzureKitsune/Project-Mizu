// -----------------------------------------------------------------------
// <copyright file="DLRASTBuilder.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Linq.Expressions;
    using Mizu3.Parser;
    using Microsoft.Scripting.Ast;
    using AstUtils = Microsoft.Scripting.Ast.Utils;
    using System.Reflection;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class DLRASTBuilder
    {
        public static System.Linq.Expressions.Expression[] Parse(string filename, ref LambdaBuilder main)
        {
            var statements = new List<Expression>();

            {
                var locals = new List<ParameterExpression>();

                string src = System.IO.File.ReadAllText(filename);
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
                return statements.ToArray();
            }
        }
        private static Expression HandleStmt(ParseNode pn, ref LambdaBuilder func, ref List<ParameterExpression> locals, string src, LabelTarget loop = null)
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

                        var value = pn.Nodes[3];
                        switch (value.Token.Type)
                        {
                            case TokenType.Argument:
                                {
                                    exp = HandleArgument(value, ref func, out ty);
                                }
                                break;
                            case TokenType.MathExpr:
                                {
                                    exp = HandleMathExpr(value, ref func);
                                    ty = exp.Type;
                                    break;
                                }
                            case TokenType.FuncStatement:
                                {
                                    //TODO: Implement this.
                                    
                                    break;
                                }
                            case TokenType.ArrayIndexExpr:
                                {
                                    var inner = value.Nodes[1];
                                    var e = HandleArgument(inner, ref func);

                                    exp = Expression.NewArrayBounds(typeof(object), e);
                                    ty = exp.Type;

                                    break;
                                }
                            default:
                                ty = typeof(object);
                                break;
                        }

                        //TODO: Check if variable exist!

                        ParameterExpression var = (ParameterExpression)func.Variable(ty, nam);

                        locals.Add(var);



                        return Expression.Assign(var, exp);

                        #endregion
                    }
                case TokenType.BreakStatement:
                    {
                        throw new NotImplementedException("Break Statements!");
                        if (loop == null)
                        {
                            //TODO: Make this an error.
                            return null;
                        }
                        else
                            return Expression.Break(loop);
                    }
                case TokenType.WhileStatement:
                    {
                        Expression w = null;

                        var expr = pn.Nodes[2];

                        var body = pn.Nodes.Find(it => it.Token.Type == TokenType.Statements); //Might be null.

                        var lexp = HandleArgument(expr.Nodes[0], ref func);
                        var rexp = HandleArgument(expr.Nodes[2], ref func);
                        var op = HandleNonMathExpr(expr.Nodes[1], lexp, rexp);

                        var loopfunc = AstUtils.Lambda(typeof(void), "Loop");
                        loopfunc.Locals.AddRange(locals);

                        var l = Expression.Label("LoopBreak");
                        
                        var bodyexp = new List<Expression>();

                        var bodylocs = new List<ParameterExpression>();
                        bodylocs.AddRange(locals);

                        if (body != null)
                            foreach (ParseNode p in body.Nodes)
                                bodyexp.Add(HandleStmt(p.Nodes[0], ref loopfunc, ref bodylocs, src,l));

                        loopfunc.Body = Expression.Block(bodyexp); 
                        
                        w = AstUtils.While(op, loopfunc.MakeLambda(), Expression.Empty());


                        return w;
                    }
                case TokenType.VariableReassignmentStatement:
                    {
                        var vari = func.Locals.Find(it => it.Name == pn.Nodes[0].Token.Text);

                        var inner = pn.Nodes.Find(it => it.Token.Type == TokenType.ArrayIndexExpr);
                        if (inner == null)
                        {
                            //Normal variable assignment.

                            var right = pn.Nodes.Find(it => it.Token.Type == TokenType.Argument || it.Token.Type == TokenType.FuncStatement);

                            switch(right.Token.Type)
                            {
                                case TokenType.Argument:
                                    return Expression.Assign(vari,
                                         HandleArgument(right, ref func));
                                case TokenType.FuncStatement:
                                    throw new NotImplementedException("Func statements in variable reassignment statements!");
                            }

                            return null; //Satisfy the compiler.
                        }
                        else
                        {
                            //Array assignment

                            var e = HandleArgument(inner.Nodes[1], ref func);

                            var right = pn.Nodes[3];
                            var rexp = HandleArgument(right, ref func);


                            return Expression.Assign(
                                Expression.ArrayAccess(vari, e),
                                Expression.Convert(
                                    rexp,
                                    typeof(object)));
                        }
                    }
                case TokenType.OutStatement:
                    {
                        MethodInfo writeLine = null;

                        if (pn.Nodes.Count > 2)
                        {
                            var n = pn.Nodes[1];
                            Type ty = null;
                            var exp = HandleArgument(n, ref func, out ty);

                            writeLine = typeof(Console).GetMethod("WriteLine", new Type[] { ty });
                            return Expression.Call(writeLine, exp);
                        }
                        else
                        {
                            writeLine = typeof(Console).GetMethod("WriteLine", new Type[] { });
                            return Expression.Call(writeLine);
                        }
                    }

            }
            return null;
        }
        private static Expression HandleNonMathExpr(ParseNode value, Expression left, Expression right)
        {
            switch (value.Token.Type)
            {
                case TokenType.LTE: return Expression.LessThanOrEqual(left, right);
                case TokenType.LT: return Expression.LessThan(left, right);
                case TokenType.GT: return Expression.GreaterThan(left, right);
                case TokenType.GTE: return Expression.GreaterThanOrEqual(left, right);
                case TokenType.EQUAL: return Expression.Equal(left, right);
                case TokenType.NOTEQUAL: return Expression.NotEqual(left, right);
            }
            return null;
        }
        private static Expression HandleArgument(ParseNode value, ref LambdaBuilder func) { Type t = null; return HandleArgument(value, ref func, out t); }
        private static Expression HandleArgument(ParseNode value, ref LambdaBuilder func, out Type ty)
        {

            ParseNode inner = null;
            if (value.Token.Type != TokenType.Argument)
                inner = value;
            else
                inner = value.Nodes[0];

            ty = null;

            Expression exp = null;
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
                case TokenType.MathExpr:
                    //Technically, this is not apart of argument.
                    exp = HandleMathExpr(value, ref func);

                    ty = exp.Type;
                    break;
                case TokenType.IDENTIFIER:
                    {
                        //TODO: Make check if variable exist or not!
                        if (value.Nodes.Count == 1)
                        {
                            //If not an array value.
                            exp = func.Locals.Find(it => it.Name == inner.Token.Text);
                            ty = exp.Type;
                        }
                        else
                        {
                            //TODO: Implement getting array values.
                            var vari = func.Locals.Find(it => it.Name == inner.Token.Text);
                            //Expression.PropertyOrField(
                            exp = Expression.ArrayIndex(vari, HandleArgument(value.Nodes[1].Nodes[1], ref func));

                            ty = exp.Type;
                        }
                        return exp;
                    }
                case TokenType.Boolean:
                    {
                        var i = value.Nodes[0].Nodes[0];
                        switch (i.Token.Type)
                        {
                            case TokenType.TRUE:
                                {
                                    exp = Expression.Constant(true);

                                    ty = exp.Type;
                                    break;
                                }
                            case TokenType.FALSE:
                                {
                                    exp = Expression.Constant(false);

                                    ty = exp.Type;
                                    break;
                                }
                        }
                        return exp;
                    }
            }
            return exp;
        }
        private static Expression HandleMathExpr(ParseNode pn, ref LambdaBuilder func)
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
                        lexp = HandleMathExpr(left, ref func);
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
                        rexp = HandleMathExpr(right, ref func);
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
