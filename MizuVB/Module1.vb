Module Module1

    Sub Main()
        Try
            Throw New Exception()
        Catch ex As Exception
            Console.WriteLine(ex.ToString())
        End Try
    End Sub

End Module
