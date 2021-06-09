using System.Collections.Generic;
using System.Linq;
using DynamicData;
using NUnit.Framework;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class KeyringTests
    {
        private MasterKeyring _masterKeyring;
        private KeyringManager _keyringManager;
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
            var newKeyring = _keyringManager.CreateNewKeyring("folder1", ServerLink);
            var newKeyring2 = _keyringManager.CreateNewKeyring("folder2", ServerLink);
            
            Assert.True(newKeyring != null);
            Assert.True(newKeyring2 != null);
            
            Assert.True(newKeyring.accessFileReferenceToSelf != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf != null);
            
            Assert.True(newKeyring.accessFileReferenceToSelf.AccessFileParent != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf.AccessFileParent != null);
            
            Assert.True(newKeyring.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf != null);

            var symmRef1 = newKeyring.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf;
            var symmRef2 = newKeyring2.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf;
            
            _masterKeyring.AddSymmetricReference(symmRef1);
            
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(1));
            
            _masterKeyring.AttemptRemoveSymmetricReference(symmRef1);
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(0));

            _masterKeyring.AddSymmetricReference(symmRef1);
            _masterKeyring.AddSymmetricReference(symmRef2);
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(2));
        }

        // [Test]
        // public void TestMergeAllEntriesFromOtherKeyring()
        // {
        //     var newKeyring1 = new Keyring("folder1");
        //     var newKeyring2 = new Keyring("folder2");
        //     
        //     var newAccessFile = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
        //     var newAccessFile2 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
        //     var fileList = new List<AccessFile> {newAccessFile, newAccessFile2};
        //     
        //     newKeyring2.AddRangeAccessFile(fileList);
        //     var keyList = new List<Keyring> {newKeyring1, newKeyring2};
        //     
        //     _masterKeyring.AddRangeKeyring(keyList);
        //
        //     var newKeyring3 = new Keyring("folder2");
        //     var newAccessFile3 = new AccessFile(ServerLink, RandomString.GenerateRandomAlphanumericString(), PageType.GenericFile);
        //     
        //     var newKeyring = new Keyring();
        //     newKeyring3.AddAccessFile(newAccessFile3);
        //     newKeyring.AddKeyring(newKeyring3);
        //     
        //     _masterKeyring.MergeAllEntriesFromOtherKeyring(newKeyring);
        //     var folder2 = _masterKeyring.keyrings.FirstOrDefault(e => e.name.Equals("folder2"));
        //     Assert.True(folder2.accessFiles.Count.Equals(3));
        // }
    }
}