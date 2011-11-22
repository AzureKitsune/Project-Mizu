// -----------------------------------------------------------------------
// <copyright file="CompilerLocalBuilder.cs" company="">
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
    using Mizu3.Parser;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class CompilerLocalBuilder : IEquatable<LocalBuilder>
    {
        private LocalBuilder local = null;
        private string name = null;
        public CompilerLocalBuilder(string pname,ILGenerator gen,Type ty, CompilerParameters info,string src = null, ParseNode pn = null)
        {
            local = gen.DeclareLocal(ty);

            if(info.IsDebugMode)
                if (src == null || pn == null)
                    local.SetLocalSymInfo(pname);
                else
                {
                    var scope = pn.Parent.Parent.Parent;
                    local.SetLocalSymInfo(pname, pn.Token.StartPos, scope.Token.EndPos);
                }

            this.name = pname;
        }

        #region IEquatable<LocalBuilder> Members

        public bool Equals(LocalBuilder other)
        {
            return local == other;
        }

        #endregion

        public LocalBuilder Local { get { return local; } }

        public int LocalIndex { get { return local.LocalIndex; } }
        public Type LocalType { get { return local.LocalType; } }

        public string Name { get { return name; } }
    }
}
