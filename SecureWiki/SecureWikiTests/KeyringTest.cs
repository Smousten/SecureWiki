using System.Collections.Generic;
using System.Linq;
using DynamicData;
using NUnit.Framework;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class KeyringTests
    {
        private MasterKeyring _masterKeyring;
        private const string ServerLink = "http://127.0.0.1/mediawiki/api.php";

        [SetUp]
        public void SetUp()
        {
            _masterKeyring = new MasterKeyring();
        }

        [TearDown]
        public void Teardown()
        {
            _masterKeyring = null;
        }

        [Test]
        public void TestAddRemoveKeyring()
        {
            var newKeyring = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            
            _masterKeyring.AddKeyring(newKeyring);
            Assert.True(_masterKeyring.keyrings.Count.Equals(1));
            
            _masterKeyring.RemoveKeyring(newKeyring);
            Assert.True(_masterKeyring.keyrings.Count.Equals(0));

            var list = new List<Keyring> {newKeyring, newKeyring2};
            _masterKeyring.AddRangeKeyring(list);
            Assert.True(_masterKeyring.keyrings.Count.Equals(2));
        }

        [Test]
        public void TestAddRemoveAccessFile()
        {
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            
            _masterKeyring.AddAccessFile(newAccessFile);
            Assert.True(_masterKeyring.accessFiles.Count.Equals(1));
            
            _masterKeyring.RemoveAccessFile(newAccessFile);
            Assert.True(_masterKeyring.accessFiles.Count.Equals(0));

            var list = new List<AccessFile> {newAccessFile, newAccessFile2};
            _masterKeyring.AddRangeAccessFile(list);
            Assert.True(_masterKeyring.accessFiles.Count.Equals(2));
        }

        [Test]
        public void TestClear()
        {
            var newKeyring = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            var keyList = new List<Keyring> {newKeyring, newKeyring2};
            
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            var fileList = new List<AccessFile> {newAccessFile, newAccessFile2};
            
            _masterKeyring.keyrings.AddRange(keyList);
            _masterKeyring.accessFiles.AddRange(fileList);
            
            Assert.True(_masterKeyring.keyrings.Count.Equals(2));
            Assert.True(_masterKeyring.accessFiles.Count.Equals(2));
            
            _masterKeyring.ClearKeyrings();
            _masterKeyring.ClearAccessFiles();
            
            Assert.True(_masterKeyring.keyrings.Count.Equals(0));
            Assert.True(_masterKeyring.accessFiles.Count.Equals(0));
        }

        [Test]
        public void TestMergeAllEntriesFromOtherKeyring()
        {
            var newKeyring1 = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            var fileList = new List<AccessFile> {newAccessFile, newAccessFile2};
            
            newKeyring2.AddRangeAccessFile(fileList);
            var keyList = new List<Keyring> {newKeyring1, newKeyring2};
            
            _masterKeyring.AddRangeKeyring(keyList);

            var newKeyring3 = new Keyring("folder2");
            var newAccessFile3 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
            
            var newKeyring = new Keyring();
            newKeyring3.AddAccessFile(newAccessFile3);
            newKeyring.AddKeyring(newKeyring3);
            
            _masterKeyring.MergeAllEntriesFromOtherKeyring(newKeyring);
            var folder2 = _masterKeyring.keyrings.FirstOrDefault(e => e.name.Equals("folder2"));
            Assert.True(folder2.accessFiles.Count.Equals(3));
        }
    }
}