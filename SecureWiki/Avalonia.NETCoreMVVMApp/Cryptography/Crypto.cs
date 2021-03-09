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
            // var plainTextBytes = Convert.FromBase64String(plainText);
            return rsa.SignData(plainTextBytes, SHA256.Create());
        }

        // Verify the signature from signedData hash, plaintext and public key stored in datafile object
        public bool VerifyData(byte[] key, string plainText, byte[] signedData)
        {
            try
            {
                RSACryptoServiceProvider rsa = new();
                rsa.ImportRSAPublicKey(key, out _);

                var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
                // var plainTextBytes = Convert.FromBase64String(plainText);
                return rsa.VerifyData(plainTextBytes, SHA256.Create(), signedData);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}