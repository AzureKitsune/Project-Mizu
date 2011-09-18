Imports System.Runtime.CompilerServices
Imports Mizu2.Parser

Module ParseNodeExt
    Public Sub FindLineAndCol(ByVal pos As Integer, ByVal src As String, ByRef line As Integer, col As Integer)
        ' http://www.codeproject.com/Messages/3852786/Re-ParseError-line-numbers-always-0.aspx

        line = 1
        col = 0

        For i As Integer = 0 To pos - 1
            If (src(i) = Environment.NewLine(0)) Then
                line += 1
                col = 1
            Else
                col += 1
            End If
        Next
    End Sub
    <Extension()> _
    Public Sub FindLineAndCol(ByVal pn As ParseNode, ByVal src As String, ByRef line As Integer, col As Integer)
        FindLineAndCol(pn.Token.StartPos, src, line, col)
    End Sub
    <Extension()> _
    Public Function GetLineAndCol(ByVal pn As ParseNode, ByVal src As String) As LineColObj
        Dim line = 0, col = 0

        Dim eo As LineColObj
        pn.FindLineAndCol(src, line, col)

        eo.Line = line
        eo.Col = col
        Return eo
    End Function
    <Extension()> _
    Public Function GetLineAndColEnd(ByVal pn As ParseNode, ByVal src As String) As LineColObj
        Dim line = 0, col = 0

        Dim eo As LineColObj
        FindLineAndCol(pn.Token.EndPos, src, line, col)

        eo.Line = line
        eo.Col = col
        Return eo
    End Function
End Module
Structure LineColObj
    Public Property Line As Integer
    Public Property Col As Integer
End Structure
