using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki
{
    class Program
    {
        
        /*
        private static WikiHandler wikiHandler;
        private static KeyRing keyRing;
        private static TCPListener tcpListener;
        */
        
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args) {
            
            /*
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED");
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", wikiHandler, keyRing);
            Thread instanceCaller = new(tcpListener.RunListener);
            instanceCaller.Start();
            Thread fuseThread = new(RunFuse);
            fuseThread.Start();
            //Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            */
            
            BuildAvaloniaApp() 
                .StartWithClassicDesktopLifetime(args);
            Console.WriteLine("Passed avalonia startup");
        }
        
        public static void RunFuse()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            // var pythonDir = Path.Combine(baseDir, @"Pyfuse_mediaWiki/");
            // var pythonScipt = Path.Combine(pythonDir, @"passthroughfs.py");

            // Create mountdir if it does not already exist
            var mountdirPath = Path.Combine(baseDir, @"fuse/directories/mountdir");
            Directory.CreateDirectory(mountdirPath);
            
            var cDir = Path.Combine(baseDir, @"fuse/src/");
            var cExe = Path.Combine(cDir, @"bbfs");
            ProcessStartInfo start = new ProcessStartInfo();
            // start.FileName = @"/usr/bin/python3";
            start.FileName = cExe;
            // start.Arguments = string.Format("{0} {1} {2}", pythonScipt, Path.Combine(pythonDir, @"srcTest/"), Path.Combine(pythonDir, @"mntTest/"));
            var rootdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/rootdir"));
            var mountdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/mountdir"));
            
            Console.WriteLine(rootdir + "\n" + mountdir);

            start.Arguments = string.Format("{0} {1} {2}", "-o direct_io", rootdir, mountdir);
            // start.Arguments = string.Format("{0} {1}", rootdir, mountdir);

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            Process process = Process.Start(start);
            process?.WaitForExit();
            process?.Close();
        }
        
        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}