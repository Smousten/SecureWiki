using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Cryptography;
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

        public RootKeyring rootKeyring;
        public Dictionary<string, string> RequestedRevision = new();
        
        private readonly string _smtpClientEmail = "SecureWikiMails@gmail.com";
        private readonly string _smtpClientPassword = "SecureWiki";

        public delegate void PrintTest(string input);

        public PrintTest printTest;

        public Manager(Thread createrThread, RootKeyring rk)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
            rootKeyring = rk;
        }

        public void Run()
        {
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED", httpClient, this, "127.0.0.1");
            _keyring = new Keyring(rootKeyring);
            _crypto = new Crypto();
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            _keyring.InitKeyring();

            TCPListenerThread = new Thread(tcpListener.RunListener) {IsBackground = true};
            TCPListenerThread.Start();

            Thread.Sleep(1000);

            Thread fuseThread = new(Program.RunFuse);
            fuseThread.IsBackground = true;
            fuseThread.Start();
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

        public string GetPageContent(string pageTitle, string revID)
        {
            MediaWikiObjects.PageQuery.PageContent pc = new(wikiHandler.MWO, pageTitle, revID);
            string output = pc.GetContent();
        
            return output;
        }
        
        // public string GetPageContent(string pageTitle)
        // {
        //     MediaWikiObjects.PageQuery.PageContent pc = new(wikiHandler.MWO, pageTitle);
        //     string output = pc.GetContent();
        //
        //     return output;
        // }


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

        public void UploadNewVersion(string pageTitle, string filepath)
        {
            wikiHandler.UploadNewVersion(pageTitle, filepath);
        }
        
        public void SetMediaWikiServer(string url)
        {
            httpClient = new HttpClient();
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED", httpClient, this, url);
        }

        public string ReadFile(string filename)
        {
            return wikiHandler.ReadFile(filename);
        }
        
        public void LoginToMediaWiki(string username, string password)
        {
            throw new NotImplementedException();
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
        
        // public void RemoveFile(string filePath, string filename, string type)
        // {
        //     _keyring.RemoveFile(filePath, filename, type);
        // }
        
        public void RemoveFile(string filePath, string filename)
        {
            _keyring.RemoveFile(filePath, filename);
        }

        public void ExportKeyring()
        {
            _keyring.ExportRootKeyringBasedOnIsChecked();
        }

        public void ImportKeyring(string importPath)
        {
            Console.WriteLine("Manager:- ImportKeyring('{0}')", importPath);
            _keyring.ImportRootKeyring(importPath);
        }

        // Delegated Crypto functions
        public DataFileEntry? GetDataFile(string filename, KeyringEntry keyring)
        {
            return _keyring.GetDataFile(filename, keyring);
        }

        public byte[] EncryptAesStringToBytes(string plainText, byte[] symmKey, byte[] iv)
        {
            return _crypto.EncryptAESStringToBytes(plainText, symmKey, iv);
        }

        public string DecryptAesBytesToString(byte[] pageContentBytes, byte[] symmKey, byte[] iv)
        {
            return _crypto.DecryptAESBytesToString(pageContentBytes, symmKey, iv);
        }

        public byte[] SignData(byte[] privateKey, string plainText)
        {
            return _crypto.SignData(privateKey, plainText);
        }

        public bool VerifyData(byte[] publicKey, string plainText, byte[] signedData)
        {
            return _crypto.VerifyData(publicKey, plainText, signedData);
        }


        public void SendEmail(string recipientEmail)
        {
            string mailto = string.Format("xdg-email mailto:{0}?subject={1}&body={2}", recipientEmail, "SecureWiki", "Hello");
            Console.WriteLine(mailto);
            Process.Start(mailto);
            // var smtpClient = new SmtpClient("smtp.gmail.com")
            // {
            //     Port = 587,
            //     Credentials = new NetworkCredential(_smtpClientEmail, _smtpClientPassword),
            //     EnableSsl = true,
            // };
            //
            // var mailMessage = new MailMessage
            // {
            //     From = new MailAddress(_smtpClientEmail),
            //     Subject = "SecureWiki file sharing",
            //     Body = "<h1>Hello</h1>" +
            //             "<br />You have received a new keyring" +
            //             "<p>Sincerely,<br />" +
            //             "<br />" +
            //             "<br />" +
            //             "Kevin Sanders<br />" +
            //             "<i>Vice President</i></p>",
            //     IsBodyHtml = true,
            // };
            // // TODO: send selected keyring and not all
            // var keyringPath = _keyring.GetKeyringFilePath();
            // var attachment = new Attachment(keyringPath, MediaTypeNames.Application.Json);
            // mailMessage.Attachments.Add(attachment);
            // mailMessage.To.Add(recipientEmail);
            //
            // Console.WriteLine(recipientEmail);
            // smtpClient.Send(mailMessage);
        }
    }
}