using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SecureWiki.ClientApplication;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;

namespace SecureWiki
{
    public class Manager
    {
        private Thread TCPListenerThread;
        private Thread WikiHandlerThread;
        private Thread CryptoThread;
        private Thread GUIThread;

        private WikiHandler wikiHandler;
        private Keyring _keyring;
        private Crypto _crypto;
        private TCPListener tcpListener;
        private static HttpClient httpClient = new();

        public delegate void PrintTest(string input);

        public PrintTest printTest;

        public Manager(Thread createrThread)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
        }

        public void Run()
        {
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED", httpClient, this);
            _keyring = new Keyring();
            _crypto = new Crypto();
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            _keyring.InitKeyring();

            TCPListenerThread = new(tcpListener.RunListener);
            TCPListenerThread.IsBackground = true;
            TCPListenerThread.Start();

            Thread.Sleep(1000);

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

        public async Task UploadNewVersion(string pageTitle)
        {
            await wikiHandler.UploadNewVersion(pageTitle);
        }

        public Task<string> ReadFile(string filename)
        {
            return wikiHandler.ReadFile(filename);
        }
        
        // Delegated Keyring functions
        public void AddNewFile(string filepath, string filename)
        {
            _keyring.AddNewFile(filepath, filename);
        }

        public void AddNewKeyRing(string filepath, string keyname)
        {
            _keyring.AddNewKeyRing(filepath, keyname);
        }

        public void RenameFile(string oldPath, string newPath)
        {
            _keyring.Rename(oldPath, newPath);
        }

        public KeyringEntry ReadKeyRing()
        {
            return _keyring.ReadKeyRing();
        }

        // Delegated Crypto functions
        public DataFileEntry? GetDataFile(string filename, KeyringEntry keyring)
        {
            return _keyring.GetDataFile(filename, keyring);
        }

        public byte[] EncryptAesStringToBytes(string plainText, byte[] symmKey, byte[] iv)
        {
            return _crypto.EncryptAesStringToBytes(plainText, symmKey, iv);
        }

        public string DecryptAesBytesToString(byte[] pageContentBytes, byte[] symmKey, byte[] iv)
        {
            return _crypto.DecryptAESBytesToString(pageContentBytes, symmKey, iv);
        }
    }
}