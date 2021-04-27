using System;
using System.Linq;
using System.Security.Cryptography;
using DynamicData;
using NUnit.Framework;
using SecureWiki.Cryptography;

namespace SecureWikiTests
{
    public class CryptoTests
    {
        private Byte[] plainText = new Byte[20];
        private Byte[] symmKey;
        private Byte[] iv;
        private Byte[] priKey;
        private Byte[] pubKey;
        
        
        [SetUp]
        public void Setup()
        {
            Random rnd = new Random();
            rnd.NextBytes(plainText);
        }

        // Test that crypto module correctly generates 256-bit aes key and iv
        [Test]
        public void TestGenerateAESParams()
        {
            (symmKey, iv) = Crypto.GenerateAESParams();
            Assert.AreEqual(256/8, symmKey.Length);
            Assert.AreEqual(128/8, iv.Length);
        }
        
        // Test that crypto module correctly generates 2048-bit rsa key
        [Test]
        public void TestGenerateRSAParams()
        {
            (priKey, pubKey) = Crypto.GenerateRSAParams();
            Console.WriteLine(priKey.Length);
            Console.WriteLine(pubKey.Length);
        }
        
        // Test that crypto module correctly encrypts and decrypts random 20 byte array
        // using symmetric algorithm
        [Test]
        public void TestEncryptDecryptAES()
        {
            // Generate symmetric key 
            (symmKey, iv) = Crypto.GenerateAESParams();

            // Encrypt random 20 byte array
            var encryptedBytes = Crypto.Encrypt(plainText, symmKey, iv);
            
            // Decrypt random 20 byte array
            var decryptedBytes = Crypto.Decrypt(encryptedBytes!, symmKey, iv);

            Assert.AreEqual(plainText, decryptedBytes);
        }
        
        // Test that crypto module fails to decrypt if wrong symmetric key is used.
        // using symmetric algorithm
        [Test]
        public void TestEncryptDecryptAESFail()
        {
            // Generate symmetric key 
            (symmKey, iv) = Crypto.GenerateAESParams();
            var (newSymmKey, newIV) = Crypto.GenerateAESParams();

            // Encrypt random 20 byte array
            var encryptedBytes = Crypto.Encrypt(plainText, symmKey, iv);
            
            // Decrypt random 20 byte array
            var decryptedBytes = Crypto.Decrypt(encryptedBytes!, newSymmKey, iv);
            Assert.AreEqual(null, decryptedBytes);
        }
        
        // Test that crypto module correctly encrypts and decrypts random 20 byte array 
        // using asymmetric algorithm
        [Test]
        public void TestEncryptDecryptRSA()
        {
            // Generate symmetric key 
            (priKey, pubKey) = Crypto.GenerateRSAParams();

            // Encrypt random 20 byte array
            var encryptedBytes = Crypto.RSAEncryptWithPublicKey(plainText, pubKey);
            
            // Decrypt random 20 byte array
            var decryptedBytes = Crypto.RSADecryptWithPrivateKey(encryptedBytes!, priKey);

            Assert.AreEqual(plainText, decryptedBytes);
        }
        
        // Test that crypto module correctly encrypts and decrypts random 20 byte array 
        // using asymmetric algorithm
        [Test]
        public void TestSignAndVerify()
        {
            // Generate symmetric key 
            (priKey, pubKey) = Crypto.GenerateRSAParams();

            // sign plaintext using private key
            var signature = Crypto.SignData(priKey, plainText);
            
            // verify signature using public key and plaintext
            var result = Crypto.VerifyData(pubKey, plainText, signature);
            Assert.True(result);
        }
        
        // Test that crypto module correctly encrypts and decrypts random 20 byte array 
        // using asymmetric algorithm
        [Test]
        public void TestSignAndVerifyFail()
        {
            // Generate symmetric key 
            (priKey, pubKey) = Crypto.GenerateRSAParams();

            // sign plaintext using private key
            var signature = Crypto.SignData(priKey, plainText);
            
            // Remove last byte from signature
            var modifiedSignature = signature.Take(signature.Length - 1).ToArray();
            
            // verify signature using public key and plaintext
            var result = Crypto.VerifyData(pubKey, plainText, modifiedSignature);
            Assert.False(result);
        }
    }
}