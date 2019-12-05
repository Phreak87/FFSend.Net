Imports System.Text
Imports System.Net.Sockets
Imports System.IO
Imports System.Net
Imports System.Net.NetworkInformation

Public Class Helper
    Class B64
        Public Shared Function unpadded_urlsafe_b64encodeB(ByVal B As Byte()) As String
            ' Kopie der Python urlsafe_b64encode(b) Funktion
            If IsNothing(B) Then Return ""
            Return Convert.ToBase64String(B).Replace("+", "-").Replace("/", "_").Replace("=", "")
        End Function
        Public Shared Function unpadded_urlsafe_b64decodeB(ByVal B As String) As Byte()
            ' Kopie der Python urlsafe_b64decode(s) Funktion
            Return Convert.FromBase64String(B.Replace("-", "+").Replace("_", "/") & IIf(B.Length Mod 4 = 0, "", Space(4 - B.Length Mod 4).Replace(" ", "=")))
        End Function
    End Class
    Class HEX
        <DebuggerStepThrough()>
        Public Shared Function HexStringToByteArray(ByVal hex As String) As Byte()
            Return Enumerable.Range(0, hex.Length).Where(Function(x) x Mod 2 = 0).[Select](Function(x) Convert.ToByte(hex.Substring(x, 2), 16)).ToArray()
        End Function
        <DebuggerStepThrough()>
        Public Shared Function ByteArrayToHexString(ByVal ba As Byte()) As String
            Return BitConverter.ToString(ba).Replace("-", "").ToLower
        End Function
    End Class
    Class UTF
        ''' <summary>
        ''' Terminierter UTF8 String 
        ''' 0 am Ende des Byte-Arrays
        ''' </summary>
        <DebuggerStepThrough()>
        Public Shared Function UTF8GetBytesTerm(ByVal Text As String) As Byte()
            Dim B1 As Byte() = System.Text.Encoding.UTF8.GetBytes(Text)
            Return Helper.BIN.AppendLastNullByte(B1)
        End Function
        <DebuggerStepThrough()>
        Public Shared Function UTF8GetBytes(ByVal Text As String) As Byte()
            Return System.Text.Encoding.UTF8.GetBytes(Text)
        End Function
        <DebuggerStepThrough()>
        Public Shared Function UTF8GetString(ByVal Bytes() As Byte) As String
            Return System.Text.Encoding.UTF8.GetString(Bytes)
        End Function
    End Class
    Class BIN

        '-------------------------------------------------------
        ' 32768 | 16384 | 8192 | 4096 | 2048 | 1024 | 512 | 256 
        ' 128   | 64    | 32   |  16  |   8  |  4   | 2   | 1   
        '-------------------------------------------------------
        <DebuggerStepThrough()>
        Shared Function TrimEmptyBytes(ByVal Bytes As Byte()) As Byte()
            If IsNothing(Bytes) Then Return Nothing
            If Bytes.Count = 0 Then Return Bytes
            Dim TrimBytes As Integer = 0
            Do Until Bytes(Bytes.Length - 1 - TrimBytes) <> 0
                TrimBytes = TrimBytes + 1
                If TrimBytes = Bytes.Length Then Exit Do
            Loop
            Array.Resize(Bytes, Bytes.Length - TrimBytes)
            If Bytes.Length = 0 Then Return Bytes

            Dim SkipBytes As Integer = 0
            Do Until (Bytes(SkipBytes)) <> 0 : SkipBytes = SkipBytes + 1 : Loop
            Bytes = Bytes.Skip(SkipBytes).ToArray

            Return Bytes
        End Function
        <DebuggerStepThrough()>
        Shared Function TrimLastEmptyBytes(ByVal Bytes As Byte()) As Byte()
            If Bytes.Count = 0 Then Return Bytes
            Do Until Bytes(Bytes.Length - 1) <> 0
                Array.Resize(Bytes, Bytes.Count - 1)
                If Bytes.Count = 0 Then Return Bytes
            Loop
            Return Bytes
        End Function

        <DebuggerStepThrough()> _
        Shared Function BinaryFindFirst(ByVal Array As Byte(), ByVal Text As String) As Integer
            Dim PatCHK As Byte() = System.Text.Encoding.UTF8.GetBytes(Text)
            Dim FByte As Byte = PatCHK(0)
            Dim LastPos As Integer = System.Array.IndexOf(Array, FByte)
            Do
                If LastPos = -1 Then Return LastPos
                If LastPos + PatCHK.Length > Array.Length Then Return -1
                Dim PatOrg(Text.Length - 1) As Byte : System.Array.Copy(Array, LastPos, PatOrg, 0, Text.Length)
                If PatOrg.SequenceEqual(PatCHK) Then : Return LastPos
                Else : LastPos = System.Array.IndexOf(Array, FByte, LastPos + 1)
                End If
            Loop
            Return -1
        End Function
        <DebuggerStepThrough()> _
        Shared Function BinaryFindNext(ByVal Array As Byte(), ByVal StartIndex As Integer, ByVal Text As String) As Integer
            Dim PatCHK As Byte() = System.Text.Encoding.UTF8.GetBytes(Text)
            Dim FByte As Byte = PatCHK(0) ' FirstByte
            Dim LastPos As Integer = System.Array.IndexOf(Array, FByte, StartIndex)
            Do

                If LastPos = -1 Then Return -1 '                            Not found
                If LastPos + PatCHK.Length > Array.Length Then Return -1 '  Not found

                Dim PatOrg(Text.Length - 1) As Byte : System.Array.Copy(Array, LastPos, PatOrg, 0, Text.Length)
                If PatOrg.SequenceEqual(PatCHK) Then : Return LastPos
                Else : LastPos = System.Array.IndexOf(Array, FByte, LastPos + 1)
                End If
            Loop
            Return -1
        End Function
        <DebuggerStepThrough()>
        Shared Function BinaryFindLast(ByVal Array As Byte(), ByVal Text As String) As Integer
            Dim PatCHK As Byte() = System.Text.Encoding.UTF8.GetBytes(Text)
            Dim FByte As Byte = PatCHK(0)
            Dim LastPos As Integer = System.Array.LastIndexOf(Array, FByte)
            Do
                If LastPos = -1 Then Return LastPos
                If LastPos + PatCHK.Length > Array.Length Then LastPos = System.Array.LastIndexOf(Array, FByte, LastPos - 1) : Continue Do
                Dim PatOrg(Text.Length - 1) As Byte : System.Array.Copy(Array, LastPos, PatOrg, 0, Text.Length)
                If PatOrg.SequenceEqual(PatCHK) Then : Return LastPos
                Else : LastPos = System.Array.LastIndexOf(Array, FByte, LastPos - 1)
                End If
            Loop
            Return -1
        End Function

        <DebuggerStepThrough()>
        Shared Function AppendFirstByte(ByVal Bytes As Byte(), AppendByte As Byte) As Byte()
            Dim B2(Bytes.Length) As Byte : Bytes.CopyTo(B2, 1) : Bytes(0) = AppendByte : Return B2
        End Function
        <DebuggerStepThrough()>
        Shared Function AppendLastByte(ByVal Bytes As Byte(), AppendByte As Byte) As Byte()
            Dim B2(Bytes.Length) As Byte : Bytes.CopyTo(B2, 0) : B2(Bytes.Length) = AppendByte : Return B2
        End Function
        <DebuggerStepThrough()>
        Shared Function AppendLastNullByte(ByVal Bytes As Byte()) As Byte()
            Return AppendLastByte(Bytes, 0)
        End Function
        <DebuggerStepThrough()>
        Shared Function CombineBytes(ByVal Array1 As Byte(), ByVal Array2 As Byte()) As Byte()
            Using combinedStream = New MemoryStream()
                Using binaryWriter = New BinaryWriter(combinedStream)
                    binaryWriter.Write(Array1)
                    binaryWriter.Write(Array2)
                End Using
                Return combinedStream.ToArray()
            End Using
        End Function
        <DebuggerStepThrough()>
        Shared Function ByteArrayToInteger(ByVal B() As Byte) As Double
            Dim RES As Double = 0
            For i As Integer = 0 To B.Count - 1
                Dim i2 As Integer = B.Count - i - 1
                Dim Shift As Long = CLng(B(i)) << (i2 * 8)
                RES = RES + Shift
            Next
            Return RES
        End Function
        <DebuggerStepThrough()>
        Shared Function IntToBitString(ByVal Number As Integer, ByVal Bits As Int16) As String
            Dim OPCODE As String = ""
            For i As Integer = Bits - 1 To 0 Step -1
                Dim Val As Integer = 2 ^ i
                If Number >= Val Then
                    Number -= Val
                    OPCODE = OPCODE & 1
                Else
                    OPCODE = OPCODE & 0
                End If
            Next
            Return OPCODE
        End Function
        <DebuggerStepThrough()>
        Public Shared Function GetByteRange(ByVal Bytes As Byte(), ByVal Index As Integer, Optional ByVal Count As Integer = -1) As Byte()
            If Count = -1 Then Count = Bytes.Count - Index
            Dim B2(Count - 1) As Byte
            Array.Copy(Bytes, Index, B2, 0, Count)
            Return B2
        End Function

        <DebuggerStepThrough()>
        Shared Function UShortToByteArray(ByVal Number As UShort) As Byte()
            Return BitConverter.GetBytes(CUShort(Number)).Reverse.ToArray ' 2 Bytes Short Little Endian
        End Function
        Public Shared Function ChromeDebugStringToByteArray(ByVal Representation As String) As Byte()
            '00000000: 1690 353d 36d2 ae09 2ce1 b703 1847 7503  ..5=6...,....Gu.
            '00000001: be3e c6f5 d5                             .>...

            Dim Lines As String() = Split(Representation, vbCrLf)
            Dim Combi As New System.Text.StringBuilder
            For Each Line In Lines
                Combi.Append(Mid(Line, 10, 40).Replace(" ", ""))
            Next
            Dim Ret((Combi.Length / 2) - 1) As Byte
            For i As Integer = 0 To Combi.Length - 1 Step 2
                Dim Part As String = Mid(Combi.ToString, i + 1, 2)
                Dim B1S = Chr(AscW(Part(0))) & Chr(AscW(Part(1)))
                Dim num = Int32.Parse(B1S, System.Globalization.NumberStyles.HexNumber)
                Ret(i / 2) = num
            Next
            Console.WriteLine(Helper.UTF.UTF8GetString(Ret))
            Return Ret
        End Function
        Public Shared Function PythonByteStringToByteArray(ByVal Representation As String) As Byte()
            Dim M1 As New MemoryStream(System.Text.Encoding.UTF8.GetBytes(Representation))
            Dim HexB As Integer() = {48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102}
            Dim B1 As New MemoryStream

            ' TestSTR  = \xdf\xd1\x0b\xed+\xaa\xc1cX{\x82\x12\x97c4\xea
            ' TestARR = 223, 209, 11, 237, 43, 170, 193, 99, 88, 123, 130, 18, 151, 99, 52, 234
            ' TestHEX = 0xdf, 0xd1, 0xb, 0xed, 0x2b, 0xaa, 0xc1, 0x63, 0x58, 0x7b, 0x82, 0x12, 0x97, 0x63, 0x34, 0xea

            Do Until M1.Position = M1.Length
                Dim P1 As Byte = M1.ReadByte
                If P1 = 92 Then ' /
                    Dim P2 As Byte = M1.ReadByte()
                    Select Case P2
                        Case 120
                            Dim BA As New List(Of Byte)
                            Do
                                Dim B1X = M1.ReadByte
                                If HexB.Contains(B1X) = True Then
                                    BA.Add(B1X)
                                    If BA.Count = 2 Then
                                        Dim B1S = Chr(BA(0)) & Chr(BA(1)) ' Console.WriteLine(B1S)
                                        Dim num = Int32.Parse(B1S, System.Globalization.NumberStyles.HexNumber)
                                        B1.WriteByte(num) : Exit Do
                                    End If
                                End If
                            Loop
                        Case Asc("r")
                            B1.WriteByte(13) ' CR
                        Case Asc("n")
                            B1.WriteByte(10) ' LF
                        Case Asc("t")
                            B1.WriteByte(9) ' Tab
                        Case Else
                            B1.WriteByte(92) ' /
                    End Select
                Else
                    B1.WriteByte(P1)
                End If
            Loop

            Return B1.ToArray
        End Function
        Public Shared Function ByteCompare(ByVal Arr1 As Object, ByVal Arr2 As Object) As Boolean
            If IsNothing(Arr1) Then Return False
            If IsNothing(Arr2) Then Return False

            Dim a As String = ""
            If Arr1.GetType.ToString = "System.Byte[]" Then
                a = String.Join(" ", CType(Arr1, System.Byte())) & vbCrLf & String.Join(" ", CType(Arr2, System.Byte()))
            Else
                a = Arr1 & vbCrLf & Arr2
            End If

            If Arr1.Length <> Arr2.Length Then Console.WriteLine(a) : Return False
            For i As Integer = 0 To Arr1.Length - 1
                If Arr1(i) <> Arr2(i) Then
                    Console.WriteLine(a) : Return False
                End If
            Next
            Return True
        End Function
        Public Shared Sub PrettyPrint(ByVal ba As Byte(), Optional Info As String = Nothing)
            If IsNothing(ba) Then Exit Sub
            If Not IsNothing(Info) Then
                Console.Write(Info & ":")
            End If
            Console.WriteLine()
            Console.WriteLine(Space(35).Replace(" ", "-"))
            Console.Write("0 |")
            For i As Integer = 1 To ba.Length
                Console.Write(Space(3 - CStr(ba(i - 1)).Length) & ba(i - 1) & " ")
                If i Mod 8 = 0 Then Console.WriteLine() : Console.Write((i & Space(2 - CStr(i).Length)) & "|")
                If i = ba.Length And i Mod 8 <> 0 Then Console.WriteLine()
            Next
            Console.WriteLine(Space(32).Replace(" ", "-"))

        End Sub
    End Class
    Class JSON
        Public Shared Function StringDictionaryToJSON(ByVal Dict As Dictionary(Of String, Object)) As String
            Return Newtonsoft.Json.JsonConvert.SerializeObject(Dict, Newtonsoft.Json.Formatting.None)
        End Function
    End Class
    Class IPV4
        <DebuggerStepThrough()>
        Shared Function UrlToEndpoint(ByVal Url As String) As Net.IPEndPoint
            Try
                If Url = "" Then Return Nothing
                Dim URI = New Uri(Url)
                If Text.RegularExpressions.Regex.IsMatch(URI.DnsSafeHost, "[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}") Then
                    Return New IPEndPoint(IPAddress.Parse(URI.DnsSafeHost), URI.Port)
                End If
                Dim IPHE As IPHostEntry = Dns.GetHostEntry(URI.DnsSafeHost)
                Dim IPEP As New IPEndPoint(GetV4IP(IPHE.AddressList), URI.Port)
                Return IPEP
            Catch ex As Exception

            End Try
            Return Nothing
        End Function
        <DebuggerStepThrough()>
        Shared Function GetV4IP(ByVal List As Object) As System.Net.IPAddress
            For Each eintrag In List
                If eintrag.addressfamily = AddressFamily.InterNetwork Then
                    Return eintrag
                End If
            Next
            Return Nothing
        End Function
    End Class
    Class LST
        Shared Function ArrayToLower(ByVal _Array As String()) As List(Of String)
            Return Array.ConvertAll(_Array, Function(s) s.ToString.ToLower).ToList
        End Function
        <DebuggerStepThrough()>
        Shared Function ListToUpper(ByVal Liste As List(Of String)) As List(Of String)
            Dim RET As New List(Of String)
            For Each eintrag In Liste
                RET.Add(eintrag.ToUpper)
            Next
            Return RET
        End Function
        <DebuggerStepThrough()>
        Shared Function ListTolower(ByVal Liste As List(Of String)) As List(Of String)
            Dim RET As New List(Of String)
            For Each eintrag In Liste
                RET.Add(eintrag.ToLower)
            Next
            Return RET
        End Function
    End Class
End Class
