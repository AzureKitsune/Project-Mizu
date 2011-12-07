// -----------------------------------------------------------------------
// <copyright file="MizuInvokeMemberBinder.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR.Binders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Dynamic;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class MizuInvokeMemberBinder : InvokeMemberBinder
    {
        public MizuInvokeMemberBinder(string name, bool ignoreCase, CallInfo callInfo): base(name,ignoreCase,callInfo)
        {
            
        }
        public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            return null;
        }

        public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }
}
