Imports System.IO
Imports Org.BouncyCastle.Crypto.Modes
Imports Org.BouncyCastle.Crypto.Engines
Imports Org.BouncyCastle.Crypto.Parameters
Imports Org.BouncyCastle.Crypto
Imports System.Security.Cryptography

Public Class Crypto

    ' The DIGEST is the Output of a HASH-Function
    ' The IV and the NONCE are Interoperable

    Class RNG
        Public Shared Function TestingNonceIV() As Byte()
            Return New Byte() {1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4}
        End Function
        Public Shared Function TestingSecretKey() As Byte()
            Return New Byte() {1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8}
        End Function
        Public Shared Function RandomSecretKey(ByVal Length As Integer) As Byte()
            Dim Sec = System.Security.Cryptography.RNGCryptoServiceProvider.Create()
            Dim Bin(Length - 1) As Byte : Sec.GetNonZeroBytes(Bin)
            Return Bin
        End Function
    End Class

    Class HKDF
        Private keyedHash As Func(Of Byte(), Byte(), Byte())

        Function deriveKey(ByVal Secret As Byte(),
                   ByVal Salt_Nonce As Byte(),
                   ByVal Info As Byte(),
                   Optional ByVal Length As Integer = 16)
            If IsNothing(Salt_Nonce) Then Salt_Nonce = New Byte() {0}
            Dim HKDF As New Crypto.HKDF()
            Return HKDF.Derive(Salt_Nonce, Secret, Info, Length)
        End Function
        Function deriveKey(ByVal Secret As Byte(),
                           ByVal Salt_Nonce As Byte(),
                           ByVal Info As String,
                           Optional ByVal Length As Integer = 16)

            Dim BInfo As Byte() = System.Text.Encoding.UTF8.GetBytes(Info)
            Return deriveKey(Secret, Salt_Nonce, BInfo, Length)
        End Function

        <DebuggerStepThrough()>
        Public Sub New()
            Dim hmac = New HMACSHA256()
            keyedHash = Function(key, message)
                            hmac.Key = key
                            Return hmac.ComputeHash(message)
                        End Function
        End Sub
        <DebuggerStepThrough()>
        Private Function Extract(ByVal salt As Byte(), ByVal inputKeyMaterial As Byte()) As Byte()
            Return keyedHash(salt, inputKeyMaterial)
        End Function
        <DebuggerStepThrough()>
        Private Function Expand(ByVal prk As Byte(), ByVal info As Byte(), ByVal outputLength As Integer) As Byte()
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
        <DebuggerStepThrough()>
        Private Function Derive(ByVal salt As Byte(), ByVal inputKeyMaterial As Byte(), ByVal info As Byte(), ByVal outputLength As Integer) As Byte()
            Dim prk = Extract(salt, inputKeyMaterial)
            Dim result = Expand(prk, info, outputLength)
            Return result
        End Function

        Public Sub Test()
            Dim hkdf = New HKDF()

            If True Then
                Dim ikm = Helper.HEX.HexStringToByteArray("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b")
                Dim salt = Helper.HEX.HexStringToByteArray("000102030405060708090a0b0c")
                Dim info = Helper.HEX.HexStringToByteArray("f0f1f2f3f4f5f6f7f8f9")
                Dim L = 42
                Dim prk = "077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5"
                Dim okm = "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865"
                Dim actualPrk = Helper.HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = Helper.HEX.ByteArrayToHexString(hkdf.deriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL42 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL42 - OK")
            End If

            If True Then
                Dim ikm = Helper.HEX.HexStringToByteArray("000102030405060708090a0b0c0d0e0f" +
                                            "101112131415161718191a1b1c1d1e1f" +
                                            "202122232425262728292a2b2c2d2e2f" +
                                            "303132333435363738393a3b3c3d3e3f" +
                                            "404142434445464748494a4b4c4d4e4f")

                Dim salt = Helper.HEX.HexStringToByteArray("606162636465666768696a6b6c6d6e6f" +
                                             "707172737475767778797a7b7c7d7e7f" +
                                             "808182838485868788898a8b8c8d8e8f" +
                                             "909192939495969798999a9b9c9d9e9f" +
                                             "a0a1a2a3a4a5a6a7a8a9aaabacadaeaf")
                Dim info = Helper.HEX.HexStringToByteArray("b0b1b2b3b4b5b6b7b8b9babbbcbdbebf" +
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

                Dim actualPrk = Helper.HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = Helper.HEX.ByteArrayToHexString(hkdf.deriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL82 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL82 - OK")
            End If

            If True Then
                Dim ikm = Helper.HEX.HexStringToByteArray("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b")
                Dim salt = New Byte(-1) {}
                Dim info = New Byte(-1) {}
                Dim L = 42
                Dim prk = "19ef24a32c717b167f33a91d6f648bdf96596776afdb6377ac434c1c293ccb04"
                Dim okm = "8da4e775a563c18f715f802a063c5a31" & "b8a11f5c5ee1879ec3454e5f3c738d2d" & "9d201395faa4b61a96c8"
                Dim actualPrk = Helper.HEX.ByteArrayToHexString(hkdf.Extract(salt, ikm))
                Dim actualOkm = Helper.HEX.ByteArrayToHexString(hkdf.deriveKey(salt, ikm, info, L))
                If prk = actualPrk Then Console.WriteLine("PRKL42_2 - OK")
                If okm = actualOkm Then Console.WriteLine("PRKL42_2 - OK")
            End If
        End Sub
    End Class

    Class AES_GCM
        Private Const KEY_BIT_SIZE = 16 * 8
        Private Const MAC_BIT_SIZE = 16 * 8
        Private Const NONCE_BIT_SIZE = 12 * 8

        Public Shared Function EncryptWithKey(ByVal Text As Byte(),
                                              ByVal Key As Byte(),
                                              ByVal IV As Byte()) As Byte()

            If IsNothing(Text) Then Return Nothing

            Dim nonSecretPayload = New Byte() {}
            If Key Is Nothing OrElse Key.Length <> KEY_BIT_SIZE / 8 Then
                Throw New ArgumentException(String.Format("Key needs to be {0} bit!", KEY_BIT_SIZE), "key")
            End If

            Dim Cipher = New GcmBlockCipher(New AesEngine())
            Dim Parameters = New AeadParameters(New KeyParameter(Key), MAC_BIT_SIZE, IV, nonSecretPayload)
            Cipher.Init(True, Parameters)
            Dim cipherText = New Byte(Cipher.GetOutputSize(Text.Length) - 1) {}
            Dim len = Cipher.ProcessBytes(Text, 0, Text.Length, cipherText, 0)
            Cipher.DoFinal(cipherText, len)

            Using combinedStream = New MemoryStream()
                Using binaryWriter = New BinaryWriter(combinedStream)
                    ' binaryWriter.Write(nonSecretPayload)
                    ' binaryWriter.Write(IV) ' Bei Meta falsches Ergebnis ... 
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
