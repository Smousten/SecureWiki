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

            var result = _dataFile.IsValidRevisionID("0", 0);
            Assert.True(result);
        }

        [Test]
        public void TestGetDataFileKeyByRevisionID()
        {
            
        }

        [Test]
        public void TestMergeWithOtherDataFileEntry()
        {
        }

        
    }
}