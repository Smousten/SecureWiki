using System.Linq;
using NUnit.Framework;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class AccessFileTests
    {
        private AccessFile _accessFile;

        [SetUp]
        public void Setup()
        {
            var serverLink = "http://127.0.0.1/mediawiki/api.php";
            var pageName = RandomString.GenerateRandomAlphanumericString();
            _accessFile = new AccessFile(serverLink, pageName, PageType.GenericFile);
        }
        
        [TearDown]
        public void TearDown()
        {
            _accessFile = null;
        }

        // Test that key list with single entry can be verified with owner keys
        [Test]
        public void TestVerifyKeys()
        {
            Assert.True(_accessFile.VerifyKeys());
        }
        
        // Test that key list with single entry where public key is invalid can not be verified
        [Test]
        public void TestVerifyKeysFail()
        {
            var (_, newPubKey) = Crypto.GenerateRSAParams();
            var key = _accessFile.keyList.FirstOrDefault();
            if (key != null) key.PublicKey = newPubKey;
            Assert.False(_accessFile.VerifyKeys());
        }
        
        [Test]
        public void TestHasSameStaticProperties()
        {
            var serverLink = _accessFile.AccessFileReference.serverLink;
            var pageName = _accessFile.AccessFileReference.targetPageName;
            var comparisonFile = new AccessFile(serverLink, pageName, PageType.GenericFile) {ownerPublicKey = _accessFile.ownerPublicKey, };
            var result = _accessFile.HasSameStaticProperties(comparisonFile);
            Assert.True(result);
        }

        [Test]
        public void TestHasSameStaticPropertiesFail()
        {
            var serverLink = _accessFile.AccessFileReference.serverLink;
            var pageName = RandomString.GenerateRandomAlphanumericString();
            var comparisonFile = new AccessFile(serverLink, pageName, PageType.GenericFile) {ownerPublicKey = _accessFile.ownerPublicKey};
            var result = _accessFile.HasSameStaticProperties(comparisonFile);
            Assert.False(result);
        }

        [Test]
        public void TestIsValidRevisionID()
        {
            var oldKey = _accessFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new AccessFileKey();
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _accessFile.keyList.Add(newKey);

            var result = false;
            for (int i = 0; i < _accessFile.keyList.Count; i++)
            {
                if (_accessFile.IsValidRevisionID("2", i))
                {
                    result = true;
                }
            }
            Assert.True(result);
        }

        [Test]
        public void TestIsValidRevisionIDFail()
        {
            var oldKey = _accessFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new AccessFileKey();
            newKey.RevisionStart = "3";
            newKey.RevisionEnd = "4";
            _accessFile.keyList.Add(newKey);

            var result = false;
            for (int i = 0; i < _accessFile.keyList.Count; i++)
            {
                if (_accessFile.IsValidRevisionID("2", i))
                {
                    result = true;
                }
            }
            Assert.False(result);
        }
        
        [Test]
        public void TestGetAccessFileKeyByRevisionID()
        {
            var oldKey = _accessFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new AccessFileKey();
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _accessFile.keyList.Add(newKey);

            var getKeyOne = _accessFile.GetAccessFileKeyByRevisionID("2");
            Assert.True(getKeyOne != null && getKeyOne.Equals(newKey));
            Assert.False(getKeyOne != null && getKeyOne.Equals(oldKey));
        }

        [Test]
        public void TestMergeWithOtherAccessFileEntry()
        {
            var comparisonFile = new AccessFile(_accessFile.AccessFileReference.serverLink, _accessFile.AccessFileReference.targetPageName, PageType.GenericFile)
            {
                ownerPublicKey = _accessFile.ownerPublicKey, 
                ownerPrivateKey = _accessFile.ownerPrivateKey
            };
            var oldKey = _accessFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var key = comparisonFile.keyList.FirstOrDefault();
            if (oldKey != null && key != null)
            {
                key.PrivateKey = oldKey.PrivateKey;
                key.PublicKey = oldKey.PublicKey;
                key.RevisionEnd = oldKey.RevisionEnd;
                key.RevisionStart = oldKey.RevisionStart;
                key.SymmKey = oldKey.SymmKey;
                key.SignedWriteKey = oldKey.SignedWriteKey;
                key.SignedReadKeys = oldKey.SignedReadKeys;
            }
            
            var newKey = new AccessFileKey(_accessFile.ownerPrivateKey!);
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _accessFile.keyList.Add(newKey);
            comparisonFile.keyList.Add(newKey);

            var newKeyComparison = new AccessFileKey(_accessFile.ownerPrivateKey!);
            newKeyComparison.RevisionStart = "4";
            newKeyComparison.RevisionEnd = "8";
            comparisonFile.keyList.Add(newKeyComparison);
            
            _accessFile.MergeWithOtherAccessFileEntry(comparisonFile);
            
            Assert.True(_accessFile.keyList.Count.Equals(3));
        }

        [Test]
        public void TestCopy()
        {
            var copyAccessFile = _accessFile.Copy();
            Assert.AreEqual(copyAccessFile.inboxReferences, _accessFile.inboxReferences);
            Assert.AreEqual(copyAccessFile.keyList.Last().SymmKey, _accessFile.keyList.Last().SymmKey);
            Assert.AreEqual(copyAccessFile.ownerPrivateKey, copyAccessFile.ownerPrivateKey);
            Assert.AreEqual(copyAccessFile.ownerPublicKey, copyAccessFile.ownerPublicKey);

            var (_, newPubKey) = Crypto.GenerateRSAParams();

            copyAccessFile.ownerPublicKey = newPubKey;
            
            Assert.AreNotEqual(copyAccessFile.ownerPublicKey, _accessFile.ownerPublicKey);

        }
        
    }
}