using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        public CacheManager cacheManager;

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
            wikiHandler = new WikiHandler("new_mysql_user",
                "THISpasswordSHOULDbeCHANGED", httpClient, this, "127.0.0.1");
            _keyring = new Keyring(rootKeyring);
            _crypto = new Crypto();
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            _keyring.InitKeyring();
            InitializeCacheManager();

            TCPListenerThread = new Thread(tcpListener.RunListener) {IsBackground = true};
            TCPListenerThread.Start();

            Thread.Sleep(1000);

            Thread fuseThread = new(Program.RunFuse);
            fuseThread.IsBackground = true;
            fuseThread.Start();
        }

        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" +
                              Thread.CurrentThread.Name);
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
            MediaWikiObjects.PageAction.UndoRevisions undoRevisions =
                new(wikiHandler.MWO, pageTitle);
            undoRevisions.UndoRevisionsByID(startID, endID);
        }

        public void DeleteRevisionsByID(string pageTitle, string IDs)
        {
            MediaWikiObjects.PageAction.DeleteRevisions deleteRevisions =
                new(wikiHandler.MWO, pageTitle);
            deleteRevisions.DeleteRevisionsByIDString(IDs);
        }

        public void UploadNewVersion(string filename, string filepath)
        {
            DataFileEntry? df = GetDataFile(filename, rootKeyring);
            var keyList = df?.keyList.Last();
            if (keyList.privateKey != null)
            {
                wikiHandler.UploadNewVersion(df, filepath);
            }
            else
            {
                Console.WriteLine("{0}: the corresponding DataFileEntry does not contain" +
                                  " a private key, upload cancelled", filepath);
            }
        }

        public void UploadNewVersionBytes(string filename, string filepath)
        {
            DataFileEntry? df = GetDataFile(filename, rootKeyring);
            var keyList = df?.keyList.First();
            if (keyList.privateKey != null)
            {
                wikiHandler.UploadNewVersionBytes(df, filepath);
            }
            else
            {
                Console.WriteLine("{0}: the corresponding DataFileEntry does not contain" +
                                  " a private key, upload cancelled", filepath);
            }
        }

        public void SetMediaWikiServer(string url)
        {
            httpClient = new HttpClient();
            wikiHandler = new WikiHandler("new_mysql_user",
                "THISpasswordSHOULDbeCHANGED", httpClient, this, url);
        }

        public string ReadFile(string filename)
        {
            var dataFile = GetDataFile(filename, rootKeyring);

            if (dataFile == null) return "This text is stored securely.";

            // var encryptedFilenameBytes = EncryptAesStringToBytes(filename, 
            //     dataFile.symmKey, dataFile.iv);
            // var encryptedFilenameString = Convert.ToBase64String(encryptedFilenameBytes);
            //
            // // URL does not allow + character, instead encode as hexadecimal
            // var pageTitle = encryptedFilenameString.Replace("+", "%2B");

            if (RequestedRevision.ContainsKey(dataFile.pagename))
            {
                return wikiHandler.ReadFile(dataFile, RequestedRevision[dataFile.pagename]);
            }

            return wikiHandler.ReadFile(dataFile);
        }

        public byte[]? ReadFileBytes(string filename)
        {
            var dataFile = GetDataFile(filename, rootKeyring);

            if (dataFile == null) return null;

            // var encryptedFilenameBytes = EncryptAesStringToBytes(filename, 
            //     dataFile.symmKey, dataFile.iv);
            // var encryptedFilenameString = Convert.ToBase64String(encryptedFilenameBytes);
            //
            // // URL does not allow + character, instead encode as hexadecimal
            // var pageTitle = encryptedFilenameString.Replace("+", "%2B");

            if (RequestedRevision.ContainsKey(dataFile.pagename))
            {
                return wikiHandler.ReadFileBytes(dataFile, RequestedRevision[dataFile.pagename]);
            }

            return wikiHandler.ReadFileBytes(dataFile);
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

        public string? AttemptReadFileFromCache(string pageTitle, string revid)
        {
            string? cacheResult;

            if (revid.Equals("-1"))
            {
                Console.WriteLine("AttemptReadFileFromCache:- revid==-1");
                cacheResult = cacheManager.GetFilePath(pageTitle);
            }
            else
            {
                cacheResult = cacheManager.GetFilePath(pageTitle, revid);
            }

            if (cacheResult == null || File.Exists(cacheResult) == false)
            {
                return null;
            }

            string fileString = File.ReadAllText(cacheResult);
            return fileString;
        }

        public void AddEntryToCache(string pageTitle, Revision rev)
        {
            Console.WriteLine("AddEntryToCache:- cacheManager.AddEntry('{0}', '{1}')", pageTitle, rev);
            cacheManager.AddEntry(pageTitle, rev);
        }

        public void SerializeCacheManagerAndWriteToFile(string path)
        {
            var jsonData = JsonConvert.SerializeObject(cacheManager, Formatting.Indented);
            File.WriteAllText(path, jsonData);
        }

        public CacheManager? ReadFromFileAndDeserializeToCacheManager(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var jsonData = File.ReadAllText(path);
            var output = JsonConvert.DeserializeObject<CacheManager>(jsonData);

            return output;
        }

        private string GetCacheManagerFilePath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var cacheManagerFileName = "CacheManager.json";
            var cacheManagerFilePath = Path.Combine(path, cacheManagerFileName);

            return cacheManagerFilePath;
        }

        public void SaveCacheManagerToFile()
        {
            string path = GetCacheManagerFilePath();
            SerializeCacheManagerAndWriteToFile(path);
        }

        public void InitializeCacheManager()
        {
            string path = GetCacheManagerFilePath();

            var existingCacheManager = ReadFromFileAndDeserializeToCacheManager(path) ?? new CacheManager();
            cacheManager = existingCacheManager;
        }

        public void CleanCache()
        {
            cacheManager.CleanCacheDirectory();
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

        public byte[] EncryptAesBytesToBytes(byte[] plainText, byte[] symmKey, byte[] iv)
        {
            return _crypto.EncryptAesBytesToBytes(plainText, symmKey, iv);
        }

        public string DecryptAesBytesToString(byte[] pageContentBytes, byte[] symmKey, byte[] iv)
        {
            return _crypto.DecryptAESBytesToString(pageContentBytes, symmKey, iv);
        }

        public byte[] DecryptAesBytesToBytes(byte[] pageContentBytes, byte[] symmKey, byte[] iv)
        {
            return _crypto.DecryptAesBytesToBytes(pageContentBytes, symmKey, iv);
        }

        public byte[] SignData(byte[] privateKey, string plainText)
        {
            return _crypto.SignData(privateKey, plainText);
        }

        public byte[] SignBytes(byte[]? privateKey, byte[] plainText)
        {
            return _crypto.SignBytes(privateKey, plainText);
        }

        public bool VerifyData(byte[] publicKey, string plainText, byte[] signedData)
        {
            return _crypto.VerifyData(publicKey, plainText, signedData);
        }

        public bool VerifyBytes(byte[] publicKey, byte[] plainText, byte[] signedData)
        {
            return _crypto.VerifyBytes(publicKey, plainText, signedData);
        }


        public void SendEmail(string recipientEmail)
        {
            // string mailto = string.Format("xdg-email mailto:{0}?subject={1}&body={2}",
            // recipientEmail, "SecureWiki", "Hello");
            // Console.WriteLine(mailto);
            // Process.Start(mailto);
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(_smtpClientEmail, _smtpClientPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpClientEmail),
                Subject = "SecureWiki file sharing",
                Body = "<h1>Hello</h1>" +
                       "<br />You have received a new keyring" +
                       "<p>Sincerely,<br />" +
                       "<br />" +
                       "<br />" +
                       "Kevin Sanders<br />" +
                       "<i>Vice President</i></p>",
                IsBodyHtml = true,
            };
            // TODO: send selected keyring and not all
            var keyringPath = _keyring.GetKeyringFilePath();
            var attachment = new Attachment(keyringPath,
                MediaTypeNames.Application.Json);
            mailMessage.Attachments.Add(attachment);
            mailMessage.To.Add(recipientEmail);

            Console.WriteLine(recipientEmail);
            smtpClient.Send(mailMessage);
        }

        public void RevokeAccess(DataFileEntry datafile)
        {
            Revision latestRevision = wikiHandler.GetLatestRevision(datafile);
            _keyring.RevokeAccess(datafile, latestRevision.revisionID);
        }
    }
}