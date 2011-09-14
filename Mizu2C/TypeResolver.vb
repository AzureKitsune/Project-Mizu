Imports Mizu2.Parser
Imports System.Reflection

Public Class TypeResolver
    Public Shared Function ResolveType(ByVal name As String) As Type
        Dim obj = Type.GetType(name, False, False) 'Worst case scenario, I'll need to implement my own search.
        '^^ Searches MSCorlib for the type.
        If obj = Nothing Then
            'It did not find the method so try searching through namespaces.
            For Each ns In Namespaces
                obj = Type.GetType(ns + "." + name)
                If obj = Nothing Then
                    Continue For
                Else : Return obj
                End If
            Next
            For Each asm In References
                For Each ns In Namespaces
                    obj = asm.GetType(ns + "." + name)
                    If obj = Nothing Then
                        Continue For
                    Else : Return obj
                    End If
                Next
            Next
        End If
        Return obj
    End Function
    ''

    '' At this point, I'm doing a bad job of keeping things clean.

    Public Shared Function ResolveTypeFromParseNode(ByVal node As ParseNode, Optional ByVal locals As List(Of LocalBuilderEx) = Nothing) As Type
        Select Case node.Token.Type
            Case TokenType.STRING
                Return GetType(String)
            Case TokenType.NUMBER
                Return GetType(Integer)
            Case TokenType.FLOAT
                Return GetType(Single)
            Case TokenType.FuncCall
                Dim method = ResolveFunctionFromParseNode(node, locals)
                Return method.ReturnType
            Case TokenType.Type
                Dim str As String = node.Nodes(0).Token.Text
                Dim obj = Type.GetType(str) 'Type to resolve it from mscorlib

                If obj = Nothing Then
                    'It did not find the method so try searching through namespaces.
                    For Each ns In Namespaces
                        obj = Type.GetType(ns + "." + str)
                        If obj = Nothing Then
                            Continue For
                        Else : Exit For
                        End If
                    Next
                End If
                Return obj
            Case TokenType.IDENTIFIER
                If Not locals Is Nothing Then
                    Dim idnt As LocalBuilderEx = locals.Find(Function(it) it.VariableName = node.Token.Text)
                    If Not idnt Is Nothing Then
                        Return idnt.VariableType
                    Else
                        Throw New Exception
                    End If
                Else : Throw New ArgumentNullException("locals")
                End If
        End Select
        Return Nothing
    End Function
    Public Shared Function ResolveTypesFromParseNodeArray(ByVal nodes As ParseNode(), Optional ByVal locals As List(Of LocalBuilderEx) = Nothing) As Type()
        Dim list As New List(Of Type)

        For Each node In nodes
            list.Add(ResolveTypeFromParseNode(node, locals))
        Next

        Return list.ToArray
    End Function
    Public Shared Function ResolveFunctionFromParseNode(ByVal stmt As ParseNode, ByVal locals As List(Of LocalBuilderEx), Optional ByRef out_params As ParseNode() = Nothing, Optional ByRef out_used_ident As Boolean = False, Optional ByRef ident As LocalBuilderEx = Nothing) As MethodInfo
        Dim text As String = stmt.Nodes(0).Nodes(0).Token.Text

        Dim lastper As Integer = text.LastIndexOf(".")

        Dim classname As String = text.Substring(0, lastper)

        Dim funcname As String = text.Substring(lastper + 1)

        ident = locals.Find(
            Function(it) it.VariableName = classname) 'Variables can be instances of classes. Check to see if its classing a variable instead of a static object instance.

        out_params = stmt.Nodes(1).Nodes.GetRange(0, stmt.Nodes(1).Nodes.Count - 1).FindAll(Function(it) it.Token.Type <> TokenType.WHITESPACE And it.Token.Type <> TokenType.BROPEN And it.Token.Type <> TokenType.BRCLOSE And it.Token.Type <> TokenType.COMMA And it.Token.Type <> TokenType._UNDETERMINED_).ToArray()

        If ident Is Nothing Then
            'Calling a static object instance.
            Dim obj As Type = TypeResolver.ResolveType(classname)

            Try
                Dim func = Nothing
                func = obj.GetMethod(funcname, TypeResolver.ResolveTypesFromParseNodeArray(out_params, locals))
                Return func
            Catch ex As Exception
                Return Nothing
            End Try
        Else
            out_used_ident = True
            Try
                Dim func = ident.VariableType.GetMethod(funcname, TypeResolver.ResolveTypesFromParseNodeArray(out_params, locals))
                Return func
            Catch ex As Exception
                Return Nothing
            End Try
        End If
    End Function
    Public Shared Function ResolvePropertyFromParseNode(ByVal stmt As ParseNode, ByVal locals As List(Of LocalBuilderEx), Optional ByRef out_params As ParseNode() = Nothing, Optional ByRef out_used_ident As Boolean = False, Optional ByRef ident As LocalBuilderEx = Nothing) As PropertyInfo
        Dim text As String = stmt.Nodes(0).Nodes(0).Token.Text

        Dim lastper As Integer = text.LastIndexOf(".")

        Dim classname As String = text.Substring(0, lastper)

        Dim funcname As String = text.Substring(lastper + 1)

        ident = locals.Find(
            Function(it) it.VariableName = classname) 'Variables can be instances of classes. Check to see if its classing a variable instead of a static object instance.

        'out_params = stmt.Nodes(1).Nodes.GetRange(0, stmt.Nodes.Count - 1).FindAll(Function(it) it.Token.Type <> TokenType.WHITESPACE And it.Token.Type <> TokenType.BROPEN And it.Token.Type <> TokenType.BRCLOSE And it.Token.Type <> TokenType.COMMA And it.Token.Type <> TokenType._UNDETERMINED_).ToArray()

        If ident Is Nothing Then
            'Calling a static object instance.
            Dim obj As Type = TypeResolver.ResolveType(classname)

            Try
                Dim func = Nothing
                func = obj.GetProperty(funcname)
                Return func
            Catch ex As Exception : Return Nothing
            End Try
        Else
            out_used_ident = True
            Try
                Dim func = ident.VariableType.GetProperty(funcname)
                Return func
            Catch ex As Exception : Return Nothing
            End Try
        End If
    End Function
    Public Shared Function ResolveEventFromParseNode(ByVal stmt As ParseNode, ByVal locals As List(Of LocalBuilderEx), Optional ByRef out_used_ident As Boolean = False, Optional ByRef ident As LocalBuilderEx = Nothing) As EventInfo
        Dim text As String = stmt.Nodes(2).Nodes(0).Token.Text

        Dim lastper As Integer = text.LastIndexOf(".")

        Dim classname As String = text.Substring(0, lastper)

        Dim funcname As String = text.Substring(lastper + 1)

        ident = locals.Find(
            Function(it) it.VariableName = classname) 'Variables can be instances of classes. Check to see if its classing a variable instead of a static object instance.

        If ident Is Nothing Then
            'Calling a static object instance.
            Dim obj As Type = TypeResolver.ResolveType(classname)

            Try
                Dim func = Nothing
                func = obj.GetEvent(funcname)
                Return func
            Catch ex As Exception : Return Nothing
            End Try
        Else
            out_used_ident = True
            Try
                Dim func = ident.VariableType.GetEvent(funcname)
                Return func
            Catch ex As Exception : Return Nothing
            End Try
        End If
    End Function
    Public Shared Function TypeResolverFromType_GetType(assembly As Assembly, str As String, bool As Boolean) As Type
        Throw New NotImplementedException
    End Function
    Public Shared Function IsValueType(ByVal type As Type) As Boolean
        Return (type Is GetType(String) Or type Is GetType(Integer) Or type Is GetType(Char) Or type Is GetType(Byte))
    End Function
    Public Shared Function ReturnTypeArrayOfCount(ByVal count As Integer, ByVal type As Type) As Type()
        Dim t As New List(Of Type)
        For i As Integer = 0 To count - 1
            t.Add(type)
        Next
        Return t.ToArray
    End Function
End Class
