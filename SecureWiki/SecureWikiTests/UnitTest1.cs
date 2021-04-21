using System;
using NUnit.Framework;
using SecureWiki.Cryptography;

namespace SecureWikiTests
{
    public class Tests
    {
        private Byte[] b = new Byte[20];
        private Byte[] symmKey;
        private Byte[] iv;
        private Byte[] priKey;
        private Byte[] pubKey;
        
        
        [SetUp]
        public void Setup()
        {
            Random rnd = new Random();
            rnd.NextBytes(b);
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
        [Test]
        public void TestEncryptDecrypt()
        {
            // Generate symmetric key 
            (symmKey, iv) = Crypto.GenerateAESParams();

            // Encrypt random 20 byte array
            var encryptedBytes = Crypto.Encrypt(b, symmKey, iv);
            
            // Decrypt random 20 byte array
            var decryptedBytes = Crypto.Decrypt(encryptedBytes!, symmKey, iv);

            Assert.AreEqual(b, decryptedBytes);
        }
    }
}