using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

namespace Mizu3.DLR.Binders
{
    class MizuCallSiteBinder: CallSiteBinder
    {
        public override System.Linq.Expressions.Expression Bind(object[] args, System.Collections.ObjectModel.ReadOnlyCollection<System.Linq.Expressions.ParameterExpression> parameters, System.Linq.Expressions.LabelTarget returnLabel)
        {
            throw new NotImplementedException();
        }
    }
}
