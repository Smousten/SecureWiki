using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using SecureWiki;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.ViewModels;
using SecureWiki.Views;

namespace SecureWikiTests
{
    public class IntegrationTest
    {
        protected Manager _manager;
        
        [OneTimeSetUp]
        public void Setup()
        {
            // var serverLink = "http://127.0.0.1/mediawiki/api.php";
            // var pageName = RandomString.GenerateRandomAlphanumericString();
            // pageName = char.ToUpper(pageName[0]) + pageName.Substring(1);

            Console.WriteLine("checking fuse");
            MainWindow.IsFuseRunning();

            Console.WriteLine("Creating manager");
            _manager = new Manager(Thread.CurrentThread, new MasterKeyring(), new Logger(), new MountedDirMirror());
            Thread managerThread = new(_manager.Run) {IsBackground = true, Name = "ManagerThread"};
            managerThread.Start();
            // _manager.Run();
            
            var sleepCnt = 0;
            while (_manager.setupFinished == false)
            {
                Console.WriteLine("sleepCnt = " + sleepCnt);
                Thread.Sleep(1000);
                sleepCnt++;
                if (sleepCnt > 60)
                {
                    break;
                }
            }
            
            Console.WriteLine("Setup() passed");
            Console.WriteLine();
        }
        
        [OneTimeTearDown]
        public void Teardown()
        {
            _manager = null;
            MainWindow.Unmount();
            DirectoryInfo directoryInfo = new(GetRootDirPath());

            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
            
            foreach (var dir in directoryInfo.GetDirectories())
            {
                dir.Delete(true);
            }
            
        }

        public class FuseTest : IntegrationTest
        {
            [Test, Order(2)]
            public void CreateFile()
            {
                Console.WriteLine("CreateFile entered");
                var mountdirPath = GetMountDirPath();
            
                Thread.Sleep(10000);
            
                var cntPre = _manager.mountedDirMirror.RootFolder.combinedList.Count;
            
                // File.Create(mountdirPath + "/CreateFileTest.txt").Dispose();
                var cmd = $"touch {mountdirPath}/CreateFileTest.txt";
                ExecuteShellCommand(cmd);
                Console.WriteLine($"ls {mountdirPath} :");
                ExecuteShellCommand($"ls {mountdirPath}");
                Console.WriteLine();

                WaitForUploads();

                var cntPost = _manager.mountedDirMirror.RootFolder.combinedList.Count;
            
                Assert.Greater(cntPost, cntPre);
            }

            [Test, Order(3)]
            public void ReadEmptyFile()
            {
                Console.WriteLine("ReadEmptyFile() entered");
                var mountdirPath = GetMountDirPath();
                var filePath = Path.Combine(mountdirPath, "CreateFileTest.txt");
                var contentString = "This is the first revision";

                if (!File.Exists(filePath))
                {
                    CreateFile();
                }

                var readContent = File.ReadAllText(filePath);
                // var readContent = "not the same"; 

                Console.WriteLine("-" + contentString + "-");
                Console.WriteLine("-" + readContent + "-");

                contentString = contentString.Trim();
                readContent = readContent.Trim();
            
                Assert.True(readContent.Equals(contentString));
            }
        }

        public class MediaWikiTest : IntegrationTest
        {
            
            [Test, Order(2)]
            public void GetWikiHandler()
            {
                var serverLink = _manager.configManager.DefaultServerLink;
                
                var wh = _manager.GetWikiHandler(serverLink);
                
                Assert.True(wh != null);
            }
            
            [Test, Order(2)]
            public void GetFreshPageName()
            {
                var contentString = "This is a string that should match";
                
                var pageName = _manager.GetFreshPageName();
                WaitForUploads();
                
                Assert.True(pageName?.Length > 1);
            }
            
            
            [Test, Order(2)]
            public void UploadAndDownloadFile()
            {
                var serverLink = _manager.configManager.DefaultServerLink;
                var contentString = "This is a string that should match";
                var pageName = _manager.GetFreshPageName();
                WaitForUploads();
                
                Assert.True(pageName?.Length > 1);
                
                var wh = _manager.GetWikiHandler(serverLink);
                
                Assert.True(wh != null);

                wh.Upload(pageName, contentString);
                
                WaitForUploads();

                var downloadedContent = wh.GetPageContent(pageName);
                
                contentString = contentString.Trim();
                downloadedContent = downloadedContent.Trim();
                
                Assert.True(downloadedContent.Equals(contentString));
            }

            [Test, Order(2)]
            public void UploadAndDownloadTwoDifferentRevisions()
            {
                var serverLink = _manager.configManager.DefaultServerLink;
                var pageName = _manager.GetFreshPageName();
                WaitForUploads();
                
                var contentString1 = "This is the first string that should match";
                var contentString2 = "This is the second string that should match";
                
                
                Assert.True(pageName?.Length > 1);
                
                var wh = _manager.GetWikiHandler(serverLink);
                
                Assert.True(wh != null);

                // Upload 1
                wh.Upload(pageName, contentString1);
                WaitForUploads();
                
                // Upload 2
                wh.Upload(pageName, contentString2);
                WaitForUploads();
                
                // Get revisions ids
                var allRevisions = _manager.GetAllRevisions(pageName, serverLink);
                if (allRevisions == null || allRevisions.revisionList.Count < 2)
                {
                    Assert.Fail();
                }
                
                // Console.WriteLine("Revision IDs:");
                // foreach (var rev in allRevisions.revisionList)
                // {
                //     Console.WriteLine(rev.revisionID);
                // }
                // Console.WriteLine();
                
                var revidLatest = allRevisions.revisionList[0].revisionID;
                var revidPrev = allRevisions.revisionList[1].revisionID;
                
                if (revidLatest == null || revidPrev == null )
                {
                    Assert.Fail();
                }
                
                // Download 
                var downloadedContentLatest = wh.GetPageContent(pageName, revidLatest);
                var downloadedContentPrev = wh.GetPageContent(pageName, revidPrev);

                // Trim all strings
                contentString1 = contentString1.Trim();
                contentString2 = contentString2.Trim();
                downloadedContentLatest = downloadedContentLatest.Trim();
                downloadedContentPrev = downloadedContentPrev.Trim();
                
                // Console.WriteLine("contentString1");
                // Console.WriteLine("-" + contentString1 + "-");
                // Console.WriteLine("contentString2");
                // Console.WriteLine("-" + contentString2 + "-");
                // Console.WriteLine("readContentLatest");
                // Console.WriteLine("-" + downloadedContentLatest + "-");
                // Console.WriteLine("readContentPrevious");
                // Console.WriteLine("-" + downloadedContentPrev + "-");
                
                // Compare
                Assert.True(contentString1.Equals(downloadedContentPrev));
                Assert.True(contentString2.Equals(downloadedContentLatest));
                
            }
        }
        
        public class CompleteSystemTest : IntegrationTest
        {
            [Test, Order(4)]
            public void EditAndUploadFile()
            {
                Console.WriteLine("EditAndUploadFile() entered");
                var mountdirPath = GetMountDirPath();
                var filename = "CreateFileTest.txt";
                var filePath = Path.Combine(mountdirPath, filename);
                var contentString = "This is a string that should match";

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("file does not exists at '{0}'", filePath);
                    var cmdTouch = $"touch {filePath}";
                    ExecuteShellCommand(cmdTouch);
                    // Console.WriteLine($"ls {mountdirPath} :");
                    // ExecuteShellCommand($"ls {mountdirPath}");
                    // Console.WriteLine();
                    WaitForUploads();
                }
                

                var cmdWrite = $"echo '{contentString}' > {filePath}";
                ExecuteShellCommand(cmdWrite);
                Console.WriteLine("cmdWrite: " + cmdWrite);
                WaitForUploads();

                var readContent = Encoding.ASCII.GetString(_manager.GetContent(filename) 
                                                           ?? Encoding.ASCII.GetBytes("not the same"));
                // var readContent = "not the same"; 

                Console.WriteLine("-" + contentString + "-");
                Console.WriteLine("-" + readContent + "-");

                contentString = contentString.Trim();
                readContent = readContent.Trim();
                
                Assert.True(readContent.Equals(contentString));
            }
            
            [Test, Order(5)]
            public void EditAndDownloadTwoDifferentRevisions()
            {
                Console.WriteLine("ReadTwoDifferentRevisions() entered");
                var mountdirPath = GetMountDirPath();
                var filename = "CreateFileTest.txt";
                var filePath = Path.Combine(mountdirPath, filename);
                
                var contentString1 = "This is the first string that should match";
                var contentString2 = "This is the second string that should match";

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("file does not exists at '{0}'", filePath);
                    var cmdTouch = $"touch {filePath}";
                    ExecuteShellCommand(cmdTouch);
                    // Console.WriteLine($"ls {mountdirPath} :");
                    // ExecuteShellCommand($"ls {mountdirPath}");
                    // Console.WriteLine();
                    WaitForUploads();
                }

                // Write first time
                _manager.tcpListener.ResetQueue();
                var cmdWrite1 = $"echo '{contentString1}' > {filePath}";
                Console.WriteLine(cmdWrite1);
                ExecuteShellCommand(cmdWrite1);
                WaitForUploads();
                
                // Write second time
                _manager.tcpListener.ResetQueue();
                var cmdWrite2 = $"echo '{contentString2}' > {filePath}";
                Console.WriteLine(cmdWrite2);
                ExecuteShellCommand(cmdWrite2);
                WaitForUploads();

                var readContentLatest = Encoding.ASCII.GetString(_manager.GetContent(filename) 
                                                           ?? Encoding.ASCII.GetBytes("not the same"));
                WaitForUploads();
                
                var mdFile = _manager.mountedDirMirror.GetMDFile(filename);
                var pageName = mdFile?.symmetricReference.accessFileTargetPageName;
                var serverLink = mdFile?.symmetricReference.targetAccessFile?.AccessFileReference.serverLink;

                if (pageName == null || serverLink == null)
                {
                    Assert.Fail();
                }
                
                var allRevisions = _manager.GetAllRevisions(pageName, serverLink);
                
                if (allRevisions == null || allRevisions.revisionList.Count < 2)
                {
                    Assert.Fail();
                }

                Console.WriteLine("Revision IDs:");
                foreach (var rev in allRevisions.revisionList)
                {
                    Console.WriteLine(rev.revisionID);
                }
                Console.WriteLine();
                
                var revidPrev = allRevisions.revisionList[1].revisionID;
                
                _manager.UpdateRequestedRevision(pageName, serverLink, revidPrev);
                
                _manager.tcpListener.ResetQueue();
                var readContentPrevious = Encoding.ASCII.GetString(_manager.GetContent(filename) 
                                                                 ?? Encoding.ASCII.GetBytes("not the same"));

                Console.WriteLine("contentString1");
                Console.WriteLine("-" + contentString1 + "-");
                Console.WriteLine("contentString2");
                Console.WriteLine("-" + contentString2 + "-");
                Console.WriteLine("readContentLatest");
                Console.WriteLine("-" + readContentLatest + "-");
                Console.WriteLine("readContentPrevious");
                Console.WriteLine("-" + readContentPrevious + "-");

                contentString1 = contentString1.Trim();
                contentString2 = contentString2.Trim();
                readContentLatest = readContentLatest.Trim();
                readContentPrevious = readContentPrevious.Trim();
                
                Assert.True(contentString1.Equals(readContentPrevious));
                Assert.True(contentString2.Equals(readContentLatest));
            }
            
            // Has to be run after EditAndDownloadTwoDifferentRevisions
            [Test, Order(6)]
            public void ReadTwoDifferentRevisions()
            {
                Console.WriteLine("ReadTwoDifferentRevisions() entered");
                var mountdirPath = GetMountDirPath();
                var filename = "CreateFileTest.txt";
                var filePath = Path.Combine(mountdirPath, filename);
                
                var contentString1 = "This is the first string that should match";
                var contentString2 = "This is the second string that should match";

                _manager.tcpListener.ResetQueue();

                var mdFile = _manager.mountedDirMirror.GetMDFile(filename);
                var pageName = mdFile?.symmetricReference.accessFileTargetPageName;
                var serverLink = mdFile?.symmetricReference.targetAccessFile?.AccessFileReference.serverLink;

                if (pageName == null || serverLink == null)
                {
                    Assert.Fail();
                }
                
                var allRevisions = _manager.GetAllRevisions(pageName, serverLink);
                
                if (allRevisions == null || allRevisions.revisionList.Count < 2)
                {
                    Assert.Fail();
                }
                
                var revidPrev = allRevisions.revisionList[1].revisionID;
                

                // Read latest
                _manager.UpdateRequestedRevision(pageName, serverLink, null);
                var readContentLatest = File.ReadAllText(filePath);
                
                // Read previous
                _manager.UpdateRequestedRevision(pageName, serverLink, revidPrev);
                _manager.tcpListener.ResetQueue();
                var readContentPrevious = File.ReadAllText(filePath);


                Console.WriteLine("contentString1");
                Console.WriteLine("-" + contentString1 + "-");
                Console.WriteLine("contentString2");
                Console.WriteLine("-" + contentString2 + "-");
                Console.WriteLine("readContentLatest");
                Console.WriteLine("-" + readContentLatest + "-");
                Console.WriteLine("readContentPrevious");
                Console.WriteLine("-" + readContentPrevious + "-");

                contentString1 = contentString1.Trim();
                contentString2 = contentString2.Trim();
                readContentLatest = readContentLatest.Trim();
                readContentPrevious = readContentPrevious.Trim();
                
                Assert.True(contentString1.Equals(readContentPrevious));
                Assert.True(contentString2.Equals(readContentLatest));
            }
        }

        public class KeyringSharingTest : IntegrationTest
        {
            [Test, Order(4)]
            public void Share()
            {
                Console.WriteLine("Share() entered");
                var serverLink = _manager.configManager.DefaultServerLink;
                var mountdirPath = GetMountDirPath();
                var filename = "CreateFileTest.txt";
                var filePath = Path.Combine(mountdirPath, filename);
                var contentString = "This is a string that should match";

                // Create keyrings
                var keyring1 = _manager._keyringManager.CreateNewKeyring("Keyring1", serverLink);
                var keyring2 = _manager._keyringManager.CreateNewKeyring("Keyring2", serverLink);

                Assert.True(keyring1 != null);
                Assert.True(keyring2 != null);
                
                Assert.True(keyring2.OwnContact.InboxReference != null);
                
                // Create access files
                _manager._keyringManager.CreateAccessFileAndReferences(serverLink, PageType.Keyring, 
                    out SymmetricReference symmRef1, out AccessFile af1);
                
                _manager._keyringManager.CreateAccessFileAndReferences(_manager.GetFreshPageName(), _manager.GetFreshPageName(),
                    serverLink, PageType.Keyring, out SymmetricReference symmRef2,
                    out AccessFile af2);


                var contact2 = keyring2.OwnContact; //new Contact("contact2", keyring2.OwnContact.InboxReference);
                var contacts = new List<Contact> {contact2};

                // Share af1 with write access, af2 with only read
                var accessFilesAndIsChecked = new List<(AccessFile, bool)> {(af1, true), (af2, false)};


                var accessFilesToUpload = _manager._keyringManager.
                    AddContactsToAccessFilesInBulk(accessFilesAndIsChecked, contacts);
            
                var accessFilesPreparedForExport = _manager._keyringManager.PrepareForExport(accessFilesAndIsChecked);

                var keyring2CntBefore = keyring2.SymmetricReferences.Count;
                
                foreach (var contact in contacts)
                {
                    var wh = _manager.GetWikiHandler(contact.InboxReference.serverLink);
                    if (wh == null)
                    {
                        Console.WriteLine("wh == null, serverLink: " + contact.InboxReference.serverLink);
                        continue;
                    }

                    var afList = accessFilesToUpload[contact].Select(af => accessFilesPreparedForExport[af]).ToList();
                    var afListString = JSONSerialization.SerializeObject(afList);

                    wh.UploadToInboxPage(contact.InboxReference.targetPageName, afListString,
                        contact.InboxReference.publicKey);
                }
                
                WaitForUploads();
                
                _manager.UpdateKeyringWithNewInboxPageEntries(keyring2);
                
                var keyring2CntAfter = keyring2.SymmetricReferences.Count;
                
                Assert.True(keyring2CntAfter > keyring2CntBefore);

                var afInbox1 = keyring2.SymmetricReferences.FirstOrDefault(e =>
                    e.targetAccessFile.AccessFileReference.targetPageName.Equals(af1.AccessFileReference
                        .targetPageName)).targetAccessFile;
                
                var afInbox2 = keyring2.SymmetricReferences.FirstOrDefault(e =>
                    e.targetAccessFile.AccessFileReference.targetPageName.Equals(af2.AccessFileReference
                        .targetPageName)).targetAccessFile;

                // Check that the added access files come from the server
                Assert.True(afInbox1 != af1);
                Assert.True(afInbox2 != af2);
                
                // Check that the original access files have their private key
                Assert.True(af1.keyList.Last().PrivateKey != null);
                Assert.True(af2.keyList.Last().PrivateKey != null);
                
                // Check that only the first of the "new" access files have a private key
                Assert.True(afInbox1.keyList.Last().PrivateKey != null);
                Assert.True(afInbox2.keyList.Last().PrivateKey == null);

            }
            
            [Test, Order(4)]
            public void ShareThenRevoke()
            {
                Console.WriteLine("Share() entered");
                var serverLink = _manager.configManager.DefaultServerLink;
                var mountdirPath = GetMountDirPath();
                var filename = "CreateFileTest.txt";
                var filePath = Path.Combine(mountdirPath, filename);
                var contentString = "This is a string that should match";

                // Create keyrings
                var keyring1 = _manager._keyringManager.CreateNewKeyring("Keyring1", serverLink);
                var keyring2 = _manager._keyringManager.CreateNewKeyring("Keyring2", serverLink);

                Assert.True(keyring1 != null);
                Assert.True(keyring2 != null);
                
                Assert.True(keyring2.OwnContact.InboxReference != null);
                
                // Create access files
                _manager._keyringManager.CreateAccessFileAndReferences(_manager.GetFreshPageName(), _manager.GetFreshPageName(),
                    serverLink, PageType.Keyring, out SymmetricReference symmRef1,
                    out AccessFile af1);
                
                _manager._keyringManager.CreateAccessFileAndReferences(serverLink, PageType.Keyring, 
                    out SymmetricReference symmRef2, out AccessFile af2);
                
                
                var contact1 = keyring1.OwnContact;
                var contact2 = keyring2.OwnContact;
                var contacts = new List<Contact> {contact1, contact2};

                // Share af1 with write access, af2 with only read
                var accessFilesAndIsChecked = new List<(AccessFile, bool)> {(af1, true), (af2, false)};


                var accessFilesToUpload = _manager._keyringManager.
                    AddContactsToAccessFilesInBulk(accessFilesAndIsChecked, contacts);
            
                var accessFilesPreparedForExport = _manager._keyringManager.PrepareForExport(accessFilesAndIsChecked);

                var keyring1CntBefore = keyring1.SymmetricReferences.Count;
                var keyring2CntBefore = keyring2.SymmetricReferences.Count;
                
                foreach (var contact in contacts)
                {
                    var wh = _manager.GetWikiHandler(contact.InboxReference.serverLink);
                    if (wh == null)
                    {
                        Console.WriteLine("wh == null, serverLink: " + contact.InboxReference.serverLink);
                        continue;
                    }

                    var afList = accessFilesToUpload[contact].Select(af => accessFilesPreparedForExport[af]).ToList();
                    var afListString = JSONSerialization.SerializeObject(afList);

                    wh.UploadToInboxPage(contact.InboxReference.targetPageName, afListString,
                        contact.InboxReference.publicKey);
                }
                
                WaitForUploads();
                
                _manager.UpdateKeyringWithNewInboxPageEntries(keyring1);
                _manager.UpdateKeyringWithNewInboxPageEntries(keyring2);
                
                var keyring1CntAfter = keyring1.SymmetricReferences.Count;
                var keyring2CntAfter = keyring2.SymmetricReferences.Count;
                
                Assert.True(keyring1CntAfter > keyring1CntBefore);
                Assert.True(keyring2CntAfter > keyring2CntBefore);

                var af1KeyCountBefore = af1.keyList.Count;
                Console.WriteLine("revoking");
                _manager.RevokeAccess(af1, new List<Contact> {contact1});
                
                WaitForUploads();
                var af1KeyCountAfter = af1.keyList.Count;
                Assert.True(af1KeyCountAfter > af1KeyCountBefore);

                Console.WriteLine("passed revoke");
                
                _manager.UpdateKeyringWithNewInboxPageEntries(keyring1);
                _manager.UpdateKeyringWithNewInboxPageEntries(keyring2);

                var af1Inbox1 = keyring1.SymmetricReferences.FirstOrDefault(e =>
                    e.targetAccessFile.AccessFileReference.targetPageName.Equals(af1.AccessFileReference
                        .targetPageName)).targetAccessFile;
                
                var af1Inbox2 = keyring2.SymmetricReferences.FirstOrDefault(e =>
                    e.targetAccessFile.AccessFileReference.targetPageName.Equals(af1.AccessFileReference
                        .targetPageName)).targetAccessFile;

                // Check that the added access files come from the server
                Assert.True(af1Inbox1 != af1);
                Assert.True(af1Inbox2 != af1);
                Assert.True(af1Inbox2 != af1Inbox1);

                Console.WriteLine("objects are not the same");

                Console.WriteLine("af1.keylist.count: " + af1.keyList.Count);
                Console.WriteLine("af1Inbox1.keylist.count: " + af1Inbox1.keyList.Count);
                Console.WriteLine("af2Inbox1.keylist.count: " + af1Inbox2.keyList.Count);
                
                
                // Check that only keyring1 has the newest key
                Assert.True(af1Inbox1.keyList.Count == af1.keyList.Count);
                Console.WriteLine("keyring1 has the new keys");
                Assert.True(af1Inbox1.keyList.Count > af1Inbox2.keyList.Count);
                Console.WriteLine("keyring2 does not have the new keys");
            }
        }
        
        [Test, Order(1)]
        public void PassSetup()
        {
            var sleepCnt = 0;
            while (_manager.setupFinished == false)
            {
                Console.WriteLine("sleepCnt = " + sleepCnt);
                Thread.Sleep(1000);
                sleepCnt++;
                if (sleepCnt > 60)
                {
                    break;
                }
            }
            
            Assert.True(_manager.setupFinished);
        }
        
        // Utility methods 
        
        public string GetMountDirPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));

            // Create mountdir if it does not already exist
            var mountdirPath = Path.Combine(baseDir, @"fuse/directories/mountdir");
            Directory.CreateDirectory(mountdirPath);
            
            var cDir = Path.Combine(baseDir, @"fuse/src/");
            var rootdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/rootdir"));
            var mountdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/mountdir"));
            return mountdir;
        }
        
        public string GetRootDirPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));

            // Create mountdir if it does not already exist
            var mountdirPath = Path.Combine(baseDir, @"fuse/directories/mountdir");
            Directory.CreateDirectory(mountdirPath);
            
            var cDir = Path.Combine(baseDir, @"fuse/src/");
            var rootdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/rootdir"));
            var mountdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/mountdir"));
            return rootdir;
        }

        public static void ExecuteShellCommand(string cmd)
        {
            Process proc = new Process ();
            proc.StartInfo.FileName = "/bin/bash";
            proc.StartInfo.Arguments = "-c \" " + cmd + " \"";
            proc.StartInfo.UseShellExecute = false; 
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start ();

            while (!proc.StandardOutput.EndOfStream) {
                Console.WriteLine (proc.StandardOutput.ReadLine ());
            }
        }
        
        protected void WaitForUploads(int cntLimit = 25)
        {
            Console.WriteLine("before sleep 2");
            var sleepCnt2 = 0;
            while (true)
            {
                if (_manager.UploadsInProgress <= 0)
                {
                    Console.WriteLine("sleepCnt = " + sleepCnt2);
                    Thread.Sleep(1000);
                    sleepCnt2++;
                    if (_manager.UploadsInProgress <= 0)
                    {
                        break;
                    }
                }

                Console.WriteLine("sleepCnt = " + sleepCnt2 + ", _manager.UploadsInProgress = " + _manager.UploadsInProgress);
                Thread.Sleep(1000);
                sleepCnt2++;
                if (sleepCnt2 >= cntLimit)
                {
                    break;
                }
            }

            Console.WriteLine("after sleep 2");
        }
    }
}