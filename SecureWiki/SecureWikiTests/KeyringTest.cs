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
        private RootKeyring _rootKeyring;
        private const string ServerLink = "http://127.0.0.1/mediawiki/api.php";

        [SetUp]
        public void SetUp()
        {
            _rootKeyring = new RootKeyring();
        }

        [TearDown]
        public void Teardown()
        {
            _rootKeyring = null;
        }

        [Test]
        public void TestAddRemoveKeyring()
        {
            var newKeyring = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            
            _rootKeyring.AddKeyring(newKeyring);
            Assert.True(_rootKeyring.keyrings.Count.Equals(1));
            
            _rootKeyring.RemoveKeyring(newKeyring);
            Assert.True(_rootKeyring.keyrings.Count.Equals(0));

            var list = new List<Keyring> {newKeyring, newKeyring2};
            _rootKeyring.AddRangeKeyring(list);
            Assert.True(_rootKeyring.keyrings.Count.Equals(2));
        }

        [Test]
        public void TestAddRemoveAccessFile()
        {
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file1");
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file2");
            
            _rootKeyring.AddAccessFile(newAccessFile);
            Assert.True(_rootKeyring.accessFiles.Count.Equals(1));
            
            _rootKeyring.RemoveAccessFile(newAccessFile);
            Assert.True(_rootKeyring.accessFiles.Count.Equals(0));

            var list = new List<AccessFile> {newAccessFile, newAccessFile2};
            _rootKeyring.AddRangeAccessFile(list);
            Assert.True(_rootKeyring.accessFiles.Count.Equals(2));
        }

        [Test]
        public void TestClear()
        {
            var newKeyring = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            var keyList = new List<Keyring> {newKeyring, newKeyring2};
            
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file1");
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file2");
            var fileList = new List<AccessFile> {newAccessFile, newAccessFile2};
            
            _rootKeyring.keyrings.AddRange(keyList);
            _rootKeyring.accessFiles.AddRange(fileList);
            
            Assert.True(_rootKeyring.keyrings.Count.Equals(2));
            Assert.True(_rootKeyring.accessFiles.Count.Equals(2));
            
            _rootKeyring.ClearKeyrings();
            _rootKeyring.ClearAccessFiles();
            
            Assert.True(_rootKeyring.keyrings.Count.Equals(0));
            Assert.True(_rootKeyring.accessFiles.Count.Equals(0));
        }

        [Test]
        public void TestMergeAllEntriesFromOtherKeyring()
        {
            var newKeyring1 = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            
            var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file1");
            var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file2");
            var fileList = new List<AccessFile> {newAccessFile, newAccessFile2};
            
            newKeyring2.AddRangeAccessFile(fileList);
            var keyList = new List<Keyring> {newKeyring1, newKeyring2};
            
            _rootKeyring.AddRangeKeyring(keyList);

            var newKeyring3 = new Keyring("folder2");
            var newAccessFile3 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file3");
            
            var newKeyring = new Keyring();
            newKeyring3.AddAccessFile(newAccessFile3);
            newKeyring.AddKeyring(newKeyring3);
            
            _rootKeyring.MergeAllEntriesFromOtherKeyring(newKeyring);
            var folder2 = _rootKeyring.keyrings.FirstOrDefault(e => e.name.Equals("folder2"));
            Assert.True(folder2.accessFiles.Count.Equals(3));
        }
    }
}