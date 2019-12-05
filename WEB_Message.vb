Imports Newtonsoft.Json
Imports System.IO
Imports System.ComponentModel
Imports System.Net

<TypeConverter(GetType(ExpandableObjectConverter)), _
EditorBrowsable(EditorBrowsableState.Always)>
Public Class WEB_Message

    Sub TEST_Headline()
        For Each Eintrag In {"GET / HTTP/1.1",
                             "GET http://www.test.de:8090/1.html?Version=1 Http/1.1",
                             "GET /Modules/head.js HTTP/1.1",
                             "POST /api/db/insert HTTP/1.1",
                             "Test.de",
                             "www.test.de",
                             "http://www.test.de",
                             "test.de/1.html",
                             "www.test.de/1.html",
                             "test.de:8090",
                             "http://www.test.de:8090",
                             "http://www.test.de:8090/1.html",
                             "GET www.test.de:8090/1.html?Version=1"}
            Dim Result = New HeadLine(Eintrag)
        Next
    End Sub

#Region "Variablen Haupt"
    Public ENDPOINT As IPEndPoint

    <JsonIgnore()> Property RAW_DATA As RawData
    Property HEAD_LINE As HeadLine
    Property HEAD_PARA As HeadPara
    Property POST_DATA As PostData
#End Region

#Region "Konstruktoren"
    '<DebuggerStepThrough()>
    Sub New(ByVal Data As String, ByVal HeadOnly As Boolean, Optional ByVal IPEndpoint As IPEndPoint = Nothing)
        Me.New(Helper.UTF.UTF8GetBytes(Data), HeadOnly, IPEndpoint)
    End Sub
    '<DebuggerStepThrough()>
    Sub New(ByVal Data() As Byte, ByVal HeadOnly As Boolean, Optional ByVal IPEndpoint As IPEndPoint = Nothing)
        If IsNothing(Data) Then Exit Sub
        If Data.Length = 0 Then Exit Sub
        ENDPOINT = IPEndpoint
        RAW_DATA = New RawData(Data, HeadOnly)
        HEAD_LINE = New HeadLine(Split(RAW_DATA.TextHead, vbCrLf)(0))
        HEAD_PARA = New HeadPara(RAW_DATA.TextHead)
        If HeadOnly = False Then POST_DATA = New PostData(HEAD_LINE, HEAD_PARA, RAW_DATA.BytesBody)
    End Sub
#End Region

#Region "Get Data Fields"
    <DebuggerStepThrough()>
    Public Overrides Function ToString() As String
        Select Case HEAD_LINE.ReqT
            Case "GET" : Return "[" & HEAD_LINE.ReqT & "] " & HEAD_LINE.PathMod & " " & String.Join(";", HEAD_LINE.Parameters)
            Case "POST" : Return "[" & HEAD_LINE.ReqT & "] " & HEAD_LINE.PathMod & " " & String.Join(";", POST_DATA.Parameters)
            Case Else : Return "[" & HEAD_LINE.ReqT & "] " & HEAD_LINE.PathMod & " " & HEAD_LINE.Port & String.Join(";", HEAD_PARA.Parameters)
        End Select
        Return ""
    End Function

    <DebuggerStepThrough()>
    Public Overloads Function GetFields(ByVal Name As String, Optional ByVal DefaultReturn As String = "") As String()
        Return GetFields({Name}, DefaultReturn)
    End Function
    <DebuggerStepThrough()>
    Public Overloads Function GetField(ByVal Name As String, Optional ByVal DefaultReturn As String = "") As String
        Return GetField({Name}, DefaultReturn)
    End Function
    <DebuggerStepThrough()>
    Public Overloads Function GetField(ByVal Name As String(), Optional ByVal DefaultReturn As String = "") As String
        For Each nameStr In Name
            If HEAD_LINE.Parameters.ContainsKey(nameStr.ToUpper) Then Return HEAD_LINE.Parameters(nameStr.ToUpper)
            If HEAD_PARA.Parameters.ContainsKey(nameStr.ToUpper) Then Return HEAD_PARA.Parameters(nameStr.ToUpper)
            If POST_DATA.Parameters.ContainsKey(nameStr.ToUpper) Then Return POST_DATA.Parameters(nameStr.ToUpper)
        Next
        Return DefaultReturn
    End Function
    '<DebuggerStepThrough()> _
    Public Overloads Function GetFields(ByVal Name As String(),
                                        Optional ByVal DefaultReturn As String = "",
                                        Optional ByVal Additional As Dictionary(Of String, String) = Nothing) _
                                        As String()
        For Each nameStr In Name
            If HEAD_PARA.Parameters.ContainsKey(nameStr.ToUpper) Then Return {HEAD_PARA.Parameters(nameStr.ToUpper)}
            If HEAD_LINE.Parameters.ContainsKey(nameStr.ToUpper) Then Return {HEAD_LINE.Parameters(nameStr.ToUpper)}
            If POST_DATA.Parameters.ContainsKey(nameStr.ToUpper) Then Return {POST_DATA.Parameters(nameStr.ToUpper)}
            If Not IsNothing(Additional) Then
                If Additional.ContainsKey(nameStr.ToUpper) Then Return {Additional(nameStr)}
            End If
        Next
        Return {DefaultReturn}
    End Function
#End Region

    <TypeConverter(GetType(ExpandableObjectConverter)), _
    EditorBrowsable(EditorBrowsableState.Always)>
    Class RawData
        Public BytesFull() As Byte = {}
        Public BytesHead() As Byte = {}
        Public BytesBody() As Byte = {}
        Property TextFull As String
        Property TextHead As String
        Property TextBody As String

        'Public LastFull(9) As Byte
        'Public LastHead(9) As Byte
        'Public LastBody(9) As Byte

        Public Overrides Function ToString() As String
            Return "HLen: " & BytesHead.Length & ", BLen: " & BytesBody.Length & "(" & TextHead & " : " & TextBody & ")"
        End Function
        '<DebuggerStepThrough()>
        Sub New(ByVal Data() As Byte, ByVal HeadOnly As Boolean)
            ' Nur Head Decodieren (für Content-Length)
            Dim IHeadEnd As Integer = Helper.BIN.BinaryFindFirst(Data, vbCrLf & vbCrLf)
            IHeadEnd = IIf(IHeadEnd = -1, Data.Length, IHeadEnd + 4)
            Dim BRawHead(IHeadEnd - 1) As Byte : Array.Copy(Data, BRawHead, IHeadEnd)
            Dim SRawHead As String = Text.Encoding.UTF8.GetString(BRawHead)
            TextHead = SRawHead : BytesHead = BRawHead
            If TextHead.Length <> BytesHead.Length Then
                IHeadEnd = 0 : TextHead = "" : BytesHead = {} ' Binärdaten
            End If

            If HeadOnly = True Then Exit Sub

            TextFull = Text.Encoding.UTF8.GetString(Data) : BytesFull = Data ' 4614
            Dim IData As Integer = Data.Length - IHeadEnd
            If IData > 1 Then
                Dim BRawCont(IData - 1) As Byte : Array.Copy(Data, IHeadEnd, BRawCont, 0, IData)
                TextBody = Text.Encoding.UTF8.GetString(Helper.BIN.TrimEmptyBytes(BRawCont)) : BytesBody = BRawCont
            End If

            ' ----------------------------------
            ' die Letzten 10 Bytes für Debugging
            ' ----------------------------------
            'If BytesFull.Length > 10 Then Array.Copy(Data, Data.Length - 10, LastFull, 0, 10)
            'If BytesHead.Length > 10 Then Array.Copy(BytesHead, BytesHead.Length - 10, LastHead, 0, 10)
            'If Not IsNothing(BytesBody) Then If BytesBody.Length > 10 Then Array.Copy(BytesBody, BytesBody.Length - 10, LastBody, 0, 10)

        End Sub
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter)), _
    EditorBrowsable(EditorBrowsableState.Always)>
    Class HeadLine
        ' ------------------------------------------------------------------------
        ' Aufbau einer Request URL
        ' ------------------------------------------------------------------------
        ' "GET", "POST", "PUT", "DELETE", "TRACE", "HEAD", "CONNECT", "M-SEARCH", "NOTIFY", "SUBSCRIBE", "UNSUBSCRIBE", "PATCH", "OPTIONS"
        ' GET (http://www.test.de:8090)/Index.html(?Param=Opt;Param=Opt) HTTP/1.1)
        ' Oder direkte URL

        ' ---------------------------------------------------------------
        ' Identifikation durch Start-Tag
        ' ---------------------------------------------------------------
        ' GET Dateiname Text.Endung?Version1.0      => Dateiname (z.B. Font)
        ' GET Dateiname+Text.Endung                 => Dateiname (ORG)
        ' GET Dateiname+Text                        => Dateiname (VLC)
        ' GET /                                     => Index.html
        ' GET /?Search=Test                         => "", Parameter ( {"Search","Test"}            ) 
        ' GET /?Search=Test&A=B                     => "", Parameter ([{"Search","Test"}, {"A","B"}]) 

        Public Source As String  ' Die Quelle der Daten
        Public ReqT As String ' Requesttyp (GET/POST/...)
        Public WebV As String ' Http-Version (http/1.1)

        Public Stat As String   ' Http StatusCode
        Public StatText As String ' Http-StatusCode-Text

        Public Port As String ' wenn abweichend von 80
        Public Host As String ' Hostname wenn verfügbar
        Public Opts As String ' Optionen: ?Name=Parameter&Name2=Parameter2
        Public Proto As String ' Prefix für Host (Http oder Https)

        Public PathOrg As String
        Public PathMod As String
        Property Parameters As New Dictionary(Of String, String)

        Public Overrides Function ToString() As String
            Return Source
        End Function
        Sub New(ByVal Headline As String)
            Source = Headline

            ' ------------------------------------------------------------
            ' erste Zeile aus HTTP Anfrage zerteilen (Pfadangabe)
            ' ------------------------------------------------------------
            Dim URL As String = ""
            Dim Splitter As String() = Split(Headline, " ")

            ' ------------------------------------------------------------
            ' Headline ist Respose z.B. "HTTP/1.1 200 OK"
            ' ------------------------------------------------------------
            If Splitter.Count > 1 Then
                If Headline.ToLower.StartsWith("http") Then
                    WebV = Splitter(0)  '                   HTTP/1.1
                    Stat = Splitter(1)  '                   StatusCode
                    If Splitter.Count > 2 Then
                        StatText = Splitter(2) '            StatusCode Text
                    End If
                    Exit Sub
                End If
            End If

            ' ------------------------------------------------------------
            ' Headline ist Request z.B. "GET Dateiname.Endung HTTP/1.1"
            ' ------------------------------------------------------------
            Select Case Splitter.Count
                Case 1
                    URL = Splitter(0)
                    Host = URLHost(URL)
                    Port = URLPort(URL)
                    PathOrg = URLAft(URL)
                    Proto = URLProto(URL)
                    Exit Sub

                Case 2 ' GET Regular URL
                    ReqT = Splitter(0)  ' GET/POST
                    URL = Splitter(1)   ' URL
                    Port = URLPort(URL)
                    Proto = URLProto(URL)
                    Opts = ""

                Case 3 ' GET Regular URL Http/1.1
                    ReqT = Splitter(0)  ' GET/POST
                    URL = Splitter(1)   ' URL
                    WebV = Splitter(2)  ' HTTP/1.1
                    Port = URLPort(URL)
                    Proto = URLProto(URL)
                    Opts = ""           ' Default setzen!

            End Select


            ' ------------------------------------------------------------
            ' Teilen in Host und Pfad
            ' ------------------------------------------------------------
            Host = URL : If URL.Contains("/") Then Host = Mid(URL, 1, InStr(URL, "/") - 1)
            Dim HSplit As String() = Split(Host, ":")
            If HSplit.Count > 1 Then
                If HSplit(0).Length > 1 Then Host = HSplit(0)
                If IsNumeric(HSplit(1)) Then Port = HSplit(1)
            End If
            PathOrg = "/" : If URL.Contains("/") Then PathOrg = Mid(URL, InStr(URL, "/"))
            Dim PSplit As String() = Split(PathOrg, "?")
            If PSplit.Count > 1 Then
                PathOrg = PSplit(0)
                Opts = PSplit(1)
            End If

            ' ------------------------------------------------------------
            ' Optionen zerteilen - Splitter für Key-Multivalue-Pairs
            ' ------------------------------------------------------------
            Dim SPL_HEAD As Char() = {"?", "&", " "}
            If Opts <> "" Then
                For Each eintrag In Opts.Split(SPL_HEAD)
                    Dim Entry As String() = Split(eintrag, "=")
                    If Parameters.ContainsKey(Entry(0).ToUpper) Then Continue For
                    If Entry.Count = 1 Then Parameters.Add(Entry(0).ToUpper, Entry(0))
                    If Entry.Count > 1 Then Parameters.Add(Entry(0).ToUpper, Uri.UnescapeDataString(Entry(1)))
                Next
            End If

            ' ------------------------------------------------------------
            ' Pfad modifizieren
            ' ------------------------------------------------------------
            PathMod = Uri.UnescapeDataString(PathOrg)
            If PathMod.StartsWith("/") Then PathMod = PathMod.Remove(0, 1)
            If PathMod.Contains("/") Then PathMod = PathMod.Replace("/", "\")

        End Sub

        Function URLHost(ByVal URL As String)
            Dim PRE1 = URLPre(URL)
            Dim AFT1 = URLAft(URL)
            Dim PRT1 = URLPort(URL)

            Dim Host As String = URL
            If PRE1 <> "" Then Host = Host.Replace(PRE1, "")
            If AFT1 <> "" Then Host = Host.Replace(AFT1, "")
            If Host.Contains(":" & PRT1) Then Host = Mid(Host, 1, InStr(Host, ":" & PRT1) - 1)
            Return Host
        End Function
        <DebuggerStepThrough()>
        Function URLProto(ByVal URL As String) As String
            Return System.Text.RegularExpressions.Regex.Match(URL, "((wss?|https?)?)").Groups(1).Value
        End Function
        <DebuggerStepThrough()>
        Function URLPre(ByVal URL As String) As String
            Return System.Text.RegularExpressions.Regex.Match(URL, "((https?)?(://)?(www.)?)").Groups(1).Value
        End Function
        <DebuggerStepThrough()>
        Function URLAft(ByVal URL As String) As String
            If URLPre(URL).Count > 0 Then URL = URL.Replace(URLPre(URL), "")
            Return System.Text.RegularExpressions.Regex.Match(URL, "(/.*)").Groups(1).Value
        End Function
        <DebuggerStepThrough()>
        Function URLPort(ByVal URL As String) As Integer
            Dim Port As Integer = 80
            Dim PRE1 = URLPre(URL)

            If PRE1.StartsWith("ws://") Then Port = 0
            If PRE1.StartsWith("wss://") Then Port = 0
            If PRE1.StartsWith("http://") Then Port = 80
            If PRE1.StartsWith("https://") Then Port = 443

            If PRE1.Length > 0 Then URL = URL.Replace(PRE1, "")
            Dim Port1 = System.Text.RegularExpressions.Regex.Match(URL, "(:[0-9]*).*").Groups(1).Value
            If Port1.Count > 1 Then Return Mid(Port1, 2)
            Return Port
        End Function
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter)), _
    EditorBrowsable(EditorBrowsableState.Always)>
    Class HeadPara
        Property Parameters As New Dictionary(Of String, String)
        Property Locations As New Dictionary(Of String, IPEndPoint)

        Public Connection As String
        Public Compression As Boolean
        Public Byterange As Range
        Public ContentLen As Integer
        Public HostName As String

        Public Overrides Function ToString() As String
            Return _
                "Conn: " & Connection & "," & _
                "Comp: " & Compression & "," & _
                "Leng: " & ContentLen & "," & _
                "Host: " & HostName
        End Function

        Sub New(ByVal Head As String)
            Dim CONN_Parameter As New List(Of String)
            Dim WDAV_Parameter As List(Of String) = {"depth"}.ToList
            Dim SOCK_Parameter As List(Of String) = {"Sec-WebSocket-Extensions", "Sec-WebSocket-Key", "Sec-WebSocket-Version", "Upgrade", "Sec-WebSocket-Accept",
                                                     "Sec-WebSocket-Protocol"}.ToList
            Dim UPNP_Parameter As List(Of String) = {"SERVER", "DATE", "CACHE-CONTROL", "NTS", "NT", "USN", "ST", "MX", "SID", "HOST", "OPT", _
                                                     "CALLBACK", "TIMEOUT", "LOCATION", "CONFIGID.UPNP.ORG", "BOOTID.UPNP.ORG", "SEARCHPORT.UPNP.ORG", _
                                                     "SOAPACTION", "Application-URL", "Access-Control-Expose-Headers", "01-NLS", "X-User-Agent"}.ToList
            Dim HTTP_Parameter As List(Of String) = {"HOST", "Connection", "Origin", "Content-Length", "Upgrade-Insecure-Requests", "Content-Type", _
                                                     "X-Requested-With", "User-Agent", "Accept", "Accept-Encoding", "Accept-Language", "Cookie", _
                                                     "Cache-Control", "Referer", "Pragma", "DNT", "Range", "Icy-MetaData", "transferMode.dlna.org"}.ToList
            Dim SPL_Parameter As Char() = {":", " "} ' Splitter für Key-Value-Pairs

            CONN_Parameter.AddRange(HTTP_Parameter)
            CONN_Parameter.AddRange(UPNP_Parameter)
            CONN_Parameter.AddRange(SOCK_Parameter)
            CONN_Parameter.AddRange(WDAV_Parameter)
            CONN_Parameter = Helper.LST.ListToUpper(CONN_Parameter)

            Dim RAW_Zeilen As String() = Split(Head, vbCrLf)
            For i As Integer = 1 To RAW_Zeilen.Count - 1
                Dim Eintrag As String = RAW_Zeilen(i)
                If Trim(Eintrag.Replace(vbCr, "").Replace(vbLf, "")) = "" Then Continue For
                Dim ParamSplitter As List(Of String) = Eintrag.Split(SPL_Parameter).ToList
                If CONN_Parameter.Contains(ParamSplitter(0).ToUpper) Then
                    If Parameters.ContainsKey(ParamSplitter(0).ToUpper) = False Then
                        Parameters.Add(ParamSplitter(0).ToUpper, Eintrag.Split(SPL_Parameter, 2)(1))
                    End If
                End If
            Next

            ContentLen = CInt(GetField("Content-Length", 0).Trim)
            Connection = GetField("Connection", "").Trim
            Compression = GetField("Accept-Encoding", "").Contains("gzip")
            HostName = GetField("Host", "").Trim

            Dim SRange As String = ""
            If Parameters.ContainsKey("range") Then SRange = Parameters("range")
            Byterange = New Range(SRange)

            ' UPNP-Relevant -> Konvertieren zu IPEndpoint
            If GetField("LOCATION", "") <> "" Then Locations.Add("LOCATION", UrlToEndpoint(GetField("LOCATION", "")))
            If GetField("APPLICATION-URL", "") <> "" Then Locations.Add("APPLICATION-URL", UrlToEndpoint(GetField("APPLICATION-URL", "")))
        End Sub

        Function UrlToEndpoint(ByVal Url As String) As IPEndPoint
            Url = Url.Replace("localhost", "127.0.0.1")
            Dim Parse As Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(Url, "https?://([0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3})(:[0-9]{0,8})?")
            Dim Port As String = 80 : If Parse.Groups(2).Value <> "" Then Port = Parse.Groups(2).Value.Replace(":", "")
            Dim IPEP As New IPEndPoint(IPAddress.Parse(Parse.Groups(1).Value), Port)
            Return IPEP
        End Function
        Function GetParameters() As String
            Dim SBuild As New System.Text.StringBuilder
            For Each eintrag In Parameters
                SBuild.AppendLine(eintrag.Key & ": " & eintrag.Value)
            Next
            Return SBuild.ToString
        End Function
        Sub RemoveFields(ByVal Names As String())
            For Each eintrag In Names
                Parameters.Remove(eintrag.ToUpper)
            Next
        End Sub

        <DebuggerStepThrough()>
        Public Overloads Function GetField(ByVal Name As String, Optional ByVal DefaultReturn As String = "") As String
            Return GetField({Name}, DefaultReturn)
        End Function
        <DebuggerStepThrough()>
        Public Overloads Function GetField(ByVal Name As String(), Optional ByVal DefaultReturn As String = "") As String
            For Each nameStr In Name
                If Parameters.ContainsKey(nameStr.ToUpper) Then Return Parameters(nameStr.ToUpper)
            Next
            Return DefaultReturn
        End Function

        <DebuggerStepThrough()>
        Class Range
            Public Min As Double
            Public Max As Double
            Public Len As Double
            Public Ret As Double

            Public Overrides Function ToString() As String
                Return Min & " - " & Max & " = " & Len
            End Function

            Sub New(ByVal Range As String)
                ' ----------------------------------------------
                ' Byte-Range auswerten
                ' ----------------------------------------------
                ' Beispiele anhand einer Max.Größe von 600  (Stream.Length = 600), Max ist dann 599!
                ' Auch bei kompletter Übertragung Range angeben weil damit player den support erkennen
                ' ----------------------------------------------
                ' Request           =>      Response (Mit Leerzeichen! bei Response)
                ' Range: bytes=100-         Content-Range: bytes 100-599/600    |   Content-Length: 500
                ' Range: bytes=0-           Content-Range: bytes 0-599/600      |   Content-Length: 600
                ' Range: bytes=-200         Content-Range: bytes 0-200/600      |   Content-Length: 201
                ' Range: bytes=200-399      Content-Range: bytes 200-399/600    |   Content-Length: 200
                ' Range: bytes=0-199        Content-Range: bytes 0-199/600      |   Content-Length: 200
                ' ----------------------------------------------
                If Range <> "" Then
                    Dim BRange As New Dictionary(Of String, Double)
                    Range = Mid(Range, InStr(Range, "=") + 1)
                    Dim RangeSPL As String() = Split(Range, "-")
                    Dim ABSMin As String = RangeSPL(0).Replace(Chr(0), "")
                    Dim ABSMax As String = RangeSPL(1).Replace(Chr(0), "")
                    ' Minimale Range
                    If ABSMin = "" Then Min = 0
                    If IsNumeric(ABSMin) Then Min = CDbl(ABSMin)
                    ' Maximale Range
                    If ABSMax = "" Then Max = -1
                    If IsNumeric(ABSMax) Then Max = CDbl(ABSMax)
                    ' Berechnete Länge 
                    Len = CDbl(Max + 1 - Min)
                    Ret = 206
                Else
                    Min = 0
                    Max = -1
                    Len = -1
                    Ret = 200
                End If
            End Sub
            Function SetMax(ByVal Value As Double) As Range
                If Max > Value Then MsgBox("RangeMax > DataMax") : Return Me
                If Max > -1 Then Return Me
                Max = Value - 1
                Len = CDbl(Max - Min + 1)
                Return Me
            End Function
        End Class
    End Class

    <TypeConverter(GetType(ExpandableObjectConverter)), _
    EditorBrowsable(EditorBrowsableState.Always)>
    Class PostData
        Public POST_TEXT As String = ""
        Public POST_HEAD As String = ""
        Public Parameters As New Dictionary(Of String, String)

        Public Overrides Function ToString() As String
            Return String.Join(";", Parameters)
        End Function

        Sub New(ByVal _Head_Line As HeadLine, ByVal _HEAD_PARA As HeadPara, ByVal RawContent As Byte())
            ' -----------------------------------
            ' Früh aussteigen
            ' -----------------------------------
            If IsNothing(RawContent) Then Exit Sub
            If _Head_Line.ReqT <> "POST" Then Exit Sub
            RawContent = Helper.BIN.TrimLastEmptyBytes(RawContent)
            If RawContent.Count = 0 Then Exit Sub

            ' -----------------------------------
            ' Content-Type Text oder WebKitForm
            ' -----------------------------------
            Dim WKFB As Integer = Helper.BIN.BinaryFindFirst(RawContent, "------WebKitFormBoundary")
            Dim ContText As Boolean = IIf(WKFB = 0, False, True)

            If ContText = False Then
                ' POST_FORM = New PostForm(RawContent)
            Else
                Dim SRawData As String = Text.Encoding.UTF8.GetString(RawContent)
                POST_TEXT = Uri.UnescapeDataString(SRawData)
                Try
                    If Not String.IsNullOrWhiteSpace(POST_TEXT) Then
                        For Each eintrag In POST_TEXT.Split("&")
                            Dim Splitter As String() = {Mid(eintrag, 1, InStr(eintrag, "=") - 1), Mid(eintrag, InStr(eintrag, "=") + 1)}
                            If Splitter.Count <> 2 Then Continue For
                            Parameters.Add(Splitter(0).Trim.ToUpper, Splitter(1).Trim)
                        Next
                    End If
                Catch : End Try
            End If
        End Sub

        Public Overloads Function GetField(ByVal Name As String, Optional ByVal DefaultReturn As String = "") As String
            Return GetField({Name}, DefaultReturn)
        End Function
        Public Overloads Function GetField(ByVal Name As String(), Optional ByVal DefaultReturn As String = "") As String
            For Each nameStr In Name
                If Parameters.ContainsKey(nameStr.ToLower) Then Return Parameters(nameStr.ToLower)
            Next
            Return DefaultReturn
        End Function
    End Class

End Class