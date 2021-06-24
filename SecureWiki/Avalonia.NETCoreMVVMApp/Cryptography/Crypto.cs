using System;
using System.Security.Cryptography;

namespace SecureWiki.Cryptography
{
    public static class Crypto
    {
        private const int TAG_SIZE = 16;
        private const int NONCE_SIZE = 12;

        public static byte[]? Encrypt(byte[] plainText, byte[] key)
        {
            var tag = new byte[TAG_SIZE];
            var nonce = new byte[NONCE_SIZE];
            var cipherText = new byte[plainText.Length];
            // Use given symmetric key
            using (AesGcm aesAlg = new(key))
            {
                try
                {
                    aesAlg.Encrypt(nonce, plainText, cipherText, tag);
                }
                catch (CryptographicException e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }

            return Utilities.ByteArrayCombiner.Combine(tag, Utilities.ByteArrayCombiner.Combine(nonce, cipherText));
        }

        public static byte[]? Decrypt(byte[] ciphertext, byte[] key)
        {
            var tag = Utilities.ByteArrayCombiner.SubArray(ciphertext, 0, TAG_SIZE);
            var nonce = Utilities.ByteArrayCombiner.SubArray(ciphertext, TAG_SIZE, NONCE_SIZE);
            var encryptedData =
                Utilities.ByteArrayCombiner.SubArray(ciphertext, TAG_SIZE + NONCE_SIZE,
                    ciphertext.Length - tag.Length - nonce.Length);
            var plaintext = new byte[encryptedData.Length];
            // Use given symmetric key
            using (AesGcm aesAlg = new(key))
            {
                try
                {
                    aesAlg.Decrypt(nonce, encryptedData, tag, plaintext);
                }
                catch (CryptographicException e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }

            return plaintext;
        }

        // Encrypts plainText bytes using public key.
        // Encryption algorithm is RSA2048 with PKCS1 padding
        public static byte[]? RSAEncrypt(byte[] data, byte[] publicKey)
        {
            try
            {
                byte[] encryptedData;
                //Create a new instance of RSACryptoServiceProvider.
                using (RSACryptoServiceProvider rsa = new())
                {
                    rsa.ImportRSAPublicKey(publicKey, out _);

                    encryptedData = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA1);
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
        public static byte[]? RSADecrypt(byte[] data, byte[] privateKey)
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
                    decryptedData = rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA1);
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

        // Generate both symmetric key and IV
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
            return rsa.SignData(data, SHA256.Create()) ;
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