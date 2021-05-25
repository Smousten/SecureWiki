using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public class FuseTest
    {
        private Manager _manager;
        
        [SetUp]
        public void Setup()
        {
            var serverLink = "http://127.0.0.1/mediawiki/api.php";
            var pageName = RandomString.GenerateRandomAlphanumericString();
            pageName = char.ToUpper(pageName[0]) + pageName.Substring(1);

            Console.WriteLine("checking fuse");
            MainWindow.IsFuseRunning();

            Console.WriteLine("Creating manager");
            _manager = new Manager(Thread.CurrentThread, new MasterKeyring(), new Logger(), new MountedDirMirror());
            Thread managerThread = new(_manager.Run) {IsBackground = true, Name = "ManagerThread"};
            managerThread.Start();
            // _manager.Run();
            Console.WriteLine("setup passed");
        }
        
        [TearDown]
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
        // [Test]
        public void PassSetup()
        {
            var sleepCnt = 0;
            while (_manager.setupFinished == false)
            {
                Console.WriteLine("sleepCnt = " + sleepCnt);
                Thread.Sleep(1000);
                sleepCnt++;
                if (sleepCnt > 30)
                {
                    break;
                }
            }
            
            Assert.True(_manager.setupFinished);
        }

        [Test]
        public void CreateFile()
        {
            Console.WriteLine("CreateFile entered");
            var mountdirPath = GetMountDirPath();

            var sleepCnt = 0;
            while (_manager.setupFinished == false)
            {
                Console.WriteLine("sleepCnt = " + sleepCnt);
                Thread.Sleep(1000);
                sleepCnt++;
            }
            
            var cntPre = _manager.mountedDirMirror.RootFolder.combinedList.Count;
            
            // File.Create(mountdirPath + "/CreateFileTest.txt").Dispose();
            var cmd = $"touch {mountdirPath}/CreateFileTest.txt";
            ExecuteShellCommand(cmd);
            Console.WriteLine($"ls {mountdirPath} :");
            ExecuteShellCommand($"ls {mountdirPath}");
            Console.WriteLine();
            
            Thread.Sleep(5000);

            var cntPost = _manager.mountedDirMirror.RootFolder.combinedList.Count;
            
            Assert.Greater(cntPost, cntPre);
        }
        
    }
}