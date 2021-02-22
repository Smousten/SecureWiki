using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecureWiki.Cryptography
{
    public class Crypto
    {
        private readonly Aes aesAlg;
        private string key = "RP4Jvz5Gv0Fxret3YoOJzrA+BkV2PTK1QcuAucAgVOc=";
        private string iv = "awFCaG5DVbr+3zaTRM4O2A==";

        public Crypto()
        {
            aesAlg = Aes.Create();
            aesAlg.Key = Convert.FromBase64String(key);
            aesAlg.IV = Convert.FromBase64String(iv);
        }

        public byte[] EncryptAESStringToBytes(string plainText)
        {
            // Ensure argument validity
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));
            byte[] encrypted;

            // Build encryptor for transforming the plaintext to ciphertext
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

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

            return encrypted;
        }

        public string DecryptAESBytesToString(byte[] cipherText)
        {
            // Ensure argument validity
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException("iv");

            string plaintext = null;

            // Build encryptor for transforming the ciphertext to plaintext
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

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

            return plaintext;
        }

        public (byte[] privateKey, byte[] publicKey) generateRSAparams()
        {
            // Generate a key pair.  
            RSA rsa = RSA.Create();  
            
            // Export the RSA keys and return them  
            var privateKey = rsa.ExportRSAPrivateKey();
            var publicKey = rsa.ExportRSAPublicKey();
            return (privateKey, publicKey);
        }

        public (byte[] Key, byte[] IV) generateAESparams()
        {
            Aes aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }

        // public byte[] ComputeSha256(string plainText)
        // {
        //     using (SHA256 sha256 = SHA256.Create())
        //     {
        //         var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
        //         return sha256.ComputeHash(plainTextBytes);
        //     }
        // }

        public byte[] SignData(RSAParameters key, string plainText)
        {
            RSACryptoServiceProvider rsa = new();
            rsa.ImportParameters(key);
            
            var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
            return rsa.SignData(plainTextBytes, SHA256.Create());
        }

        public bool VerifyData(RSAParameters key, string plainText, byte[] signedData)
        {
            try
            {
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(key);

                var plainTextBytes = Encoding.ASCII.GetBytes(plainText);
                return rsa.VerifyData(plainTextBytes, SHA256.Create(), signedData);
            }
            catch(CryptographicException e)
            {

                return false;
            }
        }
        
    }
}