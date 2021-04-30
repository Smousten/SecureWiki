using System.Linq;
using NUnit.Framework;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class DataFileTests
    {
        private DataFile _dataFile;

        [SetUp]
        public void Setup()
        {
            var serverLink = "http://127.0.0.1/mediawiki/api.php";
            var pageName = RandomString.GenerateRandomAlphanumericString();
            _dataFile = new DataFile(serverLink, pageName);
        }
        
        [TearDown]
        public void TearDown()
        {
            _dataFile = null;
        }

        // Test that key list with single entry can be verified with owner keys
        [Test]
        public void TestVerifyKeys()
        {
            Assert.True(_dataFile.VerifyKeys());
        }
        
        // Test that key list with single entry where public key is invalid can not be verified
        [Test]
        public void TestVerifyKeysFail()
        {
            var (_, newPubKey) = Crypto.GenerateRSAParams();
            var key = _dataFile.keyList.FirstOrDefault();
            if (key != null) key.PublicKey = newPubKey;
            Assert.False(_dataFile.VerifyKeys());
        }
        
        [Test]
        public void TestHasSameStaticProperties()
        {
            var serverLink = _dataFile.serverLink;
            var pageName = _dataFile.pageName;
            var comparisonFile = new DataFile(serverLink, pageName) {ownerPublicKey = _dataFile.ownerPublicKey, };
            var result = _dataFile.HasSameStaticProperties(comparisonFile);
            Assert.True(result);
        }

        [Test]
        public void TestHasSameStaticPropertiesFail()
        {
            var serverLink = _dataFile.serverLink;
            var pageName = RandomString.GenerateRandomAlphanumericString();
            var comparisonFile = new DataFile(serverLink, pageName) {ownerPublicKey = _dataFile.ownerPublicKey};
            var result = _dataFile.HasSameStaticProperties(comparisonFile);
            Assert.False(result);
        }

        [Test]
        public void TestIsValidRevisionID()
        {
            var oldKey = _dataFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new DataFileKey();
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _dataFile.keyList.Add(newKey);

            var result = false;
            for (int i = 0; i < _dataFile.keyList.Count; i++)
            {
                if (_dataFile.IsValidRevisionID("2", i))
                {
                    result = true;
                }
            }
            Assert.True(result);
        }

        [Test]
        public void TestIsValidRevisionIDFail()
        {
            var oldKey = _dataFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new DataFileKey();
            newKey.RevisionStart = "3";
            newKey.RevisionEnd = "4";
            _dataFile.keyList.Add(newKey);

            var result = false;
            for (int i = 0; i < _dataFile.keyList.Count; i++)
            {
                if (_dataFile.IsValidRevisionID("2", i))
                {
                    result = true;
                }
            }
            Assert.False(result);
        }
        
        [Test]
        public void TestGetDataFileKeyByRevisionID()
        {
            var oldKey = _dataFile.keyList.FirstOrDefault();
            if (oldKey != null)
            {
                oldKey.RevisionStart = "0";
                oldKey.RevisionEnd = "1";
            }

            var newKey = new DataFileKey();
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _dataFile.keyList.Add(newKey);

            var getKeyOne = _dataFile.GetDataFileKeyByRevisionID("2");
            Assert.True(getKeyOne != null && getKeyOne.Equals(newKey));
            Assert.False(getKeyOne != null && getKeyOne.Equals(oldKey));
        }

        [Test]
        public void TestMergeWithOtherDataFileEntry()
        {
            var comparisonFile = new DataFile(_dataFile.serverLink, _dataFile.pageName)
            {
                ownerPublicKey = _dataFile.ownerPublicKey, 
                ownerPrivateKey = _dataFile.ownerPrivateKey
            };
            var oldKey = _dataFile.keyList.FirstOrDefault();
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
            
            var newKey = new DataFileKey(_dataFile.ownerPrivateKey!);
            newKey.RevisionStart = "2";
            newKey.RevisionEnd = "3";
            _dataFile.keyList.Add(newKey);
            comparisonFile.keyList.Add(newKey);

            var newKeyComparison = new DataFileKey(_dataFile.ownerPrivateKey!);
            newKeyComparison.RevisionStart = "4";
            newKeyComparison.RevisionEnd = "8";
            comparisonFile.keyList.Add(newKeyComparison);
            
            _dataFile.MergeWithOtherDataFileEntry(comparisonFile);
            
            Assert.True(_dataFile.keyList.Count.Equals(3));
        }

        [Test]
        public void TestAddContactInfo()
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            _dataFile.AddContactInfo(pageTitle, _dataFile.serverLink);
            Assert.True(_dataFile.contactList.Count.Equals(1));
            
            _dataFile.AddContactInfo(pageTitle, _dataFile.serverLink);
            Assert.True(_dataFile.contactList.Count.Equals(1));
            
            _dataFile.AddContactInfo(RandomString.GenerateRandomAlphanumericString(), _dataFile.serverLink);
            Assert.True(_dataFile.contactList.Count.Equals(2));

            _dataFile.AddContactInfo(pageTitle, "http://192.168.1.7/mediawiki/api.php");
            Assert.True(_dataFile.contactList.Count.Equals(3));
        }
    }
}