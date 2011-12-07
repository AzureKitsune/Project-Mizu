// -----------------------------------------------------------------------
// <copyright file="MizuGetMemberBinder.cs" company="">
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
    public class MizuGetMemberBinder :GetMemberBinder
    {
        public MizuGetMemberBinder(string name, bool ignoreCase): base(name,ignoreCase)
        {}
        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            //return new MizuDefaultBinder().GetMember(target.Value);
            return null;
        }
    }
}
