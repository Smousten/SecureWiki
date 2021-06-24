using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using NUnit.Framework;
using SecureWiki;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.Views;

namespace SecureWikiTests
{
    public class PerformanceTest
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

        [Test]
        public void TestEncryptDecryptAESGCM()
        {
            // 1 KB
            DoEncryptDecryptAESGCM(1024);
            // 10 KB
            DoEncryptDecryptAESGCM(10240);
            // 100 KB
            DoEncryptDecryptAESGCM(102400);
            // 1 MB
            DoEncryptDecryptAESGCM(1048576);
            // 10 MB
            DoEncryptDecryptAESGCM(10485760);
            // 100 MB
            DoEncryptDecryptAESGCM(104857600);
        }

        public void DoEncryptDecryptAESGCM(int size)
        {
            var n = 50;
            var plainText = new Byte[size];
            Random rnd = new Random();
            rnd.NextBytes(plainText);
            Stopwatch stopwatch = new Stopwatch();

            // Generate symmetric key 
            var symmKey = Crypto.GenerateSymmKey();
            Console.WriteLine(symmKey.Length);

            // Encrypt random 20 byte array
            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.Encrypt(plainText, symmKey);
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Time to encrypt {size} bytes using AES GCM (n={n}): {stopwatch.Elapsed.TotalMilliseconds / n} ms");

            var encryptedBytes = Crypto.Encrypt(plainText, symmKey);
            // Decrypt random 20 byte array
            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.Decrypt(encryptedBytes!, symmKey);
            }

            stopwatch.Stop();
            Console.WriteLine(
                $"Time to decrypt {size} bytes using AES GCM (n={n}): {stopwatch.Elapsed.TotalMilliseconds / n} ms");
        }

        [Test]
        public void TestEncryptDecryptRSA()
        {
            var n = 10;
            var plainText = new Byte[256 - 11];
            Random rnd = new Random();
            rnd.NextBytes(plainText);

            Stopwatch stopwatch = new Stopwatch();

            // Generate symmetric key 
            var (priKey, pubKey) = Crypto.GenerateRSAParams();

            // Encrypt random 20 byte array
            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.RSAEncrypt(plainText, pubKey);
            }

            stopwatch.Stop();
            Console.WriteLine($"Time to encrypt 245 B using RSA (n={n}): {stopwatch.ElapsedMilliseconds / n} ms");

            var encryptedBytes = Crypto.RSAEncrypt(plainText, pubKey);

            // Decrypt random 20 byte array
            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.RSADecrypt(encryptedBytes!, priKey);
            }

            stopwatch.Stop();
            Console.WriteLine($"Time to decrypt 245 B using RSA (n={n}): {stopwatch.ElapsedMilliseconds / n} ms");
        }

        [Test]
        public void TestSignAndVerify()
        {
            // 1 KB
            DoSignAndVerify(1024);
            // 10 KB
            DoSignAndVerify(10240);
            // 100 KB
            DoSignAndVerify(102400);
            // 1 MB
            DoSignAndVerify(1048576);
            // 10 MB
            DoSignAndVerify(10485760);
            // 100 MB
            DoSignAndVerify(104857600);
        }
        
        public void DoSignAndVerify(int size)
        {
            var n = 50;
            var plainText = new Byte[size];
            Random rnd = new Random();
            rnd.NextBytes(plainText);
            Stopwatch stopwatch = new Stopwatch();

            // Generate symmetric key 
            var (priKey, pubKey) = Crypto.GenerateRSAParams();

            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.SignData(priKey, plainText);
            }

            stopwatch.Stop();
            Console.WriteLine($"Time to sign {size} bytes using RSA (n={n}): {stopwatch.Elapsed.TotalMilliseconds / n} ms");

            // sign plaintext using private key
            var signature = Crypto.SignData(priKey, plainText);

            // verify signature using public key and plaintext
            stopwatch.Restart();
            for (int i = 0; i < n; i++)
            {
                Crypto.VerifyData(pubKey, plainText, signature);
            }

            stopwatch.Stop();
            Console.WriteLine($"Time to verify {size} bytes using RSA (n={n}): {stopwatch.Elapsed.TotalMilliseconds / n} ms");
        }

        [Test]
        public void TestUploadAndDownloadFile()
        {
            // 1 KB
            DoUploadAndDownloadFile(1000);
            // 2 KB
            DoUploadAndDownloadFile(2000);
            // 4 KB
            DoUploadAndDownloadFile(4000);
            // 8 KB
            DoUploadAndDownloadFile(8000);
            // 16 KB
            DoUploadAndDownloadFile(16000);
            // 32 KB
            DoUploadAndDownloadFile(32000);
            // 64 KB
            DoUploadAndDownloadFile(64000);
            // 128 KB
            DoUploadAndDownloadFile(128000);
            // 256 KB
            DoUploadAndDownloadFile(256000);
            // 512 KB
            DoUploadAndDownloadFile(512000);
            // 1024 KB
            DoUploadAndDownloadFile(1024000);
            // 2048 KB
            DoUploadAndDownloadFile(2048000);
            // 4096 KB
            DoUploadAndDownloadFile(4096000);
        }

        public void DoUploadAndDownloadFile(int size)
        {
            var serverLink = _manager.configManager.DefaultServerLink;

            var plainText = new Byte[size];
            Random rnd = new Random();
            rnd.NextBytes(plainText);

            var contentString = Convert.ToBase64String(plainText);
            var pageName = _manager.GetFreshPageName();
            WaitForUploads();

            Assert.True(pageName?.Length > 1);

            var wh = _manager.GetWikiHandler(serverLink);

            Assert.True(wh != null);

            Stopwatch stopwatch = Stopwatch.StartNew();
            wh.Upload(pageName, contentString);
            WaitForUploads();
            stopwatch.Stop();
            Console.WriteLine($"Time to upload {size} bytes: {stopwatch.ElapsedMilliseconds} ms");

            stopwatch.Restart();
            var downloadedContent = wh.GetPageContent(pageName);
            stopwatch.Stop();

            Assert.AreEqual(contentString, downloadedContent);
            
            Console.WriteLine($"Time to download {size} bytes: {stopwatch.ElapsedMilliseconds} ms");
        }

        // [Test]
        // public void TestEditAndUploadFile()
        // {
        //     // 1 KB
        //     DoEditAndUploadFile(1024);
        //     // 10 KB
        //     DoEditAndUploadFile(10240);
        //     // 100 KB
        //     DoEditAndUploadFile(102400);
        //     // 1 MB
        //     DoEditAndUploadFile(1048576);
        //     // 10 MB
        //     DoEditAndUploadFile(10485760);
        // }

        public void DoEditAndUploadFile(int size)
        {
            Console.WriteLine("EditAndUploadFile() entered");
            var mountdirPath = GetMountDirPath();
            var filename = "CreateFileTest.txt";
            var filePath = Path.Combine(mountdirPath, filename);
            
            var plainText = new Byte[size];
            Random rnd = new Random();
            rnd.NextBytes(plainText);
            var contentString = Encoding.ASCII.GetString(plainText);

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

            Stopwatch stopwatch = Stopwatch.StartNew();
            // var cmdWrite = $"echo '{contentString}' > {filePath}";
            File.WriteAllText(filePath, contentString);
            // ExecuteShellCommand(cmdWrite);
            // Console.WriteLine($"cmdWrite: {cmdWrite}");
            WaitForUploads();
            stopwatch.Stop();

            Console.WriteLine($"Time to upload {size} bytes from FUSE {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();
            // var cmdWrite = $"echo '{contentString}' > {filePath}";
            // var content = Encoding.ASCII.GetString(_manager.GetContent(filename) ??
                                                   // Encoding.ASCII.GetBytes("not the same")); 
            var content = File.ReadAllText(filePath);
            // ExecuteShellCommand(cmdWrite);
            // Console.WriteLine($"cmdWrite: {cmdWrite}");
            stopwatch.Stop();

            Assert.AreEqual(contentString, content);
            Console.WriteLine($"Time to read {size} bytes from FUSE {stopwatch.ElapsedMilliseconds} ms");
        }
        //
        // [Test]
        // public void TestCompare()
        // {
        //     // 1 KB
        //     DoCompare(1024);
        //     // 10 KB
        //     DoCompare(10240);
        //     // 100 KB
        //     DoCompare(102400);
        //     // 1 MB
        //     DoCompare(1048576);
        //     // 5 MB
        //     DoCompare(5242880);
        // }
        //
        // public void DoCompare(int size)
        // {
        //     Console.WriteLine("DoCompare() entered");
        //     var mountdirPath = GetMountDirPath();
        //     var filename = "CreateFileTest.txt";
        //     var filePath = Path.Combine(mountdirPath, filename);
        //     
        //     var plainText = new Byte[size];
        //     Random rnd = new Random();
        //     rnd.NextBytes(plainText);
        //     var contentString = Convert.ToBase64String(plainText);
        //
        //     if (!File.Exists(filePath))
        //     {
        //         Console.WriteLine("file does not exists at '{0}'", filePath);
        //         var cmdTouch = $"touch {filePath}";
        //         ExecuteShellCommand(cmdTouch);
        //         WaitForUploads();
        //     }
        //
        //     var mdFile = _manager.mountedDirMirror.GetMDFile(filename);
        //     var pageName = mdFile?.symmetricReference.accessFileTargetPageName;
        //     var serverLink = mdFile?.symmetricReference.targetAccessFile?.AccessFileReference.serverLink;
        //     
        //     var wh = _manager.GetWikiHandler(serverLink);
        //
        //     Assert.True(wh != null);
        //
        //     wh.Upload(pageName, contentString);
        //     
        //     WaitForUploads();
        //
        //     var cmdCat = $"cat {filePath}";
        //     var stopwatch = Stopwatch.StartNew();
        //     ExecuteShellCommand(cmdCat);
        //     stopwatch.Stop();
        //     Console.WriteLine($"Time to cat {size} bytes: {stopwatch.ElapsedMilliseconds} ms");
        //
        // }
        
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
            Process proc = new Process();
            proc.StartInfo.FileName = "/bin/bash";
            proc.StartInfo.Arguments = "-c \" " + cmd + " \"";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                Console.WriteLine(proc.StandardOutput.ReadLine());
            }
            
        }

        protected void WaitForUploads(int cntLimit = 500)
        {
            Console.WriteLine("before sleep 2");
            var sleepCnt2 = 0;
            while (true)
            {
                if (_manager.UploadsInProgress <= 0)
                {
                    Console.WriteLine("sleepCnt = " + sleepCnt2);
                    Thread.Sleep(100);
                    sleepCnt2++;
                    if (_manager.UploadsInProgress <= 0)
                    {
                        break;
                    }
                }

                Console.WriteLine("sleepCnt = " + sleepCnt2 + ", _manager.UploadsInProgress = " +
                                  _manager.UploadsInProgress);
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