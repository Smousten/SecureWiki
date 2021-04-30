using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
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
        public void TestAddRemoveDataFile()
        {
            var newDataFile = new DataFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file1");
            var newDataFile2 = new DataFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file2");
            
            _rootKeyring.AddDataFile(newDataFile);
            Assert.True(_rootKeyring.dataFiles.Count.Equals(1));
            
            _rootKeyring.RemoveDataFile(newDataFile);
            Assert.True(_rootKeyring.dataFiles.Count.Equals(0));

            var list = new List<DataFile> {newDataFile, newDataFile2};
            _rootKeyring.AddRangeDataFile(list);
            Assert.True(_rootKeyring.dataFiles.Count.Equals(2));
        }

        [Test]
        public void TestClear()
        {
            var newKeyring = new Keyring("folder1");
            var newKeyring2 = new Keyring("folder2");
            var keyList = new List<Keyring> {newKeyring, newKeyring2};
            
            var newDataFile = new DataFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file1");
            var newDataFile2 = new DataFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), "file2");
            var fileList = new List<DataFile> {newDataFile, newDataFile2};
            
            _rootKeyring.keyrings.AddRange(keyList);
            _rootKeyring.dataFiles.AddRange(fileList);
            
            Assert.True(_rootKeyring.keyrings.Count.Equals(2));
            Assert.True(_rootKeyring.dataFiles.Count.Equals(2));
            
            _rootKeyring.ClearKeyrings();
            _rootKeyring.ClearDataFiles();
            
            Assert.True(_rootKeyring.keyrings.Count.Equals(0));
            Assert.True(_rootKeyring.dataFiles.Count.Equals(0));
        }

        [Test]
        public void TestCopyFromOtherKeyring()
        {
        }
    }
}