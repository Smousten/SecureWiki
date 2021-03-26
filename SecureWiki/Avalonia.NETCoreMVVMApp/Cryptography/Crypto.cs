using System;
using System.IO;
using System.Security.Cryptography;

namespace SecureWiki.Cryptography
{
    public class Crypto
    {
        public byte[] Encrypt(byte[] plainText, byte[] key, byte[] iv)
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

        public byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
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

        private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);
            cryptoStream.Write(data, 0, data.Length);
            cryptoStream.FlushFinalBlock();

            return ms.ToArray();
        }

        // Generate asymmetric key pair
        public (byte[] privateKey, byte[] publicKey) GenerateRSAParams()
        {
            RSA rsa = RSA.Create();
            var privateKey = rsa.ExportRSAPrivateKey();
            var publicKey = rsa.ExportRSAPublicKey();
            return (privateKey, publicKey);
        }

        // Generate symmetric key and IV
        public (byte[] Key, byte[] IV) GenerateAESParams()
        {
            Aes aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }

        // Returns signed plaintext using private key stored in datafile object
        public byte[] SignData(byte[] key, byte[] plainText)
        {
            RSACryptoServiceProvider rsa = new();
            rsa.ImportRSAPrivateKey(key, out _);
            return rsa.SignData(plainText, SHA256.Create());
        }

        // Verify the signature from signedData hash, plaintext and public key stored in datafile object
        public bool VerifyData(byte[] key, byte[] plainText, byte[] signedData)
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
    }
}