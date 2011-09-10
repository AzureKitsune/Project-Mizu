Imports Mizu2.Parser
Imports System.Dynamic
Imports System.Reflection
Imports System.Reflection.Emit
Imports System.IO

Module Module1

    Sub Main(ByVal args As String())
        If args.Count >= 2 Then
            If IO.File.Exists(args(0)) = True Then
                Dim scanner As New Scanner
                Dim parser As New Parser(scanner)

                Code = IO.File.ReadAllText(args(0))

                If Code.Length = 0 Then
                    Console.Error.WriteLine("Source code file cannot be empty.")
                    Return
                End If

                Dim tree = parser.Parse(Code)

                tree.Eval()

                If tree.Errors.Count > 0 Then
                    For Each Err As ParseError In tree.Errors
                        Console.Error.WriteLine("[{0},{1}] Error: {2}", Err.Line + 1, Err.Position, Err.Message)
                    Next
                    If ForceCompile = False Then Return
                End If

                If args.Count > 2 Then
                    For i As Integer = 2 To args.Count - 1
                        Select Case args(i).ToLower
                            Case "/debug" : IsDebug = True
                            Case "/force" : ForceCompile = True
                        End Select
                    Next
                End If

                Dim input As New FileInfo(args(0)), output As New FileInfo(args(1))
                Compile(input, output, tree)

            Else
                Console.Error.WriteLine("File doesn't exist!")
                Return
            End If
        Else
            Console.Error.WriteLine("Not enough parameters.")
            Return
        End If
    End Sub
    Public IsDebug As Boolean = False
    Public ForceCompile As Boolean = True
    Public Code As String = Nothing
    Public Doc As System.Diagnostics.SymbolStore.ISymbolDocumentWriter
    Public Namespaces As New List(Of String)
    Public Function Compile(ByVal input As FileInfo, output As FileInfo, ByVal tree As ParseTree) As Boolean
        'The majority of this was ported from the first Mizu (Mizu Concept 1).

        Dim statements = tree.Nodes(0)

        'Declares the assembly and the entypoint
        Dim name = New AssemblyName(output.Name.Replace(output.Extension, ""))
        Dim ab As AssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Save, output.DirectoryName)

        If (IsDebug) Then

            'Make assembly debug-able.
            Dim debugattr = GetType(DebuggableAttribute)
            Dim db_const = debugattr.GetConstructor(New Type() {GetType(DebuggableAttribute.DebuggingModes)})
            Dim db_builder = New CustomAttributeBuilder(db_const, New Object() {DebuggableAttribute.DebuggingModes.DisableOptimizations Or
            DebuggableAttribute.DebuggingModes.Default})

            ab.SetCustomAttribute(db_builder)
        End If

        ' Defines main module.
        Dim mb = ab.DefineDynamicModule(name.Name, name.Name + ".exe", IsDebug)

        If (IsDebug) Then
            'Define the source code file.
            Doc = mb.DefineDocument(input.FullName, Guid.Empty, Guid.Empty, Guid.Empty)
        End If

        Dim tb = mb.DefineType("App") 'Defines main type.
        Dim entrypoint = tb.DefineMethod("Main", MethodAttributes.Public + MethodAttributes.Static) 'Makes the main method.

        Dim ILgen = entrypoint.GetILGenerator(3072) 'gets the IL generator

        Dim locals As New List(Of LocalBuilderEx) 'A list to hold variables.

        ILgen.BeginExceptionBlock() 'Start a try statement.

        ILgen.BeginScope()

        ' Generate body IL
        Dim err = False
        For Each statement As ParseNode In statements.Nodes
            'Iterate though the statements, generating IL.
            Dim basestmt = statement.Nodes(0)
            HandleStatement(basestmt, ILgen, locals, err)
            If (err = True) Then
                Return False
            End If
        Next

        ILgen.EndScope()

        ILgen.BeginCatchBlock(GetType(Exception))

        ILgen.Emit(OpCodes.Callvirt, GetType(Exception).GetMethod("ToString"))

        ILgen.Emit(OpCodes.Call, GetType(Console).GetMethod("WriteLine", New Type() {GetType(String)}))

        ILgen.EndExceptionBlock()  'Ends the catch section.


        ILgen.Emit(OpCodes.Ret) 'Finishes the statement by calling return.

        ab.SetEntryPoint(entrypoint, PEFileKinds.ConsoleApplication) 'Sets entry point

        Dim finishedtype = tb.CreateType() 'Compile the type

        ab.Save(output.Name) 'Save
        Return True
    End Function
    Public Sub HandleExprAsAssignment(ByVal expr As ParseNode, ByVal ILgen As ILGenerator, ByVal locals As List(Of LocalBuilderEx), ByRef err As Boolean)

    End Sub
    Public Sub HandleExprAsBoolean(ByVal expr As ParseNode, ByVal ILgen As ILGenerator, ByVal locals As List(Of LocalBuilderEx), ByRef err As Boolean)
        Dim left As ParseNode = expr.Nodes(0)
        Dim middle As ParseNode = expr.Nodes(1) 'The operator
        If middle.Token.Type = TokenType.WHITESPACE Then middle = expr.Nodes(2)

        Dim right As ParseNode = expr.Nodes(3)
        If right.Token.Type = TokenType.WHITESPACE Then right = expr.Nodes(4)

        LoadToken(ILgen, left, locals, err)
        If err Then Return

        LoadToken(ILgen, right, locals, err)
        If err Then Return

        LoadOperator(middle, ILgen)
    End Sub
    Public Function HandleFunctionCall(ByVal stmt As ParseNode, ByVal ILgen As ILGenerator, ByRef locals As List(Of LocalBuilderEx), ByRef err As Boolean, Optional ByRef returnval As Object = Nothing) As Boolean
        Dim returnsvalues As Boolean = False

        Dim params As ParseNode() = Nothing
        Dim usedident As Boolean = False
        Dim ident As LocalBuilderEx = Nothing
        Dim func = TypeResolver.ResolveFunctionFromParseNode(stmt, locals, params, usedident, ident)

        returnval = func.ReturnType
        returnsvalues = func.ReturnType <> GetType(Void) 'If it doesn't equal Void, it returns a useable value.

        If usedident = False Then

            For Each param In params 'If any parameters, load them.
                LoadToken(ILgen, param, locals, err)

                If err Then Return False
            Next

            ILgen.Emit(OpCodes.Call, func)

            Return returnsvalues
        Else

            ILgen.Emit(OpCodes.Ldloca, ident.BaseLocal)
            'ILgen.Emit(OpCodes.Box, ident.VariableType)
            ILgen.Emit(OpCodes.Call, func)

            Return returnsvalues
        End If
    End Function
    Public Function HandleTypeResolveFromGetType(assembly As Assembly, str As String, bool As Boolean) As Type
        Return TypeResolver.TypeResolverFromType_GetType(assembly, str, bool)
    End Function
    Public Sub HandleForStatement(ByVal stmt As ParseNode, ByVal ILgen As ILGenerator, ByRef locals As List(Of LocalBuilderEx), ByRef err As Boolean)
        Dim forbit As ParseNode = Nothing
        Dim forbody_lab As Label = ILgen.DefineLabel()
        Dim endofstmt As Label = ILgen.DefineLabel()
        Dim forbody As ParseNode = stmt.Nodes.Find(Function(it) it.Token.Type = TokenType.ForStmtBODY)

        If stmt.Nodes(1).Token.Type = TokenType.WHITESPACE Then forbit = stmt.Nodes(3) Else forbit = stmt.Nodes(2)

        Select Case forbit.Token.Type
            Case TokenType.ForEachStmt

                Exit Select
            Case TokenType.ForIterStmt
                Exit Select
        End Select

        Dim tmp_locs As New List(Of LocalBuilderEx)
        locals.ForEach(Sub(it) tmp_locs.Add(it))

        ILgen.MarkLabel(forbody_lab)

        For Each stmt In forbody.Nodes
            HandleStatement(stmt, ILgen, tmp_locs, err)
        Next

        ILgen.MarkLabel(endofstmt)
    End Sub
    Public Sub HandleStatement(ByVal stmt As ParseNode, ByVal ILgen As ILGenerator, ByRef locals As List(Of LocalBuilderEx), ByRef err As Boolean)
        Select Case stmt.Token.Type
            Case TokenType.ForStatement
                HandleForStatement(stmt, ILgen, locals, err)
                Return
            Case TokenType.UsesStatement
                'Dim t As Type = Type.GetType(stmt.Nodes(2).Token.Text, Nothing, New System.Func(Of Assembly, String, Boolean, Type)(AddressOf HandleTypeResolveFromGetType), False, False)
                Dim ns As String = stmt.Nodes(2).Token.Text

                If Not Namespaces.Contains(ns) Then Namespaces.Add(ns)
                If (IsDebug) Then

                    Dim sline = 0, scol = 0

                    FindLineAndCol(Code, stmt.Token.StartPos, sline, scol)

                    Dim eline = 0, ecol = 0

                    FindLineAndCol(Code, stmt.Token.EndPos, eline, ecol)

                    ILgen.MarkSequencePoint(Doc, sline, scol, eline, ecol)
                End If

                Return
            Case TokenType.FuncCall
                If (IsDebug) Then

                    Dim sline = 0, scol = 0

                    FindLineAndCol(Code, stmt.Token.StartPos, sline, scol)

                    Dim eline = 0, ecol = 0

                    FindLineAndCol(Code, stmt.Token.EndPos, eline, ecol)

                    ILgen.MarkSequencePoint(Doc, sline, scol, eline, ecol)
                End If

                Dim returns = HandleFunctionCall(stmt, ILgen, locals, err)
                If returns = True Then
                    ILgen.Emit(OpCodes.Pop) 'Discard the value because in this context, we don't care about it.
                End If
                Return
            Case TokenType.IfStatement
                ''TODO: Handle Else statements.
                Dim expr As ParseNode = stmt.Nodes(2)
                Dim ifbody As ParseNode = stmt.Nodes.Find(Function(it) it.Token.Type = TokenType.IfStmtIFBody)
                Dim ifbodylabel As Label = ILgen.DefineLabel()
                Dim endofstmt As Label = ILgen.DefineLabel()

                'Handle the expression
                HandleExprAsBoolean(expr, ILgen, locals, err)
                If err Then Return

                ILgen.Emit(OpCodes.Brtrue, ifbodylabel)
                ILgen.Emit(OpCodes.Br, endofstmt) 'Otherwise, skip the method

                If (IsDebug) Then

                    Dim sline = 0, scol = 0

                    FindLineAndCol(Code, expr.Token.StartPos, sline, scol)

                    Dim eline = 0, ecol = 0

                    FindLineAndCol(Code, expr.Token.EndPos, eline, ecol)

                    ILgen.MarkSequencePoint(Doc, sline, scol, eline, ecol)
                End If

                ILgen.BeginScope()

                ILgen.MarkLabel(ifbodylabel)

                ''Handle inner statements here.

                Dim tmp_locals As New List(Of LocalBuilderEx) 'Create a temp list for local variables inside of the if statement.
                locals.ForEach(Sub(it) tmp_locals.Add(it)) 'Add global variables into the temp list.

                For Each ifstmt In ifbody.Nodes
                    HandleStatement(ifstmt.Nodes(0), ILgen, tmp_locals, err)

                    If err = True Then Return
                Next
                ILgen.Emit(OpCodes.Br, endofstmt)


                ILgen.MarkLabel(endofstmt)
                ILgen.EndScope()

                Return
            Case TokenType.VariableAssignment

                If (IsDebug) Then

                    Dim sline = 0, scol = 0

                    FindLineAndCol(Code, stmt.Token.StartPos, sline, scol)

                    Dim eline = 0, ecol = 0

                    FindLineAndCol(Code, stmt.Token.EndPos, eline, ecol)

                    ILgen.MarkSequencePoint(Doc, sline, scol, eline, ecol)
                End If


                Dim local As New LocalBuilderEx()

                Dim loc As ParseNode = stmt.Nodes(2)
                Dim name As String = loc.Nodes(0).Token.Text
                Dim value As ParseNode = loc.Nodes(loc.Nodes.Count - 1)

                Select Case loc.Nodes(2).Token.Type
                    Case TokenType.EQUAL
                        'var x = bla
                        'Use AS instead of = for declaring .NET objects

                        local.VariableName = name

                        LoadToken(ILgen, value, locals, err, local)
                        If err = True Then Return

                        If local.VariableType = Nothing Then
                            local.VariableType = GetType(Object) 'Just set it as object
                        End If
                        local.BaseLocal = ILgen.DeclareLocal(local.VariableType)

                        If (IsDebug) Then
                            local.BaseLocal.SetLocalSymInfo(name, loc.Nodes(0).Token.StartPos, loc.Nodes(0).Token.EndPos) 'Set variable name for debug info.
                        End If

                        ILgen.Emit(OpCodes.Stloc, local.BaseLocal)

                        locals.Add(local)
                        Return
                        Return
                    Case TokenType.AS


                        Dim typename As String = loc.Nodes(6).Nodes(0).Token.Text

                        Dim constrs As ParseNode() = loc.Nodes(6).Nodes.GetRange(1, loc.Nodes(6).Nodes.Count - 1).ToArray()
                        constrs = Array.FindAll(constrs, Function(it) it.Token.Type <> TokenType.BROPEN And it.Token.Type <> TokenType.BRCLOSE)

                        Dim objType As Type = TypeResolver.ResolveType(typename)

                        local.VariableType = objType
                        local.BaseLocal = ILgen.DeclareLocal(local.VariableType)

                        If (IsDebug) Then
                            local.BaseLocal.SetLocalSymInfo(name, loc.Nodes(0).Token.StartPos, loc.Nodes(0).Token.EndPos) 'Set variable name for debug info.
                        End If

                        local.VariableName = name

                        If constrs.Length = 0 Then
                            Dim constrInfo As ConstructorInfo = Nothing
                            constrInfo = objType.GetConstructor(New Type() {})


                            ILgen.Emit(OpCodes.Newobj, constrInfo)
                            ILgen.Emit(OpCodes.Stloc, local.BaseLocal)
                        ElseIf constrs.Length > 0 Then

                            '' Unfinished
                            Dim constrInfo As ConstructorInfo = Nothing
                            For Each constrItem As ParseNode In constrs
                                LoadToken(ILgen, constrItem, locals, err)
                                If err = True Then Return
                            Next


                            ILgen.Emit(OpCodes.Newobj, constrInfo)
                            ILgen.Emit(OpCodes.Stloc, local.BaseLocal)
                        Else
                            Console.Error.WriteLine("Error: Invalid amount of parameters.")
                            err = True
                            Return
                        End If
                        locals.Add(local)
                        Return

                End Select
        End Select
    End Sub
    Private Sub LoadToken(ByVal ILgen As ILGenerator, ByVal value As ParseNode, ByRef locals As List(Of LocalBuilderEx), ByRef Err As Boolean, Optional ByRef local As LocalBuilderEx = Nothing)
        Select Case value.Token.Type
            Case TokenType.IDENTIFIER
                Dim idnt = locals.Find(Function(it) it.VariableName = value.Token.Text)

                If idnt Is Nothing Then
                    Err = True
                    Console.Error.WriteLine("Error: Variable '{0}' doesn't exist in this context.", value.Token.Text)
                    Return
                End If

                If Not local Is Nothing Then
                    local.VariableType = idnt.VariableType
                End If

                ILgen.Emit(OpCodes.Ldloc, idnt.BaseLocal)
                Exit Select
            Case TokenType.NUMBER
                If Not local Is Nothing Then local.VariableType = GetType(Integer)

                ILgen.Emit(OpCodes.Ldc_I4, Integer.Parse(value.Token.Text))
                Exit Select
            Case TokenType.FLOAT
                If Not local Is Nothing Then local.VariableType = GetType(Single)

                ILgen.Emit(OpCodes.Ldc_R4, Single.Parse(value.Token.Text))
                Exit Select
            Case TokenType.NULLKW
                If Not local Is Nothing Then local.VariableType = Nothing

                ILgen.Emit(OpCodes.Ldnull)
                Exit Select
            Case TokenType.STRING
                If Not local Is Nothing Then local.VariableType = GetType(String)

                Dim str As String = value.Token.Text
                'trims first leading and trailing quote marks.
                str = str.Substring(1)
                str = str.Remove(str.Length - 1)

                ILgen.Emit(OpCodes.Ldstr, str)
                Exit Select
            Case TokenType.FuncCall
                Dim rt As Type = Nothing
                HandleFunctionCall(value, ILgen, locals, Err, rt)
                If Not local Is Nothing Then local.VariableType = rt
                Exit Select
            Case TokenType.MathExpr
                If Not local Is Nothing Then local.VariableType = GetType(Integer)
                HandleMathExpr(value, ILgen, locals, Err)
                Exit Select
            Case Else
                'If all else fails, declare it as a regular object.

                If Not local Is Nothing Then local.VariableType = GetType(Object)
                ILgen.Emit(OpCodes.Initobj, GetType(Object))
                Exit Select
        End Select
    End Sub
    Private Sub LoadOperator(ByVal op As ParseNode, ByVal ILgen As ILGenerator)
        Select Case op.Token.Type
            Case TokenType.EQUAL
                ILgen.Emit(OpCodes.Ceq)
                Return
            Case TokenType.GT
                ILgen.Emit(OpCodes.Cgt)
                Return
            Case TokenType.LT
                ILgen.Emit(OpCodes.Clt)
                Return
            Case TokenType.NOTEQUAL
                ILgen.Emit(OpCodes.Ceq)
                ILgen.Emit(OpCodes.Ldc_I4_0)
                ILgen.Emit(OpCodes.Ceq)
                Return
            Case TokenType.PLUS
                ILgen.Emit(OpCodes.Add)
                Return
            Case TokenType.MINUS
                ILgen.Emit(OpCodes.Sub)
                Return
            Case TokenType.MULTI
                ILgen.Emit(OpCodes.Mul)
                Return
            Case TokenType.DIV
                ILgen.Emit(OpCodes.Div)
                Return
        End Select
    End Sub
    Private Function GetLineAndCol(ByVal src As String, ByVal pos As Integer) As Object
        Dim line = 0, col = 0

        Dim eo As New Object
        FindLineAndCol(src, pos, line, col)

        eo.Line = line
        eo.Col = col
        Return eo
    End Function
    Private Sub FindLineAndCol(ByVal src As String, ByVal pos As Integer, ByRef line As Integer, col As Integer)
        ' http://www.codeproject.com/Messages/3852786/Re-ParseError-line-numbers-always-0.aspx

        line = 1
        col = 0

        For i As Integer = 0 To pos
            If (src(i) = vbNewLine) Then
                line += 1
                col = 1
            Else
                col += 1
            End If
        Next
    End Sub

    Private Sub HandleMathExpr(expr As ParseNode, ILgen As ILGenerator, locals As List(Of LocalBuilderEx), Err As Boolean)
        Dim expr1 As ParseNode = Nothing, expr2 As ParseNode = Nothing, op As ParseNode = Nothing
        Dim expr1_ind As Integer = 0

        If expr.Nodes(1).Token.Type = TokenType.WHITESPACE Then
            expr1 = expr.Nodes(2)
            expr1_ind = 2
        Else
            expr1 = expr.Nodes(1)
            expr1_ind = 1
        End If

        expr2 = expr.Nodes(expr1_ind + 2)

        op = expr.Nodes(expr.Nodes.Count - 2).Nodes(0)

        LoadToken(ILgen, expr1, locals, Err)
        LoadToken(ILgen, expr2, locals, Err)
        LoadOperator(op, ILgen)

    End Sub

End Module
