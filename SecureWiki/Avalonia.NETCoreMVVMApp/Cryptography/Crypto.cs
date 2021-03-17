using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecureWiki.Cryptography
{
    public class Crypto
    {
        public byte[] EncryptAESStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            // Ensure argument validity
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));
            byte[] encrypted;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                // Build encryptor for transforming the plaintext to ciphertext
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                aesAlg.Padding = PaddingMode.PKCS7;

                // Build necessary streams
                using (MemoryStream msEncrypt = new())
                {
                    using (CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new(csEncrypt))
                        {
                            //Write input plaintext to the stream writer
                            swEncrypt.Write(plainText);
                        }

                        // Convert memory stream to byte array
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            return encrypted;
        }

        public byte[] EncryptAesBytesToBytes(byte[] plainText, byte[] key, byte[] iv)
        {
            // Ensure argument validity
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = key;
            aesAlg.IV = iv;

            // Build encryptor for transforming the plaintext to ciphertext
            using ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            aesAlg.Padding = PaddingMode.PKCS7;

            return PerformCryptography(plainText, encryptor);
        }

        private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();

            return ms.ToArray();
        }

        public string DecryptAESBytesToString(byte[] cipherText, byte[] key, byte[] iv)
        {
            // Ensure argument validity
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException("iv");

            string plaintext;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;
                // Build encryptor for transforming the ciphertext to plaintext
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                aesAlg.Padding = PaddingMode.PKCS7;

                // Build necessary streams
                using (MemoryStream msDecrypt = new(cipherText))
                {
                    using (CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new(csDecrypt))
                        {
                            //Read input ciphertext from the stream reader
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }
        
        public byte[] DecryptAesBytesToBytes(byte[] cipherText, byte[] key, byte[] iv)
        {
            // Ensure argument validity
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException("iv");

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            // Build encryptor for transforming the ciphertext to plaintext
            using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            aes.Padding = PaddingMode.PKCS7;
            return PerformCryptography(cipherText, decryptor);
        }

        public (byte[] privateKey, byte[] publicKey) GenerateRSAParams()
        {
            // Generate a key pair.  
            RSA rsa = RSA.Create();
            // Export the RSA keys and return them  
            var privateKey = rsa.ExportRSAPrivateKey();
            var publicKey = rsa.ExportRSAPublicKey();
            return (privateKey, publicKey);
        }

        public (byte[] Key, byte[] IV) GenerateAESParams()
        {
            Aes aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }

        // Returns signed plaintext using private key stored in datafile object
        public byte[] SignData(byte[] key, string plainText)
        {
            RSACryptoServiceProvider rsa = new();
            rsa.ImportRSAPrivateKey(key, out _);
            var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
            return rsa.SignData(plainTextBytes, SHA256.Create());
        }

        public byte[] SignBytes(byte[] key, byte[] plainText)
        {
            RSACryptoServiceProvider rsa = new();
            rsa.ImportRSAPrivateKey(key, out _);
            return rsa.SignData(plainText, SHA256.Create());
        }

        // Verify the signature from signedData hash, plaintext and public key stored in datafile object
        public bool VerifyData(byte[] key, string plainText, byte[] signedData)
        {
            try
            {
                RSACryptoServiceProvider rsa = new();
                rsa.ImportRSAPublicKey(key, out _);
                var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
                return rsa.VerifyData(plainTextBytes, SHA256.Create(), signedData);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        public bool VerifyBytes(byte[] key, byte[] plainText, byte[] signedData)
        {
            try
            {
                RSACryptoServiceProvider rsa = new();
                rsa.ImportRSAPublicKey(key, out _);
                return rsa.VerifyData(plainText, SHA256.Create(), signedData);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/standard/security/walkthrough-creating-a-cryptographic-application
        // write encrypted cipher content to new file
        private void EncryptFile(string inFile, byte[] key, byte[] iv)
        {
            // Create instance of Aes for
            // symmetric encryption of the data.
            Aes aes = Aes.Create();

            aes.Key = key;
            aes.IV = iv;

            // Build encryptor for transforming the plaintext to ciphertext
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            // Write the following to the FileStream
            // - the encrypted cipher content

            int startFileName = inFile.LastIndexOf("\\") + 1;
            // Change the file's extension to ".enc"
            string outFile = "EncrFolder" + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) +
                             ".enc";

            using (FileStream outFs = new FileStream(outFile, FileMode.Create))
            {
                // Now write the cipher text using
                // a CryptoStream for encrypting.
                using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, encryptor, CryptoStreamMode.Write))
                {
                    // By encrypting a chunk at
                    // a time, you can save memory
                    // and accommodate large files.
                    int count = 0;
                    int offset = 0;

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = aes.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamEncrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                        } while (count > 0);

                        inFs.Close();
                    }

                    outStreamEncrypted.FlushFinalBlock();
                    outStreamEncrypted.Close();
                }

                outFs.Close();
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/standard/security/walkthrough-creating-a-cryptographic-application
        // write encrypted cipher content to new file
        private void DecryptFile(string inFile, byte[] key, byte[] iv)
        {
            // Create instance of Aes for
            // symetric decryption of the data.
            Aes aes = Aes.Create();

            aes.Key = key;
            aes.IV = iv;

            // Construct the file name for the decrypted file.
            string outFile = "DecrFolder" + inFile.Substring(0, inFile.LastIndexOf(".")) + ".txt";

            // Use FileStream objects to read the encrypted
            // file (inFs) and save the decrypted file (outFs).
            using (FileStream inFs = new FileStream("EncrFolder" + inFile, FileMode.Open))
            {
                // inFs.Seek(0, SeekOrigin.Begin);
                // inFs.Seek(0, SeekOrigin.Begin);
                // inFs.Read(LenK, 0, 3);
                // inFs.Seek(4, SeekOrigin.Begin);
                // inFs.Read(LenIV, 0, 3);
                //
                // // Convert the lengths to integer values.
                // int lenK = BitConverter.ToInt32(LenK, 0);
                // int lenIV = BitConverter.ToInt32(LenIV, 0);
                //
                // // Determine the start postition of
                // // the ciphter text (startC)
                // // and its length(lenC).
                // int startC = lenK + lenIV + 8;
                // int lenC = (int) inFs.Length - startC;
                //
                // // Create the byte arrays for
                // // the encrypted Aes key,
                // // the IV, and the cipher text.
                // byte[] KeyEncrypted = new byte[lenK];
                // byte[] IV = new byte[lenIV];
                //
                // // Extract the key and IV
                // // starting from index 8
                // // after the length values.
                // inFs.Seek(8, SeekOrigin.Begin);
                // inFs.Read(KeyEncrypted, 0, lenK);
                // inFs.Seek(8 + lenK, SeekOrigin.Begin);
                // inFs.Read(IV, 0, lenIV);
                // Directory.CreateDirectory(DecrFolder);
                // // Use RSACryptoServiceProvider
                // // to decrypt the AES key.
                // byte[] KeyDecrypted = rsa.Decrypt(KeyEncrypted, false);
                //
                // Decrypt the key.
                ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);

                // Decrypt the cipher text from
                // from the FileSteam of the encrypted
                // file (inFs) into the FileStream
                // for the decrypted file (outFs).
                using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                {
                    int count = 0;
                    int offset = 0;

                    // blockSizeBytes can be any arbitrary size.
                    int blockSizeBytes = aes.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];

                    // By decrypting a chunk a time,
                    // you can save memory and
                    // accommodate large files.

                    // Start at the beginning
                    // of the cipher text.
                    // inFs.Seek(startC, SeekOrigin.Begin);
                    using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamDecrypted.Write(data, 0, count);
                        } while (count > 0);

                        outStreamDecrypted.FlushFinalBlock();
                        outStreamDecrypted.Close();
                    }

                    outFs.Close();
                }

                inFs.Close();
            }
        }
    }
}