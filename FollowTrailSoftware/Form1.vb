Imports System.IO
Imports System.Text
Imports MySql.Data.MySqlClient

Public Class Form1
    Dim MySQLString As String = String.Empty
    Dim API_Host As String = String.Empty
    Dim PK As String = My.Computer.FileSystem.ReadAllText("ppk.txt")
    Dim CloseSoftware As Boolean = False
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
       RunSystem()
    End Sub

    Private Sub RunSystem
        Dim MySQLFile As StreamReader = New StreamReader("config.txt")
        Dim currentline As String =  String.Empty
        Dim MySQLServer As String =  String.Empty
        Dim MySQLUser As String =  String.Empty
        Dim MySQLPassword As String =  String.Empty
        Dim MySQLDatabase As String =  String.Empty
        Dim SSLMode As String = String.Empty
        While MySQLFile.EndOfStream = False
            currentline = MySQLFile.ReadLine
            If currentline.Contains("server") Then
                Dim GetServer As String() = currentline.Split("=")
                MySQLServer = GetServer(1)
            ElseIf currentline.Contains("username") Then
                Dim GetUsername As String() = currentline.Split("=")
                MySQLUser = GetUsername(1)
            ElseIf currentline.Contains("password") Then
                Dim GetPassword As String() = currentline.Split("=")
                MySQLPassword = GetPassword(1)
            ElseIf currentline.Contains("database") Then
                Dim GetDatabase As String() = currentline.Split("=")
                MySQLDatabase = GetDatabase(1)
            ElseIf currentline.Contains("sslmode") Then
                Dim GetSSLMode As String() = currentline.Split("=")
                SSLMode = GetSSLMode(1)
            ElseIf currentline.Contains("api") Then
                Dim Get_API_Host As String() = currentline.Split("=")
                API_Host = Get_API_Host(1)
            End If
        End While
        MySQLString = "server=" + MySQLServer + ";user=" + MySQLUser + ";database=" + MySQLDatabase + ";port=3306;password=" + MySQLPassword + ";sslmode=" + sslmode
        Label1.Text = "Running"
        Dim Thread1 As New Threading.Thread(Sub() ProcessVotes())
        Thread1.Start()
    End Sub
    Private Sub ProcessQuery(Query As String)
        Try
            Dim Connection5 As MySqlConnection = New MySqlConnection(MySQLString)
            Dim Command5 As New MySqlCommand(Query, Connection5)
            Connection5.Open()
            Command5.ExecuteNonQuery()
            Connection5.Close()
        Catch ex As Exception
            My.Computer.FileSystem.WriteAllText("sqlerrorlog.txt", DateTime.Now & " | " & ex.ToString & vbCrLf, True)
        End Try
    End Sub
    Private Sub SetPostAsProcessed(id As String)
        ProcessQuery("UPDATE votes SET processed=1 WHERE id = " & id & ";")
    End Sub
    Private Sub MoveProcessedPostsToVotesProcessedTable()
        ProcessQuery("INSERT INTO votesprocessed (author, permlink, voter, weight, processed, date) SELECT author, permlink, voter, weight, processed, date FROM votes WHERE processed=1;DELETE FROM votes WHERE processed=1;")
    End Sub
    Public Sub ProcessVotes()
        While True
            If CloseSoftware = True Then Exit While
            Try
                Dim SQLQuery As String = "Select  * FROM votes WHERE processed=0 LIMIT 1000"
                Dim Connection As MySqlConnection = New MySqlConnection(MySQLString)
                Dim Command As New MySqlCommand(SQLQuery, Connection)
                Connection.Open()
                Dim reader As MySqlDataReader = Command.ExecuteReader
                If reader.HasRows Then
                    While reader.Read
                        Dim SQLQuery2 As String = "Select DISTINCT * FROM followtrail WHERE account='" & reader("voter") & "' AND enabled=1"
                        Dim Connection2 As MySqlConnection = New MySqlConnection(MySQLString)
                        Dim Command2 As New MySqlCommand(SQLQuery2, Connection2)
                        Connection2.Open()
                        Dim reader2 As MySqlDataReader = Command2.ExecuteReader
                        If reader2.HasRows Then
                            While reader2.Read
                                Dim SQLQuery3 As String = "SELECT DISTINCT * FROM users2 WHERE drupalkey='" & reader2("drupalkey") & "' AND approved=1"
                                Dim Connection3 As MySqlConnection = New MySqlConnection(MySQLString)
                                Dim Command3 As New MySqlCommand(SQLQuery3, Connection3)
                                Connection3.Open()
                                Dim reader3 As MySqlDataReader = Command3.ExecuteReader
                                If reader3.HasRows Then
                                    While reader3.Read
                                        If String.IsNullOrEmpty(reader3("username")) = False Then
                                            Dim author = reader("author")
                                            Dim permlink = reader("permlink")
                                            Dim percent = reader2("percent")
                                            Dim weight = reader("weight")
                                            Dim username = reader3("username")
                                            Dim voter = reader("voter")
                                            Dim Thread1 As New Threading.Thread(Sub() VoteThreadAsync(author, permlink, percent, weight, username, voter))
                                            Thread1.Start()
                                            Threading.Thread.Sleep(100)
                                        End If
                                    End While
                                End If
                                Connection3.Close()
                            End While
                        End If
                        Connection2.Close()
                        SetPostAsProcessed(reader("id"))
                    End While
                    MoveProcessedPostsToVotesProcessedTable()
                End If
                Connection.Close()
                Threading.Thread.Sleep(500)
            Catch ex As Exception
                My.Computer.FileSystem.WriteAllText("errorlog.txt", DateTime.Now & " | " & ex.ToString & vbCrLf, True)
                Threading.Thread.Sleep(500)
            End Try
        End While
    End Sub
    Private Sub VoteThreadAsync(Author As String, Permlink As String, Percent As String, Weight As String, Username As String, Voter As String)
        Try
            Dim getPostVotesRequest As Net.WebRequest = Net.WebRequest.Create(API_Host + "/getPostVotes/?p=" & Author & "/" & Permlink)
            Dim getPostVotesResponse As Net.WebResponse = getPostVotesRequest.GetResponse()
            Dim ReceiveStream1 As Stream = getPostVotesResponse.GetResponseStream()
            Dim encode As Encoding = System.Text.Encoding.GetEncoding("utf-8")
            Dim readStream1 As New StreamReader(ReceiveStream1, encode)
            Dim UsersWhoVoted = readStream1.ReadToEnd 
            Dim VP As String = Percent
            Dim VoteAnyway As Boolean = False
            VP = VP.Replace("%", "")
            If IsNumeric(VP) = False Then
                VP = 1.0
            End If
            If Weight > 0.0 Then
                If VP < 0.0 Then VP = 1.0
                If VP = 0.0 Then VP = Weight
                If UsersWhoVoted.Contains(Username) = False Then VoteAnyway = True
            ElseIf (Weight = 0.0 Or Weight < 0.0) And UsersWhoVoted.Contains(Username) = False Then
                VP = 0.0
                If UsersWhoVoted.Contains(Username) Then VoteAnyway = True
            End If
            If VoteAnyway = True Then
                Dim VoteRequest As System.Net.WebRequest = System.Net.WebRequest.Create(API_Host + "/vote/")
                VoteRequest.Method = "POST"
                Dim postData As String = "i=" & Author & "/" & Permlink & "&w=" & String.Format("{0:F1}", VP) & "&v=" & Username & "&pk=" & PK
                Dim byteArray As Byte() = Encoding.UTF8.GetBytes(postData)
                VoteRequest.ContentType = "application/x-www-form-urlencoded"
                VoteRequest.ContentLength = byteArray.Length
                Dim dataStream As Stream = VoteRequest.GetRequestStream()
                dataStream.Write(byteArray, 0, byteArray.Length)
                dataStream.Close()
                Dim VoteResponse As Net.WebResponse = VoteRequest.GetResponse()
                dataStream = VoteResponse.GetResponseStream()
                Dim reader As New StreamReader(dataStream)
                Dim responseFromServer As String = reader.ReadToEnd()
                reader.Close()
                dataStream.Close()
                VoteResponse.Close()
                If responseFromServer.Contains("ok") Then
                    ProcessQuery("INSERT INTO voted (author, permlink, voter, weight, processed, date, originalvoter) VALUES ('" & Author & "', '" & Permlink & "', '" & Username & "', '" & VP & "', 1, '" & DateTime.Now & "', '" & Voter & "')")
                Else
                    ProcessQuery("INSERT INTO voted (author, permlink, voter, weight, processed, date, originalvoter) VALUES ('" & Author & "', '" & Permlink & "', '" & Username & "', '" & VP & "', 0, '" & DateTime.Now & "', '" & Voter & "')")
                    ProcessQuery("INSERT INTO voteerrors (date, username, author, permlink, error) VALUES ('" & DateTime.Now & "', '" & Username & "', '" & Author & "', '" & Permlink & "','Error voting: " & responseFromServer.Replace("'", "\'") & "')")
                End If
            End If
        Catch ex As Exception
            ProcessQuery("INSERT INTO voteerrors (date, username, author, permlink, error) VALUES ('" & DateTime.Now & "', '" & Username & "', '" & Author & "', '" & Permlink & "', 'Catch Error: " & ex.ToString.Replace("'", "\'") & "')")
        End Try
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim pList() As Process = Process.GetProcessesByName("python")
        For Each proc As Process In pList
            proc.Kill()
        Next
    End Sub

    Private Sub Label2_Click(sender As Object, e As EventArgs) Handles Label2.Click
        Label2.Text = "Free RAM: " & Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024, 2) & " GB"
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim vars As String() = Environment.GetCommandLineArgs
        If vars.Count > 1 Then
            If vars(1) = "-s" Then
                RunSystem()
            End If
        End If
        Label2.Text = "Free RAM: " & Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024, 2) & " GB"
    End Sub

    Private Sub Form1_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        CloseSoftware = True
    End Sub
End Class
