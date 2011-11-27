// -----------------------------------------------------------------------
// <copyright file="ParseNodeExt.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Mizu3.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class ParseNodeExt
    {

        public static  void FindLineAndCol(int pos, string src, ref int line, ref int col)
        {
            // http://www.codeproject.com/Messages/3852786/Re-ParseError-line-numbers-always-0.aspx


            line = 1;
            col = 0;

            if (src == "")
                return;

            for (int i = 0; i < pos - 1; i++)
            {
                if (src[i] == Environment.NewLine[0])
                {
                    line += 1;
                    col = 1;
                }
                else
                {
                    col += 1;
                }
            }
        }
        public static void FindLineAndCol(this ParseNode pn, string src, ref int line, ref int col)
        {
            FindLineAndCol(pn.Token.StartPos, src, ref line, ref col);
        }
        public static LineColObj GetLineAndCol(this ParseNode pn, string src)
        {
            int line = 0, col = 0;

            LineColObj eo = new LineColObj(); ;
            pn.FindLineAndCol(src, ref line, ref col);

            eo.Line = line;
            eo.Col = col;
            return eo;
        }
        public static LineColObj GetLineAndColEnd(this ParseNode pn, string src)
        {
            int line = 0, col = 0;

            LineColObj eo = new LineColObj(); ;
            FindLineAndCol(pn.Token.EndPos, src, ref line, ref col);

            eo.Line = line;
            eo.Col = col;
            return eo;
        }
    }
    public struct LineColObj
    {
        public int Line { get; set; }
        public int Col { get; set; }
    }
}
