using System.Linq;
using System.Threading;
using NUnit.Framework;
using SecureWiki;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class KeyringManagerTest
    {
        private KeyringManager _keyringManager;
        private RootKeyring _rootKeyring;
        private Manager _manager;
        private Logger _logger = new();

        [SetUp]
        public void SetUp()
        {
            _manager = new Manager(Thread.CurrentThread, _rootKeyring, _logger);
            _rootKeyring = new RootKeyring();
            _keyringManager = new KeyringManager(_rootKeyring, _manager);
        }

        [TearDown]
        public void Teardown()
        {
            _manager = null;
            _rootKeyring = null;
            _keyringManager = null;
        }

        // Test that a new keyring is added to the rootkeyring
        [Test]
        public void AddNewKeyringTest()
        {
            // Add keyring with name 'folder' and path 'folder', assert that keyring is inserted
            _keyringManager.AddNewKeyRing("folder", "folder");
            Assert.NotNull(_rootKeyring.keyrings.FirstOrDefault(e => e.name.Equals("folder")));

            // Add keyring with name 'folderA' and path 'folder/folderA', assert that keyring is added to child
            _keyringManager.AddNewKeyRing("folderA", "folder/folderA");
            var keyring = _rootKeyring.keyrings.FirstOrDefault();
            Assert.NotNull(keyring!.keyrings.FirstOrDefault(e => e.name.Equals("folderA")));

            // Add keyring with name 'nested' and path 'intermediate/nested', 
            // assert that an intermediate keyring is added to the rootkeyring
            _keyringManager.AddNewKeyRing("nested", "intermediate/nested");
            var intermediate = _rootKeyring.keyrings.FirstOrDefault(e => e.name.Equals("intermediate"));
            Assert.NotNull(intermediate);
            Assert.NotNull(intermediate!.keyrings.FirstOrDefault(e => e.name.Equals("nested")));
        }

        // Test that a new datafile is added to the rootkeyring
        [Test]
        public void AddNewFileTest()
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            var filename = "file";
            var filepath = "file";
            var serverLink = "http://127.0.0.1/mediawiki/api.php";

            _keyringManager.AddNewFile(filename, filepath, serverLink, pageTitle);
            
            Assert.NotNull(_rootKeyring.dataFiles.FirstOrDefault(e => e.filename.Equals("file")));
        }
    }
}