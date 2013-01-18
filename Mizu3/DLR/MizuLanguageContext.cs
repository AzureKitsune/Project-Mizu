// -----------------------------------------------------------------------
// <copyright file="MizuLanguageContext.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Scripting.Runtime;
    using System.Linq.Expressions;
    using AstUtils = Microsoft.Scripting.Ast.Utils;
    using System.Dynamic;
    using Microsoft.Scripting.Ast;
    using Mizu3.DLR.Binders;
    using Microsoft.Scripting.Hosting;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuLanguageContext : LanguageContext
    {
        public const string ScopeVarName = "scope";

        public static Scope GlobalScope { get; private set; }

        public MizuLanguageContext(ScriptDomainManager sc, IDictionary<string, object> options)
            : base(sc)
        {
            if (Instance == null)
            {
                Instance = this;
                GlobalScope = new Scope();
            }
            else
                throw new Exception();
        }
        public override Microsoft.Scripting.ScriptCode CompileSourceCode(Microsoft.Scripting.SourceUnit sourceUnit, Microsoft.Scripting.CompilerOptions options, Microsoft.Scripting.ErrorSink errorSink)
        {

            //var lamb = AstUtils.Lambda(typeof(object), "exec");

            LambdaBuilder func = AstUtils.Lambda(typeof(void), "exec");

            if (GlobalScriptScope != null)
                foreach (var name in GlobalScriptScope.GetVariableNames())
                {
                    var vari = GlobalScriptScope.GetItems().First(x => x.Key == name);
                    func.Locals.Add(Expression.Variable(vari.Value.GetType(), vari.Key));
                }

            var ast = Expression.Block(DLRASTBuilder.Parse(sourceUnit.GetCode(), ref func));

            LambdaExpression lamb = null;

            if (ast.Type == typeof(void))
                lamb = Expression.Lambda<Action<IDynamicMetaObjectProvider>>(ast, Expression.Parameter(typeof(IDynamicMetaObjectProvider), ScopeVarName));
            else
                lamb = Expression.Lambda<Func<IDynamicMetaObjectProvider, object>>(ast, Expression.Parameter(typeof(IDynamicMetaObjectProvider), ScopeVarName));

            return new MizuScriptCode(
                lamb, sourceUnit, func);
        }

        public override int ExecuteProgram(Microsoft.Scripting.SourceUnit program)
        {
            return base.ExecuteProgram(program);
        }

        public override System.Dynamic.SetMemberBinder CreateSetMemberBinder(string name, bool ignoreCase)
        {
            return new MizuSetMemberBinder(name);
        }

        public override void ScopeSetVariable(Scope scope, string name, object value)
        {
            base.ScopeSetVariable(scope, name, value);
        }

        public override bool ScopeTryGetVariable(Scope scope, string name, out dynamic value)
        {
            return base.ScopeTryGetVariable(scope, name, out value);
        }


        public override InvokeMemberBinder CreateCallBinder(string name, bool ignoreCase, CallInfo callInfo)
        {
            return base.CreateCallBinder(name, ignoreCase, callInfo);
        }

        public override ScopeExtension CreateScopeExtension(Scope scope)
        {
            return base.CreateScopeExtension(scope);
        }

        public static LanguageContext Instance { get; private set; }

        public static ScriptScope GlobalScriptScope { get; internal set; }
    }
}
