using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.ReactiveUI;

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
            BuildAvaloniaApp() 
                .StartWithClassicDesktopLifetime(args);
        }
        
        public static void RunFuse()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));

            // Create mountdir if it does not already exist
            var mountdirPath = Path.Combine(baseDir, @"fuse/directories/mountdir");
            var mountDirectoryInfo = new DirectoryInfo(mountdirPath);
            mountDirectoryInfo.Create();

            foreach (var file in mountDirectoryInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (var folder in mountDirectoryInfo.GetDirectories())
            {
                folder.Delete(true);
            }
            
            var cDir = Path.Combine(baseDir, @"fuse/src/");
            var cExe = Path.Combine(cDir, @"bbfs");
            ProcessStartInfo start = new();
            start.FileName = cExe;
            var rootdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/rootdir"));
            var mountdir = Path.GetFullPath(Path.Combine(cDir, @"../directories/mountdir"));
            
            Console.WriteLine(rootdir + "\n" + mountdir);

            start.Arguments = string.Format("{0} {1} {2}", "-o direct_io", rootdir, mountdir);

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            Process process = Process.Start(start)!;
            process.WaitForExit();
            process.Close();
        }
        
        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}