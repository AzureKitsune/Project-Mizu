Imports Microsoft.VisualStudio.TestTools.UnitTesting

Imports Mizu2C



'''<summary>
'''This is a test class for Module1Test and is intended
'''to contain all Module1Test Unit Tests
'''</summary>
<TestClass()> _
Public Class ScriptTest


    Private testContextInstance As TestContext

    '''<summary>
    '''Gets or sets the test context which provides
    '''information about and functionality for the current test run.
    '''</summary>
    Public Property TestContext() As TestContext
        Get
            Return testContextInstance
        End Get
        Set(value As TestContext)
            testContextInstance = Value
        End Set
    End Property

#Region "Additional test attributes"
    '
    'You can use the following additional attributes as you write your tests:
    '
    'Use ClassInitialize to run code before running the first test in the class
    '<ClassInitialize()>  _
    'Public Shared Sub MyClassInitialize(ByVal testContext As TestContext)
    'End Sub
    '
    'Use ClassCleanup to run code after all tests in a class have run
    '<ClassCleanup()>  _
    'Public Shared Sub MyClassCleanup()
    'End Sub
    '
    'Use TestInitialize to run code before running each test
    '<TestInitialize()>  _
    'Public Sub MyTestInitialize()
    'End Sub
    '
    'Use TestCleanup to run code after each test has run
    '<TestCleanup()>  _
    'Public Sub MyTestCleanup()
    'End Sub
    '
#End Region


    '''<summary>
    '''A test for the hello world script.
    '''</summary>
    <TestMethod()> _
    Public Sub HelloWorldTest()

        Dim dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\Mizu2\Test\"

        If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

        Dim exe = dir + "HelloWorld.exe"

        Dim script = dir + "HelloWorld.miz"
        IO.File.WriteAllText(script, "System.Console.WriteLine(""Hello World"")")

        If IO.File.Exists(exe) Then IO.File.Delete(exe)

        Dim args() As String = {script, exe} ' TODO: Initialize to an appropriate value
        Module1.Main(args)

        Assert.IsTrue(IO.File.Exists(exe), "Executable was not generated. Maybe it was invalid source code?")

        Dim psi As New ProcessStartInfo(exe)
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.CreateNoWindow = True
        psi.WindowStyle = ProcessWindowStyle.Hidden
        Dim output = Process.Start(psi).StandardOutput.ReadToEnd()
        Dim expected = "Hello World" + vbNewLine
        Assert.IsTrue(String.Equals(output, expected), "Invalid output. Expected: <" + expected + "> Actual: <" + output + ">")
    End Sub
    '''<summary>
    '''A test for the hello world script.
    '''</summary>
    <TestMethod()> _
    Public Sub ForEachTest()

        Dim dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\Mizu2\Test\"

        If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

        Dim exe = dir + "ForEach.exe"

        Dim script = dir + "ForEach.miz"
        IO.File.WriteAllText(script, "uses System" + vbNewLine + "uses System.Collections" + vbNewLine + "var list as new ArrayList()" + vbNewLine + "list.Add(""first""))" + vbNewLine + "list.Add(""second"")" + vbNewLine + "for (var item in list)" + vbNewLine + "{" + vbNewLine + "Console.WriteLine(item.ToString())" + vbNewLine + "}" + vbNewLine)

        If IO.File.Exists(exe) Then IO.File.Delete(exe)

        Dim args() As String = {script, exe, "/force"} ' TODO: Initialize to an appropriate value
        Console.SetError(IO.TextWriter.Null)
        Module1.Main(args)

        Assert.IsTrue(IO.File.Exists(exe), "Executable was not generated. Maybe it was invalid source code?")

        Dim psi As New ProcessStartInfo(exe)
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.CreateNoWindow = True
        psi.WindowStyle = ProcessWindowStyle.Hidden
        Dim output = Nothing
        Dim p = Process.Start(psi)
        p.WaitForExit()
        output.StandardOutput.ReadToEnd()
        Dim expected = "first" + vbNewLine + "second" + vbNewLine
        Assert.IsTrue(String.Equals(output, expected), "Invalid output. Expected: <" + expected + "> Actual: <" + output + ">")
    End Sub

    '''<summary>
    '''A test for a math script.
    '''</summary>
    <TestMethod()> _
    Public Sub MathTest()

        Dim dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\Mizu2\Test\"

        If IO.Directory.Exists(dir) = False Then IO.Directory.CreateDirectory(dir)

        Dim exe = dir + "Math.exe"

        Dim script = dir + "Math.miz"
        IO.File.WriteAllText(script, "var x = (2 (3 3 *) +)" + vbNewLine + "System.Console.WriteLine(""x.ToString()"")" + vbNewLine)

        If IO.File.Exists(exe) Then IO.File.Delete(exe)

        Dim args() As String = {script, exe, "/force"} ' TODO: Initialize to an appropriate value
        Console.SetError(IO.TextWriter.Null)
        Module1.Main(args)

        Assert.IsTrue(IO.File.Exists(exe), "Executable was not generated. Maybe it was invalid source code?")

        Dim psi As New ProcessStartInfo(exe)
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.CreateNoWindow = True
        psi.WindowStyle = ProcessWindowStyle.Hidden
        Dim output = Process.Start(psi).StandardOutput.ReadToEnd()
        Dim expected = 2 + (3 * 3)
        Assert.AreEqual(expected, Integer.Parse(output))
    End Sub
End Class
