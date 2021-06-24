using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DynamicData;
using NUnit.Framework;
using SecureWiki;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class KeyringTests
    {
        private Manager _manager;
        private MasterKeyring _masterKeyring;
        private KeyringManager _keyringManager;
        private Logger _logger = new();
        private MountedDirMirror _mountedDirMirror;
        private const string ServerLink = "http://127.0.0.1/mediawiki/api.php";

        [SetUp]
        public void SetUp()
        {
            _mountedDirMirror = new MountedDirMirror();
            _manager = new Manager(Thread.CurrentThread, _masterKeyring, _logger, _mountedDirMirror);
            _masterKeyring = new MasterKeyring();
            _keyringManager = new KeyringManager(_masterKeyring, _manager);
        }

        [TearDown]
        public void Teardown()
        {
            _masterKeyring = null;
        }

        [Test]
        public void TestAddRemoveKeyring()
        {
            var pageNameKeyring1 = RandomString.GenerateRandomAlphanumericString();
            var pageNameInboxPage1 = RandomString.GenerateRandomAlphanumericString();
            var pageNameAccessFile1 = RandomString.GenerateRandomAlphanumericString();
            var pageNameKeyring2 = RandomString.GenerateRandomAlphanumericString();
            var pageNameInboxPage2 = RandomString.GenerateRandomAlphanumericString();
            var pageNameAccessFile2 = RandomString.GenerateRandomAlphanumericString();

            var accessFile1 = new AccessFile(ServerLink, pageNameKeyring1, PageType.Keyring);
            var accessFile2 = new AccessFile(ServerLink, pageNameKeyring2, PageType.Keyring);
            
            var symmetricReference1 = new SymmetricReference(pageNameAccessFile1,
                ServerLink, PageType.Keyring, pageNameKeyring1, accessFile1);
            accessFile1.SymmetricReferenceToSelf = symmetricReference1;
            
            var symmetricReference2 = new SymmetricReference(pageNameAccessFile2,
                ServerLink, PageType.Keyring, pageNameKeyring2, accessFile2);
            accessFile2.SymmetricReferenceToSelf = symmetricReference2;

            var newKeyring1 = new Keyring(accessFile1.AccessFileReference, "keyring1");
            var newKeyring2 = new Keyring(accessFile2.AccessFileReference, "keyring2");
            
            InboxReference inboxReference1 = new InboxReference(pageNameInboxPage1, ServerLink, 
                InboxReference.AccessLevel.ReadWrite);
            newKeyring1.OwnContact = new OwnContact("newKeyring1", inboxReference1);

            InboxReference inboxReference2 = new InboxReference(pageNameInboxPage2, ServerLink, 
                InboxReference.AccessLevel.ReadWrite);
            newKeyring2.OwnContact = new OwnContact("newKeyring2", inboxReference2);
            
            Assert.True(newKeyring1 != null);
            Assert.True(newKeyring2 != null);
            
            Assert.True(newKeyring1.accessFileReferenceToSelf != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf != null);
            
            Assert.True(newKeyring1.accessFileReferenceToSelf.AccessFileParent != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf.AccessFileParent != null);
            
            Assert.True(newKeyring1.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf != null);
            Assert.True(newKeyring2.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf != null);

            var symmRef1 = newKeyring1.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf;
            var symmRef2 = newKeyring2.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf;
            
            _masterKeyring.AddSymmetricReference(symmRef1);
            
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(1));
            
            _masterKeyring.AttemptRemoveSymmetricReference(symmRef1);
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(0));

            _masterKeyring.AddSymmetricReference(symmRef1);
            _masterKeyring.AddSymmetricReference(symmRef2);
            Assert.True(_masterKeyring.SymmetricReferences.Count.Equals(2));
        }
    }
}