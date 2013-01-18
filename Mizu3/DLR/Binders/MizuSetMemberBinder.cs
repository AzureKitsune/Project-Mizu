using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;

namespace Mizu3.DLR.Binders
{
    public class MizuSetMemberBinder: SetMemberBinder
    {
        public MizuSetMemberBinder(string name)
            : base(name, true) {
        }

        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            throw new NotImplementedException();
        }
    }
}
