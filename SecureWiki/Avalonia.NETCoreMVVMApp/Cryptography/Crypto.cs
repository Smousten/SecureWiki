using System;
using System.IO;
using System.Security.Cryptography;

namespace SecureWiki.Cryptography
{
    public static class Crypto
    {
        // Encrypts plainText bytes using input key and iv.
        // Encryption algorithm is aes256 with PKCS7 padding
        public static byte[]? Encrypt(byte[] plainText, byte[] key, byte[] iv)
        {
            // Ensure argument validity
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));
            if (iv == null || iv.Length <= 0)
                throw new ArgumentNullException(nameof(iv));

            // Use given symmetric key and iv
            using Aes aesAlg = Aes.Create();
            aesAlg.Key = key;
            aesAlg.IV = iv;

            // Build encryptor for transforming the plaintext to ciphertext
            using ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            aesAlg.Padding = PaddingMode.PKCS7;

            return PerformCryptography(plainText, encryptor);
        }

        // Decrypts ciphertext bytes using input key and iv.
        // Decryption algorithm is AES256 with PKCS7 padding
        public static byte[]? Decrypt(byte[] cipherText, byte[] key, byte[] iv)
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

        // Perform symmetric cryptography on input data with crypto transformer (encryptor/decryptor)
        private static byte[]? PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
        {
            using var ms = new MemoryStream();
            using var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);
            try
            {
                cryptoStream.Write(data, 0, data.Length);
                cryptoStream.FlushFinalBlock();
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

            return ms.ToArray();
        }

        // Encrypts plainText bytes using public key.
        // Encryption algorithm is RSA2048 with PKCS1 padding
        public static byte[]? RSAEncryptWithPublicKey(byte[] data, byte[] publicKey)
        {
            try
            {
                byte[] encryptedData;
                //Create a new instance of RSACryptoServiceProvider.
                using (RSACryptoServiceProvider rsa = new())
                {
                    rsa.ImportRSAPublicKey(publicKey, out _);

                    encryptedData = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
                }

                return encryptedData;
            }
            //Catch and display a CryptographicException  
            //to the console.
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);

                return null;
            }
        }

        // Decrypts ciphertext bytes using private key.
        // Decryption algorithm is RSA2048 with PKCS1 padding
        public static byte[]? RSADecryptWithPrivateKey(byte[] data, byte[] privateKey)
        {
            try
            {
                byte[] decryptedData;
                //Create a new instance of RSACryptoServiceProvider.
                using (RSACryptoServiceProvider rsa = new())
                {
                    rsa.ImportRSAPrivateKey(privateKey, out _);

                    //Decrypt the passed byte array and specify OAEP padding.  
                    //OAEP padding is only available on Microsoft Windows XP or
                    //later.  
                    decryptedData = rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);
                }

                return decryptedData;
            }
            //Catch and display a CryptographicException  
            //to the console.
            catch (CryptographicException e)
            {
                Console.WriteLine(e.ToString());

                return null;
            }
        }

        // Generate asymmetric key pair
        public static (byte[] privateKey, byte[] publicKey) GenerateRSAParams()
        {
            RSA rsa = RSA.Create();
            var privateKey = rsa.ExportRSAPrivateKey();
            var publicKey = rsa.ExportRSAPublicKey();
            return (privateKey, publicKey);
        }

        // Generate symmetric key
        public static byte[] GenerateSymmKey()
        {
            Aes aes = Aes.Create();
            aes.GenerateKey();
            return aes.Key;
        }
        
        // Generate IV
        public static byte[] GenerateIV()
        {
            Aes aes = Aes.Create();
            aes.GenerateIV();
            return aes.IV;
        }
        
        public static (byte[] key, byte[] iv) GenerateAESParams()
        {
            Aes aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }
        
        // Returns signed plaintext using private key stored in datafile object
        public static byte[] SignData(byte[] key, byte[] data)
        {
            RSACryptoServiceProvider rsa = new();
            rsa.ImportRSAPrivateKey(key, out _);
            return rsa.SignData(data, SHA256.Create());
        }

        // Verify the signature from signedData hash, plaintext and public key stored in datafile object
        public static bool VerifyData(byte[] key, byte[] data, byte[] signedData)
        {
            try
            {
                RSACryptoServiceProvider rsa = new();
                rsa.ImportRSAPublicKey(key, out _);
                return rsa.VerifyData(data, SHA256.Create(), signedData);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}