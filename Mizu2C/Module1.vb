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

                If code.Length = 0 Then
                    Console.Error.WriteLine("Source code file cannot be empty.")
                    Return
                End If

                Dim tree = parser.Parse(code)

                If tree.Errors.Count > 0 Then
                    For Each Err As ParseError In tree.Errors
                        Console.Error.WriteLine("[{0},{1}] Error: {2}", Err.Line, Err.Position, Err.Message)
                    Next
                    Return
                Else
                    Dim input As New FileInfo(args(0)), output As New FileInfo(args(1))
                    Compile(input, output, tree)
                End If
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
    Public Code As String = Nothing
    Public Doc As System.Diagnostics.SymbolStore.ISymbolDocumentWriter
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
        Dim entrypoint = tb.DefineMethod("Main", MethodAttributes.Public And MethodAttributes.Static) 'Makes the main method.

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


        'if (!IsInvalid) ILgen.Emit(OpCodes.Ret); //Finishes the statement by calling return. If a invalid exe is wanted, it omits this statement.

        ab.SetEntryPoint(entrypoint, PEFileKinds.ConsoleApplication) 'Sets entry point

        Dim finishedtype = tb.CreateType() 'Compile the type

        ab.Save(output.Name) 'Save
        Return True
    End Function
    Public Sub HandleStatement(ByVal stmt As ParseNode, ByVal ILgen As ILGenerator, ByRef locals As List(Of LocalBuilderEx), ByRef err As Boolean)
        Select Case stmt.Token.Type
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

                        Select Case value.Token.Type
                            Case TokenType.IDENTIFIER
                                Dim idnt = locals.Find(Function(it) it.VariableName = value.Token.Text)

                                If idnt Is Nothing Then
                                    err = True
                                    Console.Error.WriteLine("Error: Variable '{0}' doesn't exist in this context.", value.Token.Text)
                                    Return
                                End If

                                local.VariableType = idnt.VariableType

                                ILgen.Emit(OpCodes.Ldloc, idnt.BaseLocal)
                                Exit Select
                            Case TokenType.NUMBER
                                local.VariableType = GetType(Integer)

                                ILgen.Emit(OpCodes.Ldc_I4, Integer.Parse(value.Token.Text))
                                Exit Select
                            Case TokenType.FLOAT
                                local.VariableType = GetType(Single)

                                ILgen.Emit(OpCodes.Ldc_R4, Single.Parse(value.Token.Text))
                                Exit Select
                            Case TokenType.NULLKW
                                local.VariableType = Nothing

                                ILgen.Emit(OpCodes.Ldnull)
                                Exit Select
                            Case TokenType.STRING
                                local.VariableType = GetType(String)

                                Dim str As String = value.Token.Text
                                str = str.Substring(1)
                                str = str.Remove(str.Length - 1)

                                ILgen.Emit(OpCodes.Ldstr, value.Token.Text)
                                Exit Select
                        End Select

                        local.BaseLocal = ILgen.DeclareLocal(local.VariableType)

                        If (IsDebug) Then
                            local.BaseLocal.SetLocalSymInfo(stmt.Token.Text) 'Set variable name for debug info.
                        End If

                        ILgen.Emit(OpCodes.Stloc, local.BaseLocal)

                        locals.Add(local)
                        Return
                End Select
                Return
            Case TokenType.AS
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
End Module
