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

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class CompilerLocalBuilder : IEquatable<LocalBuilder>
    {
        private LocalBuilder local = null;
        private string name = null;
        public CompilerLocalBuilder(string pname,ILGenerator gen,Type ty, CompilerParameters info)
        {
            local = gen.DeclareLocal(ty);

            if(info.IsDebugMode)
                local.SetLocalSymInfo(pname);

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
