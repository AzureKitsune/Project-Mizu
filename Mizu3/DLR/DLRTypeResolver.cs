// -----------------------------------------------------------------------
// <copyright file="DLRTypeResolver.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.DLR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class DLRTypeResolver: Mizu3.Compiler.TypeResolver
    {
        static readonly string[,] keywords =  new string[,]{  {"int", "System.Int32" },{"str","System.String"}, {"obj","System.Object"} };
        public static Type ResolveType(string type)
        {
            try
            {
                for (int i = 0; i < keywords.GetUpperBound(0); i++)
                {
                    if (type.ToLower() == keywords[i, 0])
                    {
                        type = keywords[i, 1];
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                var ty = Type.GetType(type, true);
                return ty;
            }
            catch (Exception ex)
            {
                throw new DLRASTSyntaxException("The type '" + type + "' doesn't exist!", 0, 0,ex);
            }
        }
    }
}
