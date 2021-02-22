using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using IronPython.Hosting;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki.ClientApplication
{
    class Program
    {
        private static WikiHandler wikiHandler;
        private static KeyRing keyRing;
        private static TCPListener tcpListener;

        public static void Main()
        {
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED");
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", wikiHandler, keyRing);
            Thread instanceCaller = new Thread(
                tcpListener.RunListener);
            instanceCaller.Start();
            Thread.Sleep(1000);
            RunFuse();
        }

        private static void RunFuse()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            // var pythonDir = Path.Combine(baseDir, @"Pyfuse_mediaWiki/");
            // var pythonScipt = Path.Combine(pythonDir, @"passthroughfs.py");
            var cDir = Path.Combine(baseDir, @"fuse/src/");
            var cExe = Path.Combine(cDir, @"bbfs");
            ProcessStartInfo start = new ProcessStartInfo();
            // start.FileName = @"/usr/bin/python3";
            start.FileName = cExe;
            // start.Arguments = string.Format("{0} {1} {2}", pythonScipt, Path.Combine(pythonDir, @"srcTest/"), Path.Combine(pythonDir, @"mntTest/"));
            var rootdir = Path.GetFullPath(Path.Combine(cDir, @"../example/rootdir"));
            var mountdir = Path.GetFullPath(Path.Combine(cDir, @"../example/mountdir"));
            
            start.Arguments = string.Format("{0} {1} {2}", "-o direct_io", rootdir, mountdir);

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            Process process = Process.Start(start);
            process?.WaitForExit();
            process?.Close();
        }
        
        
        
    }
}