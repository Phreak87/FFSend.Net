Imports System.Net.Sockets
Imports System.Net
Imports System.IO
Imports System.Security.Cryptography
Imports System.Security.Cryptography.X509Certificates
Imports System.Net.Security
Imports System.Text
Imports Org.BouncyCastle.Crypto.Modes
Imports Org.BouncyCastle.Crypto.Engines
Imports Org.BouncyCastle.Crypto.Parameters
Imports Org.BouncyCastle.Crypto

Module Module1

    Sub Main()
        Dim UPL As New FF_Send
        Dim RES = UPL.Upload("C:\Users\Phreak87\Desktop\Test.txt")
    End Sub

End Module


Public Class FF_Send

    Function Upload(Filename As String) As String

        Dim SEC As New SecretKeys(Nothing, "", "send.firefox.com")
        Dim CLI1 As New SOC_SEC_Client("wss://send.firefox.com/api/ws")

        CLI1.Connect()
        SEC.PrintSecrets()

        ' ---------------------------------------------
        ' Generiere Metadaten-Byte-Array
        ' ---------------------------------------------
        Dim Meta As New Dictionary(Of String, String)
        Meta.Add("iv", B64.unpadded_urlsafe_b64encodeB(SEC.metaIV))
        Meta.Add("name", Split(Filename, "\")(Split(Filename, "\").Count - 1))
        Meta.Add("type", "application/octet-stream")
        Dim Meta1 = JSON.StringDictionaryToJSON(Meta)

        Dim Metadata = Crypto.AES_GCM.EncryptWithKey(System.Text.Encoding.UTF8.GetBytes(Meta1), SEC.metaKey)
        Dim MetaDigest = Crypto.MD5.MD5Hash(Metadata)
        Dim MetaEncrypted = BIN.CombineBytes(Metadata, MetaDigest)
        Dim Meta2 As New Dictionary(Of String, Object)
        Meta2.Add("fileMetadata", B64.unpadded_urlsafe_b64encodeB(MetaEncrypted))
        Meta2.Add("authorization", "send-v1 " & B64.unpadded_urlsafe_b64encodeB(SEC.authKey))
        Meta2.Add("timeLimit", 86400)
        Meta2.Add("dlimit", 1)
        Dim Meta2Enc = JSON.StringDictionaryToJSON(Meta2)

        CLI1.SendText(Meta2Enc) '                                               MetaData

        Dim Link As String = CLI1.ReadText '                                    Link extrahieren
        Dim TUrl As String = ExtractLink(Link) '                                Die Target-URL lesen
        Console.WriteLine(TUrl) '                                               Link-Target

        Dim D1(20) As Byte : D1(0) = 255 : CLI1.SendBin(D1) '                   Send a little Trash (StreamReader Position)
        Dim D2 As Byte() = My.Computer.FileSystem.ReadAllBytes(Filename) '      Get File-Data
        CLI1.SendBin(Crypto.AES_GCM.EncryptWithKey(D2, SEC.secretKey))
        Dim D3(0) As Byte : CLI1.SendBin(D3) '                                  Send End

        Threading.Thread.Sleep(2000)
        Console.WriteLine(CLI1.ReadText) '                                      Receive OK 

        Dim TargetURL As String = ("https://send.firefox.com/download/" & TUrl & "/#" & B64.unpadded_urlsafe_b64encodeB(SEC.secretKey))
        My.Computer.Clipboard.SetText(TargetURL)

        Return ""
    End Function

    Public Function ExtractLink(ByVal URL As String)
        Return System.Text.RegularExpressions.Regex.Match(URL, "download/(.*?)/").Groups(1).Value
    End Function

    Public Class SecretKeys
        Public secretKey
        Public newAuthKey
        Public encryptKey
        Public encryptIV
        Public password As String
        Public authKey
        Public metaKey
        Public url As String
        Public metaIV(11) As Byte ' Firefox-Send verwendet einen 12 byte Null IV beim verschlüsseln der Metadaten

        Sub New(ByVal _secretKey As Byte(), ByVal _password As String, ByVal _url As String)
            secretKey = Crypto.RNG.RandomSecretKey(16) : If Not IsNothing(_secretKey) Then secretKey = _secretKey
            encryptKey = deriveEncryptKey()
            encryptIV = Crypto.RNG.RandomSecretKey(12)
            authKey = deriveAuthKey()
            metaKey = deriveMetaKey()
            password = _password
            newAuthKey = deriveNewAuthKey(_password, _url)
            ' metaIV(11) as byte ' = bytes(12) # 
        End Sub

        Function deriveEncryptKey()
            Dim Salt(0) As Byte
            Dim HKDF As New Crypto.HKDF()
            Return HKDF.DeriveKey(Salt, secretKey, System.Text.Encoding.UTF8.GetBytes("encryption"), 16)
        End Function
        Function deriveAuthKey()
            Dim Salt(0) As Byte
            Dim HKDF As New Crypto.HKDF()
            Return HKDF.DeriveKey(Salt, secretKey, System.Text.Encoding.UTF8.GetBytes("authentication"), 64)
        End Function
        Function deriveMetaKey()
            Dim Salt(0) As Byte
            Dim HKDF As New Crypto.HKDF()
            Return HKDF.DeriveKey(Salt, secretKey, System.Text.Encoding.UTF8.GetBytes("metadata"), 16)
        End Function
        Function deriveNewAuthKey(ByVal password As String, ByVal _url As String) As Byte()
            Dim PassArr = System.Text.Encoding.UTF8.GetBytes(password)
            Dim URLArr = System.Text.Encoding.UTF8.GetBytes(_url)
            Dim HMAC_SHA256 = HMAC.Create() : HMAC_SHA256.ComputeHash(PassArr)
            Dim NewAuthKey = Crypto.PBKDF2.PBKDF2(password, URLArr, 100, 64)
            Return NewAuthKey
        End Function

        Public Sub PrintSecrets()
            Console.WriteLine(url)
            Console.WriteLine(password)
            Console.WriteLine("ExampleKey:" & "qCHwlxOiPcvDPfyJ7cmPZg")
            Console.WriteLine("SecretKey_:" & B64.unpadded_urlsafe_b64encodeB(secretKey))
            Console.WriteLine("NewAuthKey:" & B64.unpadded_urlsafe_b64encodeB(newAuthKey))
            Console.WriteLine("EncryptKey:" & B64.unpadded_urlsafe_b64encodeB(encryptKey))
            Console.WriteLine("EncryptIV_:" & B64.unpadded_urlsafe_b64encodeB(encryptIV))
            Console.WriteLine("AuthKey___:" & B64.unpadded_urlsafe_b64encodeB(authKey))
            Console.WriteLine("MetaKey___:" & B64.unpadded_urlsafe_b64encodeB(metaKey))
            Console.WriteLine("MetaIV____:" & B64.unpadded_urlsafe_b64encodeB(metaIV))
        End Sub
    End Class
End Class

Class B64
    <DebuggerStepThrough()>
    Public Shared Function unpadded_urlsafe_b64encodeB(ByVal B As Byte()) As String
        ' Kopie der Python urlsafe_b64encode(b) Funktion
        Return Convert.ToBase64String(B).Replace("+", "-").Replace("/", "_").Replace("=", "")
    End Function
    <DebuggerStepThrough()>
    Public Shared Function unpadded_urlsafe_b64decodeB(ByVal B As String) As Byte()
        ' Kopie der Python urlsafe_b64decode(s) Funktion
        Return Convert.FromBase64String(B.Replace("-", "+").Replace("_", "/") & IIf(B.Length Mod 4 = 0, "", Space(4 - B.Length Mod 4).Replace(" ", "=")))
    End Function
End Class
Class HEX
    <DebuggerStepThrough()>
    Shared Function HexValueFromByteRange(ByVal Bytes As Byte(), ByVal Index As Integer, ByVal Length As Integer) As String
        Return HEX.HexBytes(BIN.GetByteRange(Bytes, Index, Length))
    End Function
    <DebuggerStepThrough()>
    Public Shared Function HexStringToByteArray(ByVal hex As String) As Byte()
        Return Enumerable.Range(0, hex.Length).Where(Function(x) x Mod 2 = 0).[Select](Function(x) Convert.ToByte(hex.Substring(x, 2), 16)).ToArray()
    End Function
    <DebuggerStepThrough()>
    Public Shared Function ByteArrayToHexString(ByVal ba As Byte()) As String
        Return BitConverter.ToString(ba).Replace("-", "").ToLower
    End Function
    <DebuggerStepThrough()>
    Public Shared Function HexBytes(ByVal ba As Byte()) As String
        Dim Builder As New StringBuilder
        For Each Hex As Byte In ba
            Builder.Append(Hex.ToString("x2"))
        Next
        Return Builder.ToString()
    End Function
End Class
Class JSON
    Public Shared Function StringDictionaryToJSON(ByVal Dict As Dictionary(Of String, Object)) As String
        Return Newtonsoft.Json.JsonConvert.SerializeObject(Dict, Newtonsoft.Json.Formatting.None)
    End Function
    Public Shared Function StringDictionaryToJSON(ByVal Dict As Dictionary(Of String, String)) As String
        Return Newtonsoft.Json.JsonConvert.SerializeObject(Dict, Newtonsoft.Json.Formatting.None)
    End Function
End Class
Class BIN
    <DebuggerStepThrough()>
    Shared Function CombineBytes(ByVal Array1 As Byte(), ByVal Array2 As Byte()) As Byte()
        Dim MS As New MemoryStream
        MS.Write(Array1, 0, Array1.Length)
        MS.Write(Array2, 0, Array2.Length)
        Return MS.ToArray
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
    Shared Function BytesToBitString(ByVal Bytes As Byte()) As String
        Dim OUT As New System.Text.StringBuilder
        Dim Bits = New BitArray(Bytes.Reverse.ToArray)
        For i As Integer = 0 To Bits.Length - 1
            OUT.Insert(0, IIf(Bits(i) = False, 0, 1))
        Next
        Return OUT.ToString
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
    Shared Function HexStringToDouble(ByVal Hex As String) As Double
        Return Integer.Parse(Hex, Globalization.NumberStyles.HexNumber)
    End Function
    <DebuggerStepThrough()>
    Shared Function DoubleToHexString(ByVal Value As Integer) As String
        Return Value.ToString("X")
    End Function
    <DebuggerStepThrough()>
    Shared Function IntegerFromByteRange(ByVal Bytes As Byte(), ByVal Index As Integer, ByVal Length As Integer) As Integer
        Return BIN.ByteArrayToInteger(BIN.GetByteRange(Bytes, Index, Length))
    End Function
    <DebuggerStepThrough()>
    Shared Function BitStringFromByteRange(ByVal Bytes As Byte(), ByVal Index As Integer, ByVal Length As Integer) As String
        Return BIN.BytesToBitString(BIN.GetByteRange(Bytes, Index, Length))
    End Function

    <DebuggerStepThrough()>
    Shared Function UShortToByteArray(ByVal Number As UShort) As Byte()
        Return BitConverter.GetBytes(CUShort(Number)).Reverse.ToArray ' 2 Bytes Short Little Endian
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
    Public Shared Function GetByteRange(ByVal Bytes As Byte(), ByVal Index As Integer, Optional ByVal Count As Integer = -1) As Byte()
        If Count = -1 Then Count = Bytes.Count - Index
        Dim B2(Count - 1) As Byte
        Array.Copy(Bytes, Index, B2, 0, Count)
        Return B2
    End Function

    Public Shared Function ByteArrayToIntString(ByVal ba As Byte()) As String
        Return String.Join(" ", ba)
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
                                    Dim B1S = Chr(BA(0)) & Chr(BA(1)) : Console.WriteLine(B1S)
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
End Class

Public Class SOC_Shared
    <DebuggerStepThrough()>
    Shared Function GetDataLength(ByVal Bytes As Byte()) As Integer
        Dim BOP As Byte = Bytes(1)  '            Länge und Mask f. Modifikation
        If BOP >= 128 Then BOP -= 128 '          Byte1 (128, MASK) entfernen wenn gesetzt
        If BOP < 126 Then Return BOP '           Länge steht in den letzten 7 Bytes von Byte1 (Max. 125, 126 und 127 haben Sonderfunktion)
        If BOP = 126 And Bytes.Count > 3 Then Return BIN.ByteArrayToInteger({Bytes(2), Bytes(3)}.ToArray)
        If BOP = 127 And Bytes.Count > 9 Then Return BIN.ByteArrayToInteger({Bytes(2), Bytes(3), Bytes(4), Bytes(5), Bytes(6), Bytes(7), Bytes(8), Bytes(9)}.ToArray)
        Return 0
    End Function
    <DebuggerStepThrough()>
    Shared Function CreateMaskingKey() As Byte()
        Dim Mask(3) As Byte
        Dim Rand As New System.Security.Cryptography.RNGCryptoServiceProvider()
        Rand.GetBytes(Mask)
        Return Mask
    End Function
    <DebuggerStepThrough()>
    Shared Function CreateSocketKey() As String
        Dim src(15) As Byte
        Dim RNG As New System.Security.Cryptography.RNGCryptoServiceProvider
        RNG.GetBytes(src) : Return Convert.ToBase64String(src)
    End Function
    <DebuggerStepThrough()>
    Shared Function ResponseWebSocketAccept(ByVal KEY As String) As String
        Dim _WKEY As String = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        Dim GES As String = KEY + _WKEY
        Dim GESB As Byte() = Text.Encoding.UTF8.GetBytes(GES)
        Dim SHA1 As Byte() = System.Security.Cryptography.SHA1.Create().ComputeHash(GESB)
        Return Convert.ToBase64String(SHA1)
    End Function

    Enum OPCodes
        Continuation = 0
        Text = 1
        Binary = 2
        RESV1 = 3
        RESV2 = 4
        RESV3 = 5
        RESV4 = 6
        RESV5 = 7
        Close = 8
        Ping = 9
        Pong = 10
        Unknown = 99
    End Enum
    <DebuggerStepThrough()>
    Shared Function OpCodeDescription(ByVal OP1 As Integer) As OPCodes
        If CInt(OP1) = 0 Then Return OPCodes.Continuation
        If CInt(OP1) = 1 Then Return OPCodes.Text
        If CInt(OP1) = 2 Then Return OPCodes.Binary
        If CInt(OP1) = 3 Then Return OPCodes.RESV1
        If CInt(OP1) = 4 Then Return OPCodes.RESV2
        If CInt(OP1) = 5 Then Return OPCodes.RESV3
        If CInt(OP1) = 6 Then Return OPCodes.RESV4
        If CInt(OP1) = 7 Then Return OPCodes.RESV5
        If CInt(OP1) = 8 Then Return OPCodes.Close
        If CInt(OP1) = 9 Then Return OPCodes.Ping
        If CInt(OP1) = 10 Then Return OPCodes.Pong
        Return OPCodes.Unknown
    End Function

    Enum CloseCodes
        Normal = 8
        GoingAway = 9
        ProtocolError = 10
        WrongDatatype = 11
        RES1 = 12
        RES2 = 13
        RES3 = 14
        DataTypeIncorrect = 15
        PolicyError = 16
        TooBig = 17
        ExtensionNegotiation = 18
        Unexpected = 19
        RES4 = 20
        RES5 = 21
        RES6 = 22
        TLSError = 23
    End Enum
    Shared Function CloseDescription(ByVal OP1 As Integer) As CloseCodes
        ' ----------------------------------------------------
        ' Todo: Handling von Close-Frames
        ' ----------------------------------------------------
        If CInt(OP1) = 8 Then Return ("Normal Close")
        If CInt(OP1) = 9 Then Return ("Going Away")
        If CInt(OP1) = 10 Then Return ("Protocol Error")
        If CInt(OP1) = 11 Then Return ("Wrong Datatype")
        If CInt(OP1) = 12 Then Return ("Reserved: ")
        If CInt(OP1) = 13 Then Return ("Reserved: ")
        If CInt(OP1) = 14 Then Return ("Reserved: ")
        If CInt(OP1) = 15 Then Return ("Datatype Incorrect (<>UTF8)")
        If CInt(OP1) = 16 Then Return ("Policy Error")
        If CInt(OP1) = 17 Then Return ("Too Big Message")
        If CInt(OP1) = 18 Then Return ("Extension Negotiation Error")
        If CInt(OP1) = 19 Then Return ("Unexpected Error")
        If CInt(OP1) = 20 Then Return ("Reserved: ")
        If CInt(OP1) = 21 Then Return ("Reserved: ")
        If CInt(OP1) = 22 Then Return ("Reserved: ")
        If CInt(OP1) = 23 Then Return ("TLS Error")
        Return 8
    End Function

    <DebuggerStepThrough()>
    Shared Function MaskMessage(ByVal Message As Byte(), ByVal Mask() As Byte) As Byte()
        If IsNothing(Mask) Then
            Return Message
        End If
        For i As Integer = 0 To Message.Length - 1
            Message(i) = Message(i) Xor Mask(i Mod 4)
        Next
        Return Message
    End Function
    Shared Function ByteArrayToMessage(ByVal Bytes() As Byte) As String
        If IsNothing(Bytes) Then Return Nothing
        If Bytes.Count = 0 Then Return Nothing

        Dim BYTE1 As String = BIN.IntToBitString(Bytes(0), 8)
        Dim OPCODE As Integer = Bytes(0) << 4 >> 4 ' Bitshift um 4 nach rechts und zurück (löscht 1. 4 Bytes)

        Dim EOP1 As OPCodes = SOC_Shared.OpCodeDescription(OPCODE)
        Select Case EOP1
            Case OPCodes.Ping : Return "[Socket]:[Control]:[Ping]"
            Case OPCodes.Pong : Return "[Socket]:[Control]:[Pong]"
            Case OPCodes.Close : Return "[Socket]:[Control]:[Close]"
        End Select

        If Bytes.Length = 1 Then Return EOP1
        Dim OPCODE2 As String = BIN.IntToBitString(Bytes(1), 8)
        Dim MSK As Boolean = False  '            Maskierung Default
        Dim LEN As Integer = 0      '            Länge
        Dim SKP As Integer = 0      '            Skip Bytes

        ' ---------------
        ' Länge Daten
        ' ---------------
        LEN = SOC_Shared.GetDataLength(Bytes)
        If LEN = 0 Then Return ""
        If Bytes(1) >= 128 Then MSK = True
        Dim DLEN As Integer = BIN.ByteArrayToInteger({Bytes(1)}) - IIf(MSK = True, 128, 0) ' Die Länge aus Byte0
        If DLEN = 126 Then SKP = 2
        If DLEN = 127 Then SKP = 8

        ' ---------------
        ' Disconnect
        ' ---------------

        If MSK = False Then
            Dim DST(LEN - 1) As Byte
            Buffer.BlockCopy(Bytes, 2 + SKP, DST, 0, LEN)
            Return System.Text.Encoding.UTF8.GetString(DST)
        Else
            Dim DST(LEN - 1) As Byte
            Dim BEGBYTE As Integer = 2 + SKP : If MSK = True Then BEGBYTE += 4
            Dim KEY(3) As Byte : Buffer.BlockCopy(Bytes, BEGBYTE - 4, KEY, 0, 4)
            Buffer.BlockCopy(Bytes, BEGBYTE, DST, 0, LEN)
            Return System.Text.Encoding.UTF8.GetString(SOC_Shared.MaskMessage(DST, KEY))
        End If

        Return ""
    End Function
    Shared Function MessageToByteArray(ByVal Message As String, ByVal EncodeMask As Boolean) As Byte()
        '-----------------------------------
        ' Client zu Server => Masked
        '-----------------------------------
        ' 128 | 64 | 32 | 16 | 8 | 4 | 2 | 1
        ' FIN | RS | RS | RS | <  OPCODE   > Byte #1 (0001=Text, ... Ping/Pong)
        ' MSK | <      PAYLOAD LENGTH      > Byte #2
        ' <         Extension Bytes        > Byte #3  (Optional, if len =126)
        ' <         Extension Bytes        > Byte #4  (Optional, if len =126)
        ' <         Extension Bytes        > Byte #5  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #6  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #7  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #8  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #9  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #10 (Optional, if len =127)
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        '-----------------------------------

        Dim MASK() As Byte = CreateMaskingKey() '       Mask (4 Bytes)
        Dim L1 As UInt16 = Message.Length '             Message Length
        Dim BYTES(1) As Byte '                          Mit 2 Bytes starten
        Dim BITS As New BitArray(16, 0) '               Bit-Array mit 2 Bytes erzeugen
        Dim EXT As Integer = 0 '                         Extension Bytes

        BITS.Set(0, True) '                             (OPC:TEXT)
        BITS.Set(7, True) '                             (FIN:TRUE)
        If EncodeMask = True Then BITS.Set(15, True) '  (ENC:TRUE)

        BITS.CopyTo(BYTES, 0)

        If L1 > 0 And L1 <= 125 Then BYTES(1) += L1 '                           Nachricht is kleiner als 126 Bytes
        If L1 >= 126 And L1 <= UInt16.MaxValue Then BYTES(1) += 126 : EXT = 2 ' um 2 Bytes erweitern (Payloadlänge)
        If L1 >= 65536 And L1 < ULong.MaxValue Then BYTES(1) += 127 : EXT = 8 ' um 8 Bytes erweitern (Payloadlänge)

        Dim Payload(0) As Byte
        If EncodeMask = True Then Payload = SOC_Shared.MaskMessage(Text.Encoding.UTF8.GetBytes(Message), MASK)
        If EncodeMask = False Then Payload = SOC_Shared.MaskMessage(Text.Encoding.UTF8.GetBytes(Message), Nothing)

        Dim ResponseData As Byte() : ReDim ResponseData((2 + EXT + 4 + Payload.Length) - 1) '       Array vorbereiten
        Buffer.BlockCopy(BYTES, 0, ResponseData, 0, 2) '                                            Bytes anfügen
        If EXT = 2 Then Buffer.BlockCopy(BIN.UShortToByteArray(L1), 0, ResponseData, 2, 2) ' Erweitern um 2 Bytes
        If EXT = 8 Then Buffer.BlockCopy(BIN.UShortToByteArray(L1), 0, ResponseData, 2, 8) ' Erweitern um 8 Bytes
        Buffer.BlockCopy(MASK, 0, ResponseData, 2 + EXT, MASK.Length) '                             Maskierung anfügen
        Buffer.BlockCopy(Payload, 0, ResponseData, 2 + EXT + IIf(EncodeMask = True, 4, 0), Payload.Length) '         Payload anfügen
        Return ResponseData

    End Function
    Shared Function DataToByteArray(Data As Byte(), ByVal EncodeMask As Boolean) As Byte()
        '-----------------------------------
        ' Client zu Server => Masked
        '-----------------------------------
        ' 128 | 64 | 32 | 16 | 8 | 4 | 2 | 1
        ' FIN | RS | RS | RS | <  OPCODE   > Byte #1 (0001=Text, ... Ping/Pong)
        ' MSK | <      PAYLOAD LENGTH      > Byte #2
        ' <         Extension Bytes        > Byte #3  (Optional, if len =126)
        ' <         Extension Bytes        > Byte #4  (Optional, if len =126)
        ' <         Extension Bytes        > Byte #5  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #6  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #7  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #8  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #9  (Optional, if len =127)
        ' <         Extension Bytes        > Byte #10 (Optional, if len =127)
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        ' <               MASK             > MASK-KEY wenn MSK-Bit = 1
        '-----------------------------------

        Dim MASK() As Byte = CreateMaskingKey() '       Mask (4 Bytes)
        Dim L1 As UInt16 = Data.Length '                Data Length
        Dim BYTES(1) As Byte '                          Mit 2 Bytes starten
        Dim BITS As New BitArray(16, 0) '               Bit-Array mit 2 Bytes erzeugen
        Dim EXT As Integer = 0 '                         Extension Bytes

        BITS.Set(1, True) '                             (OPC:TEXT)
        BITS.Set(7, True) '                             (FIN:TRUE)
        If EncodeMask = True Then BITS.Set(15, True) '  (ENC:TRUE)

        BITS.CopyTo(BYTES, 0)

        If L1 > 0 And L1 <= 125 Then BYTES(1) += L1 '                           Nachricht is kleiner als 126 Bytes
        If L1 >= 126 And L1 <= UInt16.MaxValue Then BYTES(1) += 126 : EXT = 2 ' um 2 Bytes erweitern (Payloadlänge)
        If L1 >= 65536 And L1 < ULong.MaxValue Then BYTES(1) += 127 : EXT = 8 ' um 8 Bytes erweitern (Payloadlänge)

        Dim Payload(0) As Byte
        If EncodeMask = True Then Payload = SOC_Shared.MaskMessage(Data, MASK)
        If EncodeMask = False Then Payload = SOC_Shared.MaskMessage(Data, Nothing)

        Dim ResponseData As Byte() : ReDim ResponseData((2 + EXT + 4 + Payload.Length) - 1) '       Array vorbereiten
        Buffer.BlockCopy(BYTES, 0, ResponseData, 0, 2) '                                            Bytes anfügen
        If EXT = 2 Then Buffer.BlockCopy(BIN.UShortToByteArray(L1), 0, ResponseData, 2, 2) '        Erweitern um 2 Bytes
        If EXT = 8 Then Buffer.BlockCopy(BIN.UShortToByteArray(L1), 0, ResponseData, 2, 8) '        Erweitern um 8 Bytes
        Buffer.BlockCopy(MASK, 0, ResponseData, 2 + EXT, MASK.Length) '                             Maskierung anfügen
        Buffer.BlockCopy(Payload, 0, ResponseData, 2 + EXT + IIf(EncodeMask = True, 4, 0), Payload.Length) '         Payload anfügen
        Return ResponseData

    End Function

End Class


Public Class SOC_SEC_Client
    Dim _URL As String
    Dim SS As SslStream
    Dim TC As TcpClient
    Dim NS As NetworkStream
    Dim _CKEY As String = SOC_Shared.CreateSocketKey()
    Dim _AKEY As String

    Property Connected As Boolean = False

    Event ReceivedData(Text As String, Client As SOC_SEC_Client)
    Event Disconnected()

    <DebuggerStepThrough()>
    Sub New(ByVal URL As String)
        _URL = URL
    End Sub
    <DebuggerStepThrough()>
    Function CB1() As RemoteCertificateValidationCallback
        Return Nothing
    End Function
    <DebuggerStepThrough()>
    Function CB2() As LocalCertificateSelectionCallback
        Return Nothing
    End Function

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

    Sub Connect()
        If _URL = "" Then Exit Sub
        Dim URI As New Uri(_URL)
        Dim IEP As IPEndPoint = UrlToEndpoint(_URL)
        Dim ITRY As Integer = 0

        Dim WSUpgrade As New StringBuilder
        WSUpgrade.AppendLine("GET " & URI.PathAndQuery & " HTTP/1.1")
        WSUpgrade.AppendLine("Upgrade: WebSocket")
        WSUpgrade.AppendLine("Connection: Upgrade")
        WSUpgrade.AppendLine("Sec-WebSocket-Version: 13")
        WSUpgrade.AppendLine("Sec-WebSocket-Key: " & _CKEY)
        WSUpgrade.AppendLine("Host: " & URI.Host & ":" & URI.Port)
        WSUpgrade.AppendLine()
        Dim BWSUpgrade As Byte() = Encoding.UTF8.GetBytes(WSUpgrade.ToString)

        Try
            TC = New TcpClient(IEP.Address.ToString, URI.Port)
            TC.SendTimeout = 2000 : TC.ReceiveTimeout = 2000
            SS = New Security.SslStream(TC.GetStream, False, CB1, CB2)
            NS = TC.GetStream

            SS.AuthenticateAsClient(URI.DnsSafeHost, Nothing, System.Security.Authentication.SslProtocols.Default, False) : Threading.Thread.Sleep(200)
            SS.Write(BWSUpgrade) : Dim DATA = System.Text.Encoding.UTF8.GetString(ReadAsByteArray(TC, SS, True)) : Console.WriteLine(DATA)
            Connected = True
        Catch : End Try

    End Sub

    Sub SendText(ByVal Text As String)
        If IsNothing(TC) Then Exit Sub
        If Connected = False Then Connect()
        If Connected = False Then Exit Sub
        Try
            Dim B As Byte()
            B = SOC_Shared.MessageToByteArray(Text, 1)
            SS.Write(B, 0, B.Length)
        Catch ex As Exception
            Connected = False
        End Try
    End Sub
    Sub SendBin(ByVal Data As Byte())
        If IsNothing(NS) Then Exit Sub
        If Connected = False Then Connect()
        If Connected = False Then Exit Sub
        Try
            Dim B As Byte()
            B = SOC_Shared.DataToByteArray(Data, 1)
            SS.Write(B, 0, B.Length)
        Catch ex As Exception
            Connected = False
        End Try
    End Sub

    Function ReadText() As String
        Return SOC_Shared.ByteArrayToMessage(ReadAsByteArray(TC, SS, True))
    End Function
    Function ReadData() As Byte()
        Return ReadAsByteArray(TC, SS, True)
    End Function

    Function ReadAsByteArray(Stream As TcpClient, Secure As SslStream, Wait As Boolean) As Byte()
        If Wait = True Then Threading.Thread.Sleep(200)
        If Stream.Available > IIf(Wait = True, -1, 0) Then
            Dim B(Stream.Available - 1) As Byte : Secure.Read(B, 0, Stream.Available)
            Return B
        End If
        Return Nothing
    End Function
    Function ReadAsString(Stream As TcpClient, Secure As SslStream, Wait As Boolean) As String
        Dim Target As Byte() = ReadAsByteArray(Stream, Secure, Wait)
        If Not IsNothing(Target) Then Return System.Text.Encoding.UTF8.GetString(Target)
        Return ""
    End Function
End Class

Public Class Crypto
    Class RNG
        Public Shared Function RandomSecretKey(ByVal Length As Integer) As Byte()
            Dim Sec = System.Security.Cryptography.RNGCryptoServiceProvider.Create()
            Dim Bin(Length - 1) As Byte : Sec.GetNonZeroBytes(Bin)
            Return Bin
        End Function
    End Class
    Class MD5
        Public Shared Function MD5Hash(ByVal BY As Byte()) As Byte()
            Dim MD5 As New MD5CryptoServiceProvider
            Return MD5.ComputeHash(BY)
        End Function
    End Class
    Class PBKDF2
        Public Shared Function PBKDF2(ByVal Input As String, ByVal Salt As Byte(), ByVal Iterations As Integer, ByVal Hash_Size As Integer)
            If IsNothing(Salt) Then Salt = New Byte() {0, 0, 0, 0, 0, 0, 0, 0}
            Dim RES As New Rfc2898DeriveBytes(Input, Salt, Iterations)
            Return RES.GetBytes(Hash_Size)
        End Function
    End Class
    Class HKDF
        Private keyedHash As Func(Of Byte(), Byte(), Byte())
        Public Sub New()
            Dim hmac = New HMACSHA256()
            keyedHash = Function(key, message)
                            hmac.Key = key
                            Return hmac.ComputeHash(message)
                        End Function
        End Sub
        Public Function Extract(ByVal salt As Byte(), ByVal inputKeyMaterial As Byte()) As Byte()
            Return keyedHash(salt, inputKeyMaterial)
        End Function
        Public Function Expand(ByVal prk As Byte(), ByVal info As Byte(), ByVal outputLength As Integer) As Byte()
            Dim resultBlock = New Byte(-1) {}
            Dim result = New Byte(outputLength - 1) {}
            Dim bytesRemaining = outputLength
            Dim i As Integer = 1

            While bytesRemaining > 0
                Dim currentInfo = New Byte(resultBlock.Length + info.Length + 1 - 1) {}
                Array.Copy(resultBlock, 0, currentInfo, 0, resultBlock.Length)
                Array.Copy(info, 0, currentInfo, resultBlock.Length, info.Length)
                currentInfo(currentInfo.Length - 1) = CByte(i)
                resultBlock = keyedHash(prk, currentInfo)
                Array.Copy(resultBlock, 0, result, outputLength - bytesRemaining, Math.Min(resultBlock.Length, bytesRemaining))
                bytesRemaining -= resultBlock.Length
                i += 1
            End While

            Return result
        End Function
        Public Function DeriveKey(ByVal salt As Byte(), ByVal inputKeyMaterial As Byte(), ByVal info As Byte(), ByVal outputLength As Integer) As Byte()
            Dim prk = Extract(salt, inputKeyMaterial)
            Dim result = Expand(prk, info, outputLength)
            Return result
        End Function

        Public Sub Test()
            Dim hkdf = New HKDF()

            If True Then
                Dim ikm = HEX.HexStringToByteArray("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b")
                Dim salt = HEX.HexStringToByteArray("000102030405060708090a0b0c")
                Dim info = HEX.HexStringToByteArray("f0f1f2f3f4f5f6f7f8f9")
                Dim L = 42
                Dim prk = "077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5"
                Dim okm = "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865"
                Dim actualPrk = HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = HEX.ByteArrayToHexString(hkdf.DeriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL42 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL42 - OK")
            End If

            If True Then
                Dim ikm = HEX.HexStringToByteArray("000102030405060708090a0b0c0d0e0f" +
                                            "101112131415161718191a1b1c1d1e1f" +
                                            "202122232425262728292a2b2c2d2e2f" +
                                            "303132333435363738393a3b3c3d3e3f" +
                                            "404142434445464748494a4b4c4d4e4f")

                Dim salt = HEX.HexStringToByteArray("606162636465666768696a6b6c6d6e6f" +
                                             "707172737475767778797a7b7c7d7e7f" +
                                             "808182838485868788898a8b8c8d8e8f" +
                                             "909192939495969798999a9b9c9d9e9f" +
                                             "a0a1a2a3a4a5a6a7a8a9aaabacadaeaf")
                Dim info = HEX.HexStringToByteArray("b0b1b2b3b4b5b6b7b8b9babbbcbdbebf" +
                                             "c0c1c2c3c4c5c6c7c8c9cacbcccdcecf" +
                                             "d0d1d2d3d4d5d6d7d8d9dadbdcdddedf" +
                                             "e0e1e2e3e4e5e6e7e8e9eaebecedeeef" +
                                             "f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff")
                Dim L = 82
                Dim prk = "06a6b88c5853361a06104c9ceb35b45cef760014904671014a193f40c15fc244"
                Dim okm = "b11e398dc80327a1c8e7f78c596a4934" +
                          "4f012eda2d4efad8a050cc4c19afa97c" +
                          "59045a99cac7827271cb41c65e590e09" +
                          "da3275600c2f09b8367793a9aca3db71" +
                          "cc30c58179ec3e87c14c01d5c1f3434f" +
                          "1d87"

                Dim actualPrk = HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = HEX.ByteArrayToHexString(hkdf.DeriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL82 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL82 - OK")
            End If

            If True Then
                Dim ikm = HEX.HexStringToByteArray("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b")
                Dim salt = New Byte(-1) {}
                Dim info = New Byte(-1) {}
                Dim L = 42
                Dim prk = "19ef24a32c717b167f33a91d6f648bdf96596776afdb6377ac434c1c293ccb04"
                Dim okm = "8da4e775a563c18f715f802a063c5a31" & "b8a11f5c5ee1879ec3454e5f3c738d2d" & "9d201395faa4b61a96c8"
                Dim actualPrk = HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = HEX.ByteArrayToHexString(hkdf.DeriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL42_2 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL42_2 - OK")
            End If
        End Sub
    End Class
    Class AES_GCM
        Private Const KEY_BIT_SIZE = 16 * 8
        Private Const MAC_BIT_SIZE = 128
        Private Const NONCE_BIT_SIZE = 128

        Public Shared Function EncryptWithKey(ByVal text As Byte(), ByVal key As Byte(), Optional ByVal nonSecretPayload As Byte() = Nothing) As Byte()
            If key Is Nothing OrElse key.Length <> KEY_BIT_SIZE / 8 Then Throw New ArgumentException(String.Format("Key needs to be {0} bit!", KEY_BIT_SIZE), "key")
            nonSecretPayload = If(nonSecretPayload, New Byte() {})
            Dim nonce = New Byte(NONCE_BIT_SIZE / 8 - 1) {}
            nonce = Crypto.RNG.RandomSecretKey(nonce.Length) ' Random.NextBytes(nonce, 0, nonce.Length)
            Dim cipher = New GcmBlockCipher(New AesEngine())
            Dim parameters = New AeadParameters(New KeyParameter(key), MAC_BIT_SIZE, nonce, nonSecretPayload)
            cipher.Init(True, parameters)
            Dim cipherText = New Byte(cipher.GetOutputSize(text.Length) - 1) {}
            Dim len = cipher.ProcessBytes(text, 0, text.Length, cipherText, 0)
            cipher.DoFinal(cipherText, len)

            Using combinedStream = New MemoryStream()
                Using binaryWriter = New BinaryWriter(combinedStream)
                    binaryWriter.Write(nonSecretPayload)
                    binaryWriter.Write(nonce)
                    binaryWriter.Write(cipherText)
                End Using

                Return combinedStream.ToArray()
            End Using
        End Function
        Public Shared Function DecryptWithKey(ByVal message As Byte(), ByVal key As Byte(), Optional ByVal nonSecretPayloadLength As Integer = 0) As Byte()
            If key Is Nothing OrElse key.Length <> KEY_BIT_SIZE / 8 Then Throw New ArgumentException(String.Format("Key needs to be {0} bit!", KEY_BIT_SIZE), "key")
            If message Is Nothing OrElse message.Length = 0 Then Throw New ArgumentException("Message required!", "message")

            Using cipherStream = New MemoryStream(message)

                Using cipherReader = New BinaryReader(cipherStream)
                    Dim nonSecretPayload = cipherReader.ReadBytes(nonSecretPayloadLength)
                    Dim nonce = cipherReader.ReadBytes(NONCE_BIT_SIZE / 8)
                    Dim cipher = New GcmBlockCipher(New AesEngine())
                    Dim parameters = New AeadParameters(New KeyParameter(key), MAC_BIT_SIZE, nonce, nonSecretPayload)
                    cipher.Init(False, parameters)
                    Dim cipherText = cipherReader.ReadBytes(message.Length - nonSecretPayloadLength - nonce.Length)
                    Dim plainText = New Byte(cipher.GetOutputSize(cipherText.Length) - 1) {}

                    Try
                        Dim len = cipher.ProcessBytes(cipherText, 0, cipherText.Length, plainText, 0)
                        cipher.DoFinal(plainText, len)
                    Catch __unusedInvalidCipherTextException1__ As InvalidCipherTextException
                        Return Nothing
                    End Try

                    Return plainText
                End Using
            End Using
        End Function
    End Class
End Class
