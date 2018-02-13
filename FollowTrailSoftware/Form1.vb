Imports System.IO
Imports MySql.Data.MySqlClient

Public Class Form1
    Dim MySQLString As String = String.Empty
    Dim PK As String = My.Computer.FileSystem.ReadAllText("ppk.txt")
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim MySQLFile As StreamReader = New StreamReader("MySQLConfig.txt")
        Dim currentline As String = ""
        Dim MySQLServer As String = ""
        Dim MySQLUser As String = ""
        Dim MySQLPassword As String = ""
        Dim MySQLDatabase As String = ""
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
            End If
        End While
        MySQLString = "server=" + MySQLServer + ";user=" + MySQLUser + ";database=" + MySQLDatabase + ";port=3306;password=" + MySQLPassword + ";"
        Label1.Text = "Running"
        Dim Thread1 As New System.Threading.Thread(Sub() ProcessVotes())
        Thread1.Start()
    End Sub
    Private Sub ProcessQuery(Query As String)
        Dim Connection5 As MySqlConnection = New MySqlConnection(MySQLString)
        Dim Command5 As New MySqlCommand(Query, Connection5) With {.CommandTimeout = 999}
        Connection5.Open()
        Command5.ExecuteNonQuery()
        Connection5.Close()
    End Sub
    Private Sub SetPostAsProcessed(id As String)
        ProcessQuery("UPDATE votes SET processed=1 WHERE id = " & id & ";")
    End Sub
    Private Sub MoveProcessedPostsToVotesProcessedTable()
        ProcessQuery("INSERT INTO votesprocessed SELECT * FROM votes WHERE processed=1;DELETE FROM votes WHERE processed=1;")
    End Sub
    Public Sub ProcessVotes()
        While True
            Try
                Dim SQLQuery As String = "Select  * FROM votes WHERE processed=0 LIMIT 500"
                Dim Connection As MySqlConnection = New MySqlConnection(MySQLString)
                Dim Command As New MySqlCommand(SQLQuery, Connection) With {.CommandTimeout = 999}
                Connection.Open()
                Dim reader As MySqlDataReader = Command.ExecuteReader
                If reader.HasRows Then
                    While reader.Read
                        Dim SQLQuery2 As String = "Select DISTINCT * FROM followtrail WHERE account='" & reader("voter") & "' AND enabled=1"
                        Dim Connection2 As MySqlConnection = New MySqlConnection(MySQLString)
                        Dim Command2 As New MySqlCommand(SQLQuery2, Connection2) With {.CommandTimeout = 999}
                        Connection2.Open()
                        Dim reader2 As MySqlDataReader = Command2.ExecuteReader
                        If reader2.HasRows Then
                            While reader2.Read
                                Dim SQLQuery3 As String = "SELECT DISTINCT * FROM users2 WHERE drupalkey='" & reader2("drupalkey") & "' AND approved=1"
                                Dim Connection3 As MySqlConnection = New MySqlConnection(MySQLString)
                                Dim Command3 As New MySqlCommand(SQLQuery3, Connection3) With {.CommandTimeout = 999}
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
                                            Dim Thread1 As New System.Threading.Thread(Sub() VoteThreadAsync(author, permlink, percent, weight, username, voter))
                                            Thread1.Start()
                                            System.Threading.Thread.Sleep(50)
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
            Dim parameters As String = "getPostVotes.py " & Author & "/" & Permlink
            Dim info As ProcessStartInfo = New ProcessStartInfo("python", parameters)
            info.CreateNoWindow = True
            info.RedirectStandardOutput = True
            info.RedirectStandardError = True
            info.UseShellExecute = False
            Dim p As Process = Process.Start(info)
            Dim UsersWhoVoted As String = p.StandardOutput.ReadToEnd
            p.WaitForExit(10000)
            If p.HasExited = False Then
                p.Kill()
            End If
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
                'ElseIf Weight = 0.0 Then
                '    VP = 0.0
                '    If UsersWhoVoted.Contains(Username) = True Then VoteAnyway = True Else VoteAnyway = False
                'Else
                '    VP = Weight
                '    If UsersWhoVoted.Contains(Username) = True Then VoteAnyway = True Else VoteAnyway = False
            End If
            If VoteAnyway = True Then
                Dim parameters2 As String = "votePost.py " & Author & "/" & Permlink & " " & String.Format("{0:F1}", VP) & " " & Username & " " & PK
                Dim info2 As ProcessStartInfo = New ProcessStartInfo("python", parameters2)
                info2.RedirectStandardOutput = True
                info2.RedirectStandardError = True
                info2.CreateNoWindow = True
                info2.UseShellExecute = False
                Dim p2 As Process = Process.Start(info2)
                Dim responseFromServer As String = p2.StandardOutput.ReadToEnd
                Dim ErrorResponse As String = p2.StandardError.ReadToEnd
                p2.WaitForExit(10000)
                If p2.HasExited = False Then
                    p2.Kill()
                End If
                If String.IsNullOrEmpty(ErrorResponse) = False Then
                    My.Computer.FileSystem.WriteAllText("Logs\" + Username + "-" + Author + "-" + Permlink, ErrorResponse, True)
                End If
                If responseFromServer.Contains("ok") Then
                    ProcessQuery("INSERT INTO voted (author, permlink, voter, weight, processed, date, originalvoter) VALUES ('" & Author & "', '" & Permlink & "', '" & Username & "', '" & VP & "', 1, '" & DateTime.Now & "', '" & Voter & "')")
                Else
                    ProcessQuery("INSERT INTO voted (author, permlink, voter, weight, processed, date, originalvoter) VALUES ('" & Author & "', '" & Permlink & "', '" & Username & "', '" & VP & "', 0, '" & DateTime.Now & "', '" & Voter & "')")
                    My.Computer.FileSystem.WriteAllText("Logs\" + Username + "-" + Author + "-" + Permlink, responseFromServer, True)
                End If
            End If
        Catch ex As Exception
            My.Computer.FileSystem.WriteAllText("Logs\" + Username + "-" + Author + "-" + Permlink, ex.ToString, True)
        End Try
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim pList() As Process = Process.GetProcessesByName("python")
        For Each proc As Process In pList
            proc.Kill()
        Next
    End Sub

End Class
