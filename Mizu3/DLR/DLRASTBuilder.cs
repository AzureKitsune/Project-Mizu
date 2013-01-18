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
    using System.IO;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class DLRASTBuilder
    {
        public static System.Linq.Expressions.Expression[] Parse(System.IO.FileInfo file, ref LambdaBuilder main, bool iscompiler = false, bool generateDebuginfo = false)
        {
            var code = "";
            var str = new StreamReader(file.OpenRead());
            try
            {
                code = str.ReadToEnd();
            }
            catch (Exception)
            {

            }
            finally
            {
                str.Close();
                str.Dispose();
            }
            SymbolDocumentInfo doc = null;
            if (generateDebuginfo)
                doc = Expression.SymbolDocument(file.FullName);
            return Parse(code, ref main, generateDebuginfo, iscompiler, doc);
        }
        [DebuggerNonUserCode]
        public static System.Linq.Expressions.Expression[] Parse(string source, ref LambdaBuilder main, bool generateDebuginfo = false, bool iscompiler = false, SymbolDocumentInfo doc = null)
        {
            var statements = new List<Expression>();
            var errors = new List<DLRASTBuildException>();
            {
                var locals = new List<ParameterExpression>();

                var scanner = new Scanner();
                var parser = new Parser(scanner);
                var tree = parser.Parse(source);
                if (tree.Errors.Count > 0)
                {
                    foreach (ParseError pe in tree.Errors)
                        errors.Add(new DLRASTBuildException(pe.Message, pe.Line, pe.Column));
                }
                else
                {
                    var stmts = tree.Nodes[0].Nodes[0].Nodes;

                    foreach (ParseNode pn in stmts)
                    {
                        try
                        {
                            var stmt = pn.Nodes[0];
                            var x = HandleStmt(stmt, ref main, ref locals, source, null, iscompiler);
                            if (x != null)
                            {
                                if (generateDebuginfo)
                                {
                                    if (doc == null)
                                        throw new ArgumentNullException("doc");

                                    var cord = stmt.GetLineAndCol(source);
                                    var cordend = stmt.GetLineAndColEnd(source);

                                    statements.Add(
                                        Expression.DebugInfo(doc, cord.Line, cord.Col + 1, cordend.Line, cordend.Col));


                                }

                                statements.Add(x); //TODO: Handle this better
                            }
                        }
                        catch (DLRASTBuildException dex)
                        {
                            errors.Add(dex);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new DLRASTBuildException("An inner exception has occured.", 0, 0, ex));
                        }

                    }
                }

                if (errors.Count > 0)
                    throw new AggregateException(errors.ToArray());

                return statements.ToArray();
            }
        }
        private static Expression HandleStmt(ParseNode pn, ref LambdaBuilder func, ref List<ParameterExpression> locals, string src, LabelTarget label = null, bool iscompiler = true)
        {

            switch (pn.Token.Type)
            {
                case TokenType.LetStatement:
                    {
                        #region LET
                        /*var start = pn.GetLineAndCol(src);
                        var end = pn.GetLineAndColEnd(src); */

                        var nam = pn.Nodes[1].Token.Text;

                        if (func.Locals.Find(it => it.Name == nam) != null)
                        {
                            var cord = pn.GetLineAndCol(src);
                            throw new DLRASTBuildException(
                                String.Format("The '{0}' variable already exist in this scope!", nam)
                                , cord.Line, cord.Col);
                        }

                        Type ty = null;

                        Expression exp = null;

                        switch (pn.Nodes[2].Token.Type)
                        {
                            case TokenType.EQUAL:
                                {
                                    #region Anything but a function call
                                    var value = pn.Nodes[3];
                                    switch (value.Token.Type)
                                    {
                                        case TokenType.Argument:
                                            {
                                                exp = HandleArgument(value, src, ref func, out ty, iscompiler);
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
                                                exp = HandleFunc(value, ref func);
                                                ty = exp.Type;
                                                break;
                                            }
                                        case TokenType.ArrayIndexExpr:
                                            {
                                                //For creating arrays.
                                                var inner = value.Nodes.Find(it => it.Token.Type == TokenType.MathExpr || it.Token.Type == TokenType.IDENTIFIER || it.Token.Type == TokenType.NUMBER);
                                                var e = HandleArgument(inner, src, ref func);

                                                var gentype = value.Nodes.Find(it => it.Token.Type == TokenType.GenericTypeIdentifier);

                                                if (gentype == null)
                                                    exp = Expression.NewArrayBounds(typeof(object), e); //Creates the default array. Object[] with the size of 'e'.
                                                else
                                                {
                                                    string requested_type = gentype.Nodes[1].Token.Text;
                                                    var t = DLRTypeResolver.ResolveType(requested_type);

                                                    exp = Expression.NewArrayBounds(t, e);
                                                }
                                                ty = exp.Type;

                                                break;
                                            }
                                        case TokenType.IterStatement:
                                            {
                                                var iter = pn.Nodes[3];
                                                exp = HandleIterStatement(iter, ref func, src);

                                                ty = exp.Type;
                                                break;
                                            }
                                        default:
                                            ty = typeof(object);
                                            break;
                                    }
                                    break;
                                    #endregion
                                }
                            case TokenType.ARROW:
                                {
                                    #region Function calls
                                    exp = HandleMethodCall(pn.Nodes[3], ref func, src, iscompiler);

                                    ty = exp.Type;
                                    #endregion
                                    break;
                                }
                        }

                        //TODO: Check if variable exist!

                        ParameterExpression var = (ParameterExpression)func.Variable(ty, nam);

                        locals.Add(var);

                        if (iscompiler)
                            return Expression.Assign(var, exp);
                        else
                        {
                            MizuLanguageContext.Instance.ScopeSetVariable(MizuLanguageContext.GlobalScope, nam, exp);
                            return Expression.Default(typeof(object));
                        }

                        #endregion
                    }
                case TokenType.BreakStatement:
                    {
                        //throw new NotImplementedException("Break Statements!");
                        if (label == null)
                        {
                            //TODO: Make this an error.
                            var cord = pn.GetLineAndCol(src);
                            throw new DLRASTBuildException("Break is invalid in this context.", cord.Line, cord.Col);
                        }
                        else
                            return Expression.Break(label);
                    }
                case TokenType.RetStatement:
                    {
                        if (label == null)
                        {
                            //TODO: Make this an error.
                            var cord = pn.GetLineAndCol(src);
                            throw new DLRASTBuildException("Return is invalid in this context.", cord.Line, cord.Col);
                        }
                        else
                        {
                            if (pn.Nodes.Count > 1)
                            {
                                var id = pn.Nodes[1];
                                var exp = HandleArgument(id, src, ref func);

                                if (exp.Type != @label.Type)
                                    exp = Expression.Convert(exp, @label.Type);

                                return Expression.Return(@label, exp);
                            }
                            else
                                return Expression.Return(@label);
                        }
                    }
                case TokenType.MethodCallStatement:
                    {
                        var call = pn.Nodes[0];
                        var exp = HandleMethodCall(call, ref func, src, iscompiler);
                        return exp;
                        break;
                    }
                case TokenType.WhileStatement:
                    {
                        #region While

                        Expression w = null;

                        var expr = pn.Nodes[2];

                        var body = pn.Nodes.Find(it => it.Token.Type == TokenType.Statements); //Might be null.

                        var lexp = HandleArgument(expr.Nodes[0], src, ref func);
                        var rexp = HandleArgument(expr.Nodes[2], src, ref func);
                        var op = HandleNonMathExpr(expr.Nodes[1], lexp, rexp);

                        var loopfunc = AstUtils.Lambda(typeof(void), "Loop");
                        loopfunc.Parameters.AddRange(locals);


                        //loopfunc.

                        var @l = Expression.Label("LoopBreak");

                        var bodyexp = new List<Expression>();

                        var bodylocs = new List<ParameterExpression>(); //Not needed
                        //bodylocs.AddRange(locals);

                        if (body != null)
                            foreach (ParseNode p in body.Nodes)
                            {
                                var st = HandleStmt(p.Nodes[0], ref loopfunc, ref bodylocs, src, l);
                                if (st != null)
                                    bodyexp.Add(st);
                            }

                        loopfunc.Body = Expression.Block(bodyexp);



                        w = AstUtils.While(op, loopfunc.Body, Expression.Empty(), @l, null);


                        return w;
                        #endregion
                    }
                case TokenType.VariableReassignmentStatement:
                    {
                        #region Variable Assignment
                        var vari = GetVariable(pn.Nodes[0], ref func, src);


                        var oper = pn.Nodes.Find(it => it.Token.Type == TokenType.EQUAL || it.Token.Type == TokenType.ARROW);

                        switch (oper.Token.Type)
                        {
                            case TokenType.EQUAL:
                                {
                                    #region Anything but function calls
                                    var inner = pn.Nodes.Find(it => it.Token.Type == TokenType.ArrayIndexExpr);
                                    if (inner == null)
                                    {
                                        //Normal variable assignmenet.

                                        var right = pn.Nodes.Find(it => it.Token.Type == TokenType.Argument || it.Token.Type == TokenType.FuncStatement || it.Token.Type == TokenType.MathExpr);

                                        switch (right.Token.Type)
                                        {
                                            case TokenType.Argument:
                                                return Expression.Assign(vari,
                                                     HandleArgument(right, src, ref func));
                                            case TokenType.FuncStatement:
                                                throw new NotImplementedException("Func statements in variable reassignment statements!");
                                            case TokenType.MathExpr:
                                                return Expression.Assign(vari,
                                                    HandleMathExpr(right, ref func));
                                        }

                                        return null; //Satisfy the compiler.
                                    }
                                    else
                                    {
                                        //Array assignment

                                        var e = HandleArgument(inner.Nodes[1], src, ref func);

                                        ParseNode[] allright = new Lazy<ParseNode[]>(new Func<ParseNode[]>(() =>
                                        {
                                            int indr = pn.Nodes.IndexOf(
                                                 pn.Nodes.Find(it => it.Token.Type == TokenType.EQUAL));
                                            return pn.Nodes.GetRange(indr + 1, pn.Nodes.Count - indr - 1).ToArray();
                                        })).Value;
                                        var right = allright[0];
                                        var rexp = HandleArgument(right, src, ref func);

                                        Expression arrexpr = null;

                                        var arryty = vari.Type.GetElementType();
                                        if (rexp.Type != arryty)
                                        {
                                            arrexpr = Expression.Convert(
                                                rexp,
                                                arryty);
                                        }
                                        else
                                        {
                                            arrexpr = rexp;
                                        }

                                        return Expression.Assign(
                                            Expression.ArrayAccess(vari, e), arrexpr);
                                    }
                                }
                                    #endregion
                                break;
                            case TokenType.ARROW:
                                /*var right = pn.Nodes.Find(it => it.Token.Type == TokenType.FuncCallStatement);
                                return Expression.Assign(vari,
                                    HandleFuncCall(pn.Nodes[3], ref func, src); */
                                throw new NotImplementedException("Assigning anonymous functions to an already assigned variable.");
                                break;
                        }
                        break;
                        #endregion
                    }
                case TokenType.TryCatchStatement:
                    {
                        var statements = pn.Nodes.Find(it => it.Token.Type == TokenType.Statements);

                        var catches = pn.Nodes.FindAll(it => it.Token.Type == TokenType.TryCatchStatement_CatchBlock);

                        var tryfunc = AstUtils.Lambda(typeof(void), "Try");
                        tryfunc.Parameters.AddRange(locals);
                        var bodyexp = new List<Expression>();
                        var bodylocs = new List<ParameterExpression>(); //Not needed

                        foreach (ParseNode p in statements.Nodes)
                        {
                            var st = HandleStmt(p.Nodes[0], ref tryfunc, ref bodylocs, src, null);
                            if (st != null)
                                bodyexp.Add(st);
                        }

                        tryfunc.Body = Expression.Block(bodyexp);

                        var catchList = new List<CatchBlock>();

                        foreach (var cpn in catches)
                        {
                            var catchfunc = AstUtils.Lambda(tryfunc.ReturnType, "Catch");

                            var par = cpn.Nodes.Find(it => it.Token.Type == TokenType.Parameter);
                            var parex = HandleParameter(par, ref catchfunc);
                            //catchfunc.Parameters.Add(parex);

                            catchfunc.Parameters.AddRange(locals);
                            var catchexp = new List<Expression>();
                            var stmt = cpn.Nodes.Find(it => it.Token.Type == TokenType.Statements);
                            foreach (ParseNode p in stmt.Nodes)
                            {
                                var st = HandleStmt(p.Nodes[0], ref catchfunc, ref bodylocs, src, null);
                                if (st != null)
                                    catchexp.Add(st);
                            }
                            catchfunc.Body = Expression.Block(catchexp);




                            var block = Expression.Catch(parex, catchfunc.MakeLambda());
                            catchList.Add(block);
                        }

                        return Expression.TryCatch(tryfunc.MakeLambda(), catchList.ToArray());

                        break;
                    }
                case TokenType.OutStatement:
                    {
                        MethodInfo writeLine = null;

                        if (pn.Nodes.Count > 2)
                        {
                            var n = pn.Nodes[1];
                            Type ty = null;
                            Expression exp = null;
                            switch (n.Token.Type)
                            {
                                case TokenType.Argument:
                                    {
                                        exp = HandleArgument(n, src, ref func, out ty, iscompiler);
                                        break;
                                    }
                                case TokenType.RETURN:
                                    {
                                        exp = HandleMethodCall(pn.Nodes[2], ref func, src, iscompiler);
                                        ty = exp.Type;
                                        break;
                                    }
                                case TokenType.MethodCall:
                                    {
                                        exp = HandleMethodCall(n, ref func, src, iscompiler);
                                        ty = exp.Type;
                                        break;
                                    }
                            }

                            if (iscompiler)
                            {
                                writeLine = typeof(Console).GetMethod("WriteLine", new Type[] { ty });
                                return Expression.Call(writeLine, exp);
                            }
                            else
                            {
                                writeLine = typeof(TextWriter).GetMethod("WriteLine", new Type[] { typeof(object) });

                                return Expression.Call(Expression.Constant(MizuLanguageContext.Instance.DomainManager.SharedIO.OutputWriter), writeLine, new Expression[] { (Expression)((ConstantExpression)exp).Value });
                            }
                        }
                        else
                        {
                            if (iscompiler)
                            {
                                writeLine = typeof(Console).GetMethod("WriteLine", new Type[] { });
                                return Expression.Call(writeLine);
                            }
                            else
                            {
                                writeLine = typeof(TextWriter).GetMethod("WriteLine", new Type[] { });

                                return Expression.Call(Expression.Constant(MizuLanguageContext.Instance.DomainManager.SharedIO.OutputWriter), writeLine);
                            }
                        }
                    }

            }
            return null;
        }
        private static Expression HandleIterStatement(ParseNode value, ref LambdaBuilder func, string src = "")
        {
            throw new NotImplementedException("Iter Statements are not supported at this time.");


            var body = value.Nodes[value.Nodes.Count - 1];

            var l = value.Nodes.IndexOf(
                    value.Nodes.Find(it => it.Token.Type == TokenType.OPENBR));
            var r = value.Nodes.IndexOf(
                    value.Nodes.Find(it => it.Token.Type == TokenType.CLOSEBR));
            var parms = value.Nodes.GetRange(l + 1, r - l - 1);

            if (parms.Count != 3)
            {
                //Just an indentifier
            }
            else
            {
                //Range from x to y.
                var x = parms[0];
                var y = parms[2];
                //Expression.NewArrayInit(typeof(object),
            }
            return null;
        }
        private static Expression HandleMethodCall(ParseNode value, ref LambdaBuilder func, string src = "", bool iscompiler = true)
        {
            value = (value.Token.Type == TokenType.MethodCallStatement ? value.Nodes[0] : value);
            var funccall = value.Nodes[0];

            if (value.Nodes.Find(it => it.Token.Type == TokenType.BROPEN) != null)
            {

                var name = funccall;
                name = (name.Token.Type == TokenType.IDENTIFIER || name.Token.Type == TokenType.TYPE ? name : name.Nodes[0]);
                var nam = GetVariable(name, ref func, src, true, false);

                if (nam != null)
                {
                    //Variable function (anonymous) called?
                    var args = value.Nodes.FindAll(it => it.Token.Type == TokenType.Argument);

                    var arglist = new List<Expression>();
                    foreach (var a in args)
                        arglist.Add(HandleArgument(a, src, ref func));

                    if (arglist.Count > 0)
                        return Expression.Invoke(nam, arglist.ToArray());
                    else
                        return Expression.Invoke(nam);
                }
                else
                {
                    //.NET type method. Eventually, I'll need to switch to the DLR way of doing this.
                    //

                    string type = name.Token.Text;
                    string ident = type.Substring(0,
                        type.LastIndexOf('.'));
                    string call = type.Substring(type.LastIndexOf('.') + 1);

                    var nam2 = GetVariable(ident, ref func, src, false, false, iscompiler);

                    if (nam2 != null)
                    {
                        MethodInfo meth = null;

                        var args = value.Nodes.FindAll(it => it.Token.Type == TokenType.Argument);

                        var arglist = new List<Expression>();
                        var argtypes = new List<Type>();
                        foreach (var a in args)
                        {
                            var e = HandleArgument(a, src, ref func);
                            arglist.Add(e);
                            argtypes.Add(e.Type);
                        }

                        if (arglist.Count > 0)
                        {
                            meth = nam2.Type.GetMethod(call, argtypes.ToArray());
                            return Expression.Call(nam2, meth, arglist);
                        }
                        else
                        {
                            meth = nam2.Type.GetMethod(call);
                            return Expression.Call(nam2, meth);
                        }
                    }
                    else
                    {
                        //STATIC method call.
                        throw new NotImplementedException("Do not know how to implement access to static methods on the DLR.");
                    }
                }
            }
            else
            {
                //resolve .net type. field or property
                //throw new NotImplementedException(".NET Type Resolution.");

                var setright = value.Nodes.Find(it => it.Token.Type == TokenType.EQUAL || it.Token.Type == TokenType.ARROW);

                string type = funccall.Token.Text;

                string ident = type.Substring(0,
                    type.LastIndexOf('.'));
                string field = type.Substring(type.LastIndexOf('.') + 1);

                var nam = GetVariable(ident, ref func, src, true, false, iscompiler);

                if (nam != null)
                {
                    //Property or field from a variable.

                    if (setright == null)
                        return Expression.PropertyOrField(nam, field); //Get the value
                    else
                    {
                        switch (setright.Token.Type)
                        {
                            case TokenType.EQUAL:
                                {
                                    //Non functions.
                                    Expression exp = null;
                                    var right = value.Nodes[value.Nodes.Count - 1];
                                    switch (right.Token.Type)
                                    {
                                        case TokenType.Argument:
                                            {
                                                exp = HandleArgument(right, src, ref func);
                                                break;
                                            }
                                    }
                                    return Expression.Assign(
                                        Expression.PropertyOrField(nam, field),
                                        exp);
                                }
                            case TokenType.ARROW:
                                {
                                    //functions
                                    break;
                                }
                        }
                    }
                }
                else
                {
                    //Static type property/field
                    throw new NotImplementedException("Do not know how to implement access to static property/fields on the DLR.");
                }


            }

            return null;
        }
        private static Expression HandleFunc(ParseNode value, ref LambdaBuilder func)
        {
            var inner = value.Nodes[0];
            var name = value.Parent.Nodes[1];
            var param = value.Nodes.FindAll(it => it.Token.Type == TokenType.Parameter);
            var stmts = value.Nodes.Find(it => it.Token.Type == TokenType.Statement || it.Token.Type == TokenType.Statements);

            var meth = AstUtils.Lambda(typeof(object), name.Token.Text);

            foreach (ParseNode par in param)
                HandleParameter(par, ref meth);

            var locs = new List<ParameterExpression>();

            if (stmts.Token.Type == TokenType.Statement)
                meth.Body = HandleStmt(stmts.Nodes[0], ref meth, ref locs, "", null);
            else
            {
                var lab = Expression.Label(meth.ReturnType, "Return" + meth.Name);
                var st = new List<Expression>();
                foreach (var s in stmts.Nodes)
                {
                    var val = HandleStmt(s.Nodes[0], ref meth, ref locs, "", @lab);
                    st.Add((val == null ? Expression.Empty() : val));
                }

                st.Add(Expression.Label(@lab, Expression.Default(@lab.Type)));
                //st.Add(Expression.Return(lab));
                meth.Body = Expression.Block(@lab.Type, st);
            }


            return meth.MakeLambda();
        }
        private static ParameterExpression HandleParameter(ParseNode value, ref LambdaBuilder func)
        {
            var type = value.Nodes.Find(it => it.Token.Type == TokenType.TYPE);
            var parname = value.Nodes.Find(it => it.Token.Type == TokenType.IDENTIFIER);
            if (type == null)
                return func.Parameter(
                    typeof(object),
                    parname.Token.Text);
            else
                return func.Parameter(
                    DLRTypeResolver.ResolveType(type.Token.Text),
                    parname.Token.Text);
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
        private static Expression HandleArgument(ParseNode value, string src, ref LambdaBuilder func, bool iscompiler = true) { Type t = null; return HandleArgument(value, src, ref func, out t, iscompiler); }
        private static Expression HandleArgument(ParseNode value, string src, ref LambdaBuilder func, out Type ty, bool iscompiler = true)
        {

            ParseNode inner = null;
            if (value.Token.Type != TokenType.Argument && value.Token.Type != TokenType.NonArrayArgument)
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
                        if (value.Nodes.Find(it => it.Token.Type == TokenType.ArrayIndexExpr) == null)
                        {
                            //If not an array value.

                            exp = GetVariable(inner, ref func, src, false, true, iscompiler);

                            ty = ((ConstantExpression)((ConstantExpression)exp).Value).Type;
                        }
                        else
                        {
                            //TODO: Implement getting array values.
                            var vari = GetVariable(inner, ref func, src);
                            var index = value.Nodes.Find(it => it.Token.Type == TokenType.ArrayIndexExpr);

                            var indexer = HandleArgument(
                                index.Nodes[1],
                                src, ref func);
                            exp = Expression.ArrayIndex(vari,
                                   indexer);

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
            var left = pn.Nodes.FindAll(it => it.Token.Type != TokenType.ArrayIndexExpr && it != op)[0];
            var right = pn.Nodes.FindAll(it => it.Token.Type != TokenType.ArrayIndexExpr && it != op)[1];

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
                    lexp = GetVariable(left, ref func, "");
                    if (lexp.Type.IsArray)
                    {
                        var larr = pn.Nodes.FindAll(it => it.Token.Type == TokenType.ArrayIndexExpr && it != op)[0];
                        var len = larr.Nodes[1];
                        var le = HandleArgument(len, "", ref func);

                        lexp = Expression.ArrayAccess(lexp,
                            le);
                    }
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
                    rexp = GetVariable(right, ref func, "");
                    if (rexp.Type.IsArray)
                    {
                        var rarr = pn.Nodes.FindAll(it => it.Token.Type == TokenType.ArrayIndexExpr && it != op)[1];
                        var ren = rarr.Nodes[1];
                        var re = HandleArgument(ren, "", ref func);

                        rexp = Expression.ArrayAccess(rexp,
                            re);
                    }
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
        private static DLRASTBuildException VariableDoesntExist(ParseNode pn, string src) { var cord = pn.GetLineAndCol(src); return VariableDoesntExist(pn.Token.Text, src, cord); }
        private static DLRASTBuildException VariableDoesntExist(string pn, string src, LineColObj cord)
        {
            return new DLRASTBuildException(
                                    String.Format("The '{0}' variable doesn't exist in this scope!", pn)
                                    , cord.Line, cord.Col);
        }
        private static Expression GetVariable(ParseNode pn, ref LambdaBuilder func, string src = "", bool justlocal = false, bool throwErr = true, bool iscompiler = true)
        {
            return GetVariable(pn.Token.Text, ref func, src, justlocal, throwErr, iscompiler);
        }
        private static Expression GetVariable(string pn, ref LambdaBuilder func, string src = "", bool justlocal = false, bool throwErr = true, bool iscompiler = true)
        {
            if (iscompiler)
            {
                var exp = func.Locals.Find(it => it.Name == pn);

                if (!justlocal)
                    exp = (exp == null ? func.Parameters.Find(it => it.Name == pn) : exp);

                if (exp == null)
                    if (throwErr)
                        throw VariableDoesntExist(pn, src, new LineColObj() { Line = 0, Col = 0 });
                    else
                        return null;
                else
                    return exp;
            }
            else
            {
                dynamic val = MizuLanguageContext.Instance.ScopeGetVariable(MizuLanguageContext.GlobalScope, pn);
                var typ = val.Type;
                return Expression.Constant(val);
            }

        }
    }
}
