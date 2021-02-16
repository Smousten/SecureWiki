using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using SecureWiki.ClientApplication;
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
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../..")); // very beautiful code
            var pythonDir = Path.Combine(baseDir, @"Pyfuse_mediaWiki/");
            var pythonScipt = Path.Combine(pythonDir, @"passthroughfs.py");
            ProcessStartInfo start = new();
            start.FileName = @"/usr/bin/python3";
            start.Arguments = string.Format("{0} {1} {2}", pythonScipt, Path.Combine(pythonDir, @"srcTest/"), Path.Combine(pythonDir, @"mntTest/"));

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