using System;
using System.Threading;
using SecureWiki.ClientApplication;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki
{
    public class Manager
    {
        private Thread TCPListenerThread;
        private Thread WikiHandlerThread;
        private Thread CryptoThread;
        private Thread GUIThread;
        
        private WikiHandler wikiHandler;
        private KeyRing keyRing;
        private TCPListener tcpListener;

        public delegate void PrintTest(string input);
        public PrintTest printTest;

        public Manager(Thread createrThread)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
        }

        public void Run()
        {
            printTest("www1");
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED");
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", this);
            
            keyRing.InitKeyring();
            
            TCPListenerThread = new(tcpListener.RunListener);
            TCPListenerThread.IsBackground = true;
            TCPListenerThread.Start();
            
            Thread fuseThread = new(Program.RunFuse);
            fuseThread.IsBackground = true;
            fuseThread.Start();

            printTest("www2");
        }
        
        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" + Thread.CurrentThread.Name);
        }

        public MediaWikiObjects.PageQuery.AllRevisions GetAllRevisions(string ID)
        {
            MediaWikiObjects.PageQuery.AllRevisions output = new("Www");

            return output;
        }

        public void AddNewFile(string filepath, string filename)
        {
            keyRing.AddNewFile(filepath, filename);
        }

        public void AddNewKeyRing(string filepath, string keyname)
        {
            keyRing.AddNewKeyRing(filepath, keyname);
        }

        public void RenameFile(string filepath, string oldname, string newname)
        {
            keyRing.RenameFile( filepath,  oldname,  newname);
        }
    }
}