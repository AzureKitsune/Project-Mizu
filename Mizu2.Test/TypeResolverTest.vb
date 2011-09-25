Imports System.Reflection

Imports System.Collections.Generic

Imports Mizu2.Parser

Imports System

Imports Microsoft.VisualStudio.TestTools.UnitTesting

Imports Mizu2C



'''<summary>
'''This is a test class for TypeResolverTest and is intended
'''to contain all TypeResolverTest Unit Tests
'''</summary>
<TestClass()> _
Public Class TypeResolverTest


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
    '''A test for IsNamespaceAvailable
    '''</summary>
    <TestMethod()> _
    Public Sub IsNamespaceAvailableTest()
        Dim ns As String = "System" ' TODO: Initialize to an appropriate value
        Dim expected As Boolean = True ' TODO: Initialize to an appropriate value
        Dim actual As Boolean
        actual = TypeResolver.IsNamespaceAvailable(ns)
        Assert.AreEqual(expected, actual)
        'Assert.Inconclusive("Verify the correctness of this test method.")
    End Sub

    '''<summary>
    '''A test for IsValueType
    '''</summary>
    <TestMethod()> _
    Public Sub IsValueTypeTest()
        Dim type As Type = GetType(Integer) ' TODO: Initialize to an appropriate value
        Dim expected As Boolean = True ' TODO: Initialize to an appropriate value
        Dim actual As Boolean
        actual = TypeResolver.IsValueType(type)
        Assert.AreEqual(expected, actual)
        'Assert.Inconclusive("Verify the correctness of this test method.")
    End Sub
    '''<summary>
    '''A test for ResolveType
    '''</summary>
    <TestMethod()> _
    Public Sub ResolveTypeTest()
        Dim name As String = "System.Object" ' TODO: Initialize to an appropriate value
        Dim expected As Type = GetType(Object) ' TODO: Initialize to an appropriate value
        Dim actual As Type
        actual = TypeResolver.ResolveType(name)
        Assert.AreEqual(expected, actual)
        'Assert.Inconclusive("Verify the correctness of this test method.")
    End Sub

    '''<summary>
    '''A test for ReturnTypeArrayOfCount
    '''</summary>
    <TestMethod()> _
    Public Sub ReturnTypeArrayOfCountTest()
        Dim count As Integer = 2 ' TODO: Initialize to an appropriate value
        Dim type As Type = GetType(Object) ' TODO: Initialize to an appropriate value
        Dim expected As Type() = {GetType(Object), GetType(Object)} ' TODO: Initialize to an appropriate value
        Dim actual As Type()
        actual = TypeResolver.ReturnTypeArrayOfCount(count, type)
        CollectionAssert.AreEquivalent(expected, actual)
        'Assert.AreEqual(expected, actual)
        'Assert.Inconclusive("Verify the correctness of this test method.")
    End Sub
End Class
