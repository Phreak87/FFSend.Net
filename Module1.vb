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
Imports System.Net.NetworkInformation

Module Module1

    Sub Main()
        Dim UPL As New FF_Send
        Dim RES = UPL.Upload("C:\Test.txt")
        Console.ReadLine()
    End Sub

End Module

Public Class FF_Send
    Sub New()

    End Sub

    Function Upload(Filename As String) As String
        If My.Computer.FileSystem.FileExists(Filename) = False Then Return "File not Found"
        Dim SEC As New KeyRing(Nothing, "", "send.firefox.com")

        ' ---------------------------------------------------------------------
        ' Generiere Metadaten (Test = TestKey + TestSalt + TestMeta [Test.txt])
        ' (Test) => {"fileMetadata":"hb6dyyP5dMFeYXEBv6sa3YXb0XBHeKFuIt4AGBdJgqFKrBLLfU_V5xh96Ke0rzeX0Y-I7JbSMLElB4aU6UBiqO22TP9EqD93mBuHrNVe1xL-ceWK3kWYaCC72SNUNxTrABFKuP9MQ-PqvQnqAD9yptpHW2JlCxX7DKNcXQk4UfZfs_2gkN9sLw","authorization":"send-v1 Bjb4F24osyrwuu8zeRlc0f0JRMgQbAxtr4rvcbghvlMsxO9r40WNKVooKbfQ_yqJLIZJCQCs7Yy5VJMfl5LapA","timeLimit":86400,"dlimit":1}
        ' (Gen)  => {"fileMetadata":"hb6dyyP5dMFeYXEBv6sa3YXb0XBHeKFuIt4AGBdJgqFKrBLLfU_V5xh96Ke0rzeX0Y-I7JbSMLElB4aU6UBiqO22TP9EqD93mBuHrNVe1xL-ceWK3kWYaCC72SNUNxTrABFKuP9MQ-PqvQnqAD9yptpHW2JlCxX7DKNcXQk4UfZfs_2gkN9sLw","authorization":"send-v1 Bjb4F24osyrwuu8zeRlc0f0JRMgQbAxtr4rvcbghvlMsxO9r40WNKVooKbfQ_yqJLIZJCQCs7Yy5VJMfl5LapA","timeLimit":86400,"dlimit":1}
        ' ---------------------------------------------------------------------
        Dim Meta2 As New Dictionary(Of String, Object)
        Meta2.Add("fileMetadata", Helper.B64.unpadded_urlsafe_b64encodeB(SEC.EncryptMetaData(Nothing)))
        Meta2.Add("authorization", "send-v1 " & Helper.B64.unpadded_urlsafe_b64encodeB(SEC.authKey))
        Meta2.Add("timeLimit", 86400)
        Meta2.Add("dlimit", 1)
        Dim Meta2Enc = Helper.JSON.StringDictionaryToJSON(Meta2)

        Dim CLI1 As New SOC_SEC_Client("wss://send.firefox.com/api/ws")

        ' ---------------------------------------------------------------------
        ' MetaDaten senden und Antwort abwarten und auslesen (TargetURL)
        ' ---------------------------------------------------------------------
        CLI1.SendText(Meta2Enc) '                                               Send MetaData
        Threading.Thread.Sleep(500) '                                           Let the Server work
        Dim Link As String = CLI1.ReadText '                                    Link extrahieren
        Dim TUrl As String = ExtractLink(Link) '                                Die Target-URL lesen
        Console.WriteLine("Target-ID:" & TUrl) '                                Link-Target

        Dim keyHKDF = New Crypto.HKDF()

        ' ---------------------------------------------------------
        ' BInfo generieren und Terminieren (Len: 16) - OK
        ' ---------------------------------------------------------
        Dim BInfo As Byte() = Helper.UTF.UTF8GetBytesTerm("Content-Encoding: aes128gcm") '  Muss terminiert sein !!!
        Dim HKDFKey As Byte() = keyHKDF.deriveKey(SEC.RawSecret, SEC.Nonce, BInfo, 16)   '  (Test) = 198,11, 99,38, ...     (LEN16)

        ' ---------------------------------------------------------
        ' NonceBase generieren und Terminieren (Len: 12) - OK
        ' ---------------------------------------------------------
        Dim BNonce As Byte() = Helper.UTF.UTF8GetBytesTerm("Content-Encoding: nonce") '     Muss terminiert sein !!!
        Dim NonceBase As Byte() = keyHKDF.deriveKey(SEC.RawSecret, SEC.Nonce, BNonce, 12) ' (Test) = 215, 77, 70, 88, ...   (LEN12)

        ' ---------------------------------------------------------
        ' Initialisierung der Datenübertragung - OK (Für Testdaten)
        ' ---------------------------------------------------------
        Dim TestInit = Helper.BIN.ChromeDebugStringToByteArray(
            "00000000: 0102 0304 0102 0304 0102 0304 0102 0304  ................" & vbCrLf & _
            "00000001: 0001 0000 00                             .....")
        Dim DataInit(20) As Byte : SEC.Nonce.CopyTo(DataInit, 0) : DataInit(17) = 1
        Helper.BIN.PrettyPrint(TestInit, "TestInit")
        Helper.BIN.PrettyPrint(DataInit, "DataInit")

        ' ---------------------------------------------------------
        ' Datenübertragung - Fehler (Länge falsch) - 4 Bytes OK?
        ' ---------------------------------------------------------
        Dim TestData = Helper.BIN.ChromeDebugStringToByteArray(
            "00000000: 1690 353d 36d2 ae09 2ce1 b703 1847 7503  ..5=6...,....Gu." & vbCrLf & _
            "00000001: be3e c6f5 d5                             .>...")
        Dim Data As Byte() = Helper.BIN.AppendLastByte(My.Computer.FileSystem.ReadAllBytes(Filename), 2)
        Dim DataENC As Byte() = Crypto.AES_GCM.EncryptWithKey(Data, HKDFKey, NonceBase)
        Helper.BIN.PrettyPrint(TestData, "Test-Data")
        Helper.BIN.PrettyPrint(DataENC, "Org-Data")
        Helper.BIN.PrettyPrint(Crypto.AES_GCM.DecryptWithKey(Helper.BIN.CombineBytes(NonceBase, TestData), HKDFKey, 0), "Test-Data")
        Helper.BIN.PrettyPrint(Crypto.AES_GCM.DecryptWithKey(Helper.BIN.CombineBytes(NonceBase, DataENC), HKDFKey, 0), "Orig-Deco")


        ' ---------------------------------------------------------
        ' Datenübertragung beenden
        ' ---------------------------------------------------------
        Dim DataEnd(0) As Byte

        ' ---------------------------------------------------------
        ' Test der Datensendung
        ' ---------------------------------------------------------

        CLI1.SendBin(DataInit) '                                                Nonce Senden (21 Bytes)
        CLI1.SendBin(DataENC) '                                                 Daten Senden (21 Bytes)
        CLI1.SendBin(DataEnd) '                                                 Ende  Senden (1 Byte)

        Threading.Thread.Sleep(2000) '                                          Let the Server work
        Dim Stat As String = CLI1.ReadText '                                    Status auslesen
        Console.WriteLine(Stat) '                                               Status ausgeben

        Dim TargetURL As String = ("https://send.firefox.com/download/" & TUrl & "/#" & Helper.B64.unpadded_urlsafe_b64encodeB(SEC.RawSecret))
        Console.WriteLine(TargetURL & " copied to Clipboard") : My.Computer.Clipboard.SetText(TargetURL)
        Return TargetURL
    End Function
    Public Function ExtractLink(ByVal URL As String)
        If IsNothing(URL) Then Return Nothing
        Return System.Text.RegularExpressions.Regex.Match(URL, "download/(.*?)/").Groups(1).Value
    End Function
    Public Class KeyRing
        Public RawSecret As Byte()
        Public Nonce As Byte()
        Public encrKey
        Public password As String
        Public authKey
        Public metaKey
        Public url As String

        Public EmptyIV12(11) As Byte '  Eine leere (12 * 0) Nonce

        Sub New(ByVal _secretKey As Byte(), ByVal _password As String, ByVal _url As String)
            ' ----------------------------------------------------
            ' 16-Bit kryptografischen Schlüssel erzeugen
            ' ----------------------------------------------------
            RawSecret = _secretKey ' Für Testfunktionen
            If IsNothing(_secretKey) Then RawSecret = Crypto.RNG.RandomSecretKey(16)

            ' ---------------------------------------------------
            ' 16 Bytes Salt (Nonce) erzeugen
            ' ---------------------------------------------------
            Nonce = Crypto.RNG.RandomSecretKey(16)
            Nonce = Convert.FromBase64String("yRCdyQ1EMSA3mo4rqSkuNQ==") ' {201,16,157,201,13,68,49,32,55,154,142,43,169,41,46,53}

            Dim TestValues As Boolean = False
            If TestValues = True Then
                RawSecret = Crypto.RNG.TestingSecretKey() ' {1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8}
                Nonce = Crypto.RNG.TestingNonceIV() '       {1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4}
            End If

            authKey = deriveAuthKey() '                  64 Bytes => Mit Testdaten => 6  , 54,  248, 23, 110, 40, 179, 42,  240, 186, 239, 51, 121, 25, 92, 209, ... (LEN64)
            metaKey = deriveMetaKey() '                  16 Bytes => Mit Testdaten => 161, 119, 214, 11, 125, 70, 89,  105, 72,  44,  58,  40, 173, 3,  56, 118      (LEN16)
            encrKey = deriveEncrKey() '                  16 Bytes => Mit Testdaten => 

            password = _password

        End Sub

        Function deriveEncrKey()
            Return New Crypto.HKDF().deriveKey(RawSecret, Nonce, "deriveKey", 16)
        End Function
        Function deriveAuthKey()
            Return New Crypto.HKDF().deriveKey(RawSecret, Nothing, "authentication", 64)
        End Function
        Function deriveMetaKey()
            Return New Crypto.HKDF().deriveKey(RawSecret, Nothing, "metadata", 16)
        End Function

        Function EncryptFileData(FileData) As Byte()

            '59, 39, 82, 45, 36, 200, 58, 81, 20, 24, 36, 30, 252, 36, 173, 65, 118, 116, 231, 27, 180  (Len:21)
            '00000000: 3b27 522d 24c8 3a51 1418 241e fc24 ad41  ;'R-$.:Q..$..$.A
            '00000001: 7674 e71b b4                             vt...

            Dim Crypt As Byte() = Crypto.AES_GCM.EncryptWithKey(FileData, encrKey, Nonce)
            Dim Test1 As Byte() = Helper.BIN.PythonByteStringToByteArray("3b27522d24c83a511418241efc24ad417674e71bb4") ' 42 Bytes

            Return Crypt
        End Function
        Function EncryptMetaData(MetaData)
            ' ----------------------------------------------------
            ' Diese Funktion liefert das korrekte Ergebnis zurück!
            ' ----------------------------------------------------
            Dim Text As String = ("{""name"":""Test.txt"",""size"":4,""type"":""text/plain"",""manifest"":{""files"":[{""name"":""Test.txt"",""size"":4,""type"":""text/plain""}]}}")
            Dim BText = Helper.UTF.UTF8GetBytes(Text)
            Dim Crypt As Byte() = Crypto.AES_GCM.EncryptWithKey(BText, metaKey, EmptyIV12)
            Return Crypt
        End Function
        Function AuthKeyB64() As String
            ' ----------------------------------------------------
            ' Diese Funktion liefert das korrekte Ergebnis zurück!
            ' ----------------------------------------------------
            Dim RES = Helper.B64.unpadded_urlsafe_b64encodeB(authKey)
            Return RES
        End Function
    End Class

    Public Class Internal
        Sub Tests()
            ' -------------------------------------------------------------------------------------------------
            ' Base64 + Hex De/Encoding von Python-String im Modus URLSafe und unpadded ('==' am ende)
            ' -------------------------------------------------------------------------------------------------
            Dim BS1 = Helper.BIN.PythonByteStringToByteArray("\xdf\xd1\x0b\xed+\xaa\xc1cX{\x82\x12\x97c4\xea")
            Dim BH1 = Helper.HEX.ByteArrayToHexString(BS1)
            Dim BB1 = Helper.B64.unpadded_urlsafe_b64encodeB(BS1)
            If BB1 <> "39EL7SuqwWNYe4ISl2M06g" Then Console.WriteLine("Fehler")
            Dim BB2 = Helper.B64.unpadded_urlsafe_b64decodeB(BB1)
            Dim BH2 = Helper.HEX.ByteArrayToHexString(BB2)
            If BH1 <> BH2 Then Console.WriteLine("Fehler")

            ' -------------------------------------------------------------------------------------------------
            ' Encryption AES256 GCM
            ' -------------------------------------------------------------------------------------------------
            Dim S1 = "Hallo"
            Dim K1 = Crypto.RNG.RandomSecretKey(16)
            Dim K2 = Crypto.RNG.RandomSecretKey(12)
            Dim B1 = Crypto.AES_GCM.EncryptWithKey(System.Text.Encoding.UTF8.GetBytes(S1), K1, K2)
            Dim B0 = Crypto.AES_GCM.DecryptWithKey(B1, K1)
            Dim S2 = System.Text.Encoding.UTF8.GetString(B0)

            ' -------------------------------------------------------------------------------------------------
            ' Secret Keys (ehuggett/send-cli)
            ' -------------------------------------------------------------------------------------------------
            '# test data was obtained by adding debug messages to {"commit":"188b28f","source":"https://github.com/mozilla/send/","version":"v1.2.4"}
            Dim testdata As New Dictionary(Of String, Object)
            testdata.Add("secretKey", Helper.BIN.PythonByteStringToByteArray("q\xd94B\xa1&\x03\xa5<8\xddk\xee.\xea&"))
            testdata.Add("encryptKey", Helper.BIN.PythonByteStringToByteArray("\xc4\x979\xaa\r\n\xeb\xc7\xa16\xa4%\xfd\xa6\x91\t"))
            testdata.Add("authKey", Helper.BIN.PythonByteStringToByteArray("5\xa0@\xef\xd0}f\xc7o{S\x05\xe4,\xe1\xe4\xc2\x8cE\xa1\xfat\xc1\x11\x94e[L\x89%\xf5\x8b\xfc\xb5\x9b\x87\x9a\xf2\xc3\x0eKt\xdeL\xab\xa4\xa6%t\xa6""4\r\x07\xb3\xf5\xf6\xb9\xec\xcc\x08\x80}\xea"))
            testdata.Add("metaKey", Helper.BIN.PythonByteStringToByteArray("\xd5\x9dF\x05\x86\x1a\xfdi\xaeK+\xe7\x8e\x7f\xf2\xfd"))
            testdata.Add("password", "drowssap")
            testdata.Add("url", "http://192.168.254.87:1443/download/fa4cd959de/#cdk0QqEmA6U8ON1r7i7qJg")
            testdata.Add("newAuthKey", Helper.BIN.PythonByteStringToByteArray("U\x02F\x19\x1b\xc1W\x03q\x86q\xbc\xe7\x84WB\xa7(\x0f\x8a\x0f\x17\\\xb9y\xfaZT\xc1\xbf\xb2\xd48\x82\xa7\t\x9a\xb1\x1e{cg\n\xc6\x995+\x0f\xd3\xf4\xb3kd\x93D\xca\xf9\xa1(\xdf\xcb_^\xa3"))

            '# generate all keys
            Dim keys = New KeyRing(testdata("secretKey"), testdata("password"), testdata("url"))

            '# Check every key has the expected value
            Console.WriteLine(Helper.BIN.ByteCompare(testdata("secretKey"), keys.RawSecret))
            Console.WriteLine(Helper.BIN.ByteCompare(testdata("encryptKey"), keys.encrKey))
            Console.WriteLine(Helper.BIN.ByteCompare(testdata("authKey"), keys.authKey))
            Console.WriteLine(Helper.BIN.ByteCompare(testdata("metaKey"), keys.metaKey))
            Console.WriteLine(Helper.BIN.ByteCompare(testdata("password"), keys.password))

        End Sub
    End Class
End Class



Public Class SOC_SEC_Client
    Dim _URL As String
    Dim SS As SslStream
    Dim TC As TcpClient
    Dim NS As NetworkStream
    Dim _MSG As WEB_Message
    Dim _CKEY As String = SOC_Shared.CreateSocketKey()
    Dim _AKEY As String

    Property Connected As Boolean = False

    Event ReceivedData(Text As String, Client As SOC_SEC_Client)
    Event Disconnected()

    <DebuggerStepThrough()>
    Sub New(ByVal URL As String)
        _URL = URL
        Connect()
    End Sub
    <DebuggerStepThrough()>
    Function CB1() As RemoteCertificateValidationCallback
        Return Nothing
    End Function
    <DebuggerStepThrough()>
    Function CB2() As LocalCertificateSelectionCallback
        Return Nothing
    End Function

    Sub Connect()
        If Connected = True Then Exit Sub
        If _URL = "" Then Exit Sub
        Dim URI As New Uri(_URL)
        Dim IEP As IPEndPoint = Helper.IPV4.UrlToEndpoint(_URL)
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

            SS.AuthenticateAsClient(URI.DnsSafeHost, Nothing, System.Security.Authentication.SslProtocols.Default, False)
            Threading.Thread.Sleep(400)
            SS.Write(BWSUpgrade) : Dim DATA As New WEB_Message(ReadAsByteArray(TC, SS, True), True, Nothing)
            Connected = True
        Catch : End Try

    End Sub

    Sub SendText(ByVal Text As String)
        If IsNothing(tc) Then Exit Sub
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

    Function ReadAsByteArray(ByVal Stream As TcpClient, ByVal Secure As SslStream, ByVal Wait As Boolean) As Byte()
        If IsNothing(Stream) Then Return Nothing
        If IsNothing(Secure) Then Return Nothing
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

Public Class SOC_Shared
    '<DebuggerStepThrough()>
    Shared Function GetDataLength(ByVal Bytes As Byte()) As Integer
        Dim BOP As Byte = Bytes(1)  '            Länge und Mask f. Modifikation
        If BOP >= 128 Then BOP -= 128 '          Byte1 (128, MASK) entfernen wenn gesetzt
        If BOP < 126 Then Return BOP '           Länge steht in den letzten 7 Bytes von Byte1 (Max. 125, 126 und 127 haben Sonderfunktion)
        If BOP = 126 And Bytes.Count > 3 Then Return Helper.BIN.ByteArrayToInteger({Bytes(2), Bytes(3)}.ToArray)
        If BOP = 127 And Bytes.Count > 9 Then Return Helper.BIN.ByteArrayToInteger({Bytes(2), Bytes(3), Bytes(4), Bytes(5), Bytes(6), Bytes(7), Bytes(8), Bytes(9)}.ToArray)
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
        'Client-Sends:   szQ7F+ChCAL4ksP6wkNnhQ==  
        'Server-Answer:  9A6HATB6GRtZdMfdErZcScGD6s8=
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

    Shared Function PingMessage() As Byte()
        Dim Ping() As Byte = {138, 128, 0, 0, 0, 0}
        Return Ping
    End Function
    Shared Function PongMessage() As Byte()
        Dim Pong() As Byte = {138, 128}
        Return Helper.BIN.CombineBytes(Pong, SOC_Shared.CreateMaskingKey)
    End Function
    Shared Function ByteArrayToMessage(ByVal Bytes() As Byte) As String
        If IsNothing(Bytes) Then Return Nothing
        If Bytes.Count = 0 Then Return Nothing

        Dim BYTE1 As String = Helper.BIN.IntToBitString(Bytes(0), 8)
        Dim OPCODE As Integer = Bytes(0) << 4 >> 4 ' Bitshift um 4 nach rechts und zurück (löscht 1. 4 Bytes)

        Dim EOP1 As OPCodes = SOC_Shared.OpCodeDescription(OPCODE)
        Select Case EOP1
            Case OPCodes.Ping : Return "[Socket]:[Control]:[Ping]"
            Case OPCodes.Pong : Return "[Socket]:[Control]:[Pong]"
            Case OPCodes.Close : Return "[Socket]:[Control]:[Close]"
        End Select

        If Bytes.Length = 1 Then Return EOP1
        Dim OPCODE2 As String = Helper.BIN.IntToBitString(Bytes(1), 8)
        Dim MSK As Boolean = False  '            Maskierung Default
        Dim LEN As Integer = 0      '            Länge
        Dim SKP As Integer = 0      '            Skip Bytes

        ' ---------------
        ' Länge Daten
        ' ---------------
        LEN = SOC_Shared.GetDataLength(Bytes)
        If LEN = 0 Then Return ""
        If Bytes(1) >= 128 Then MSK = True
        Dim DLEN As Integer = Helper.BIN.ByteArrayToInteger({Bytes(1)}) - IIf(MSK = True, 128, 0) ' Die Länge aus Byte0
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
        If EXT = 2 Then Buffer.BlockCopy(Helper.BIN.UShortToByteArray(L1), 0, ResponseData, 2, 2) ' Erweitern um 2 Bytes
        If EXT = 8 Then Buffer.BlockCopy(Helper.BIN.UShortToByteArray(L1), 0, ResponseData, 2, 8) ' Erweitern um 8 Bytes
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
        If EXT = 2 Then Buffer.BlockCopy(Helper.BIN.UShortToByteArray(L1), 0, ResponseData, 2, 2) ' Erweitern um 2 Bytes
        If EXT = 8 Then Buffer.BlockCopy(Helper.BIN.UShortToByteArray(L1), 0, ResponseData, 2, 8) ' Erweitern um 8 Bytes
        Buffer.BlockCopy(MASK, 0, ResponseData, 2 + EXT, MASK.Length) '                             Maskierung anfügen
        Buffer.BlockCopy(Payload, 0, ResponseData, 2 + EXT + IIf(EncodeMask = True, 4, 0), Payload.Length) '         Payload anfügen
        Return ResponseData

    End Function

End Class
