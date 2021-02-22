using System;
using System.Net.Http;
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
        private static HttpClient httpClient= new ();

        public delegate void PrintTest(string input);
        public PrintTest printTest;

        public Manager(Thread createrThread)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
        }

        public void Run()
        {
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED", httpClient);
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", wikiHandler, keyRing);
            
            TCPListenerThread = new(tcpListener.RunListener);
            TCPListenerThread.IsBackground = true;
            TCPListenerThread.Start();
            
            Thread fuseThread = new(Program.RunFuse);
            fuseThread.IsBackground = true;
            fuseThread.Start();

            //printTest("www2");
        }
        
        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" + Thread.CurrentThread.Name);
        }

        public MediaWikiObjects.PageQuery.AllRevisions GetAllRevisions(string pageTitle)
        {
            MediaWikiObjects.PageQuery.AllRevisions allRevisions = new(wikiHandler.MWO, pageTitle);

            allRevisions.GetAllRevisions();
            Console.WriteLine("Printing all revisions from manager:");
            allRevisions.PrintAllRevisions();

            MediaWikiObjects.PageQuery.AllRevisions output = allRevisions;
            
            return output;
        }
        
        public string GetPageContent(string pageTitle)
        {
            //MediaWikiObjects MWO = new(httpClient);
            
            MediaWikiObjects.PageQuery.PageContent pc = new(wikiHandler.MWO, pageTitle);
            string output = pc.GetContent();
            
            return output;
        }

        public void UndoRevisionsByID(string pageTitle, string startID, string endID)
        {
            MediaWikiObjects.PageAction.UndoRevisions undoRevisions = new(wikiHandler.MWO, pageTitle);
            undoRevisions.UndoRevisionsByID(startID, endID);
        }
        
        public void DeleteRevisionsByID(string pageTitle, string IDs)
        {
            MediaWikiObjects.PageAction.DeleteRevisions deleteRevisions = new(wikiHandler.MWO, pageTitle);
            deleteRevisions.DeleteRevisionsByIDString(IDs);
        }
        
    }
}