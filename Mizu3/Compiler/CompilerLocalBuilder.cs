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
        public CompilerLocalBuilder(string name,ILGenerator gen,Type ty,int startoff,int endoff)
        {
            local = gen.DeclareLocal(ty);
            local.SetLocalSymInfo(name, startoff, endoff);
            
        }

        #region IEquatable<LocalBuilder> Members

        public bool Equals(LocalBuilder other)
        {
            return local == other;
        }

        #endregion

        public LocalBuilder Local { get { return local; } }
    }
}
