using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DynamicData;
using Newtonsoft.Json;
using SecureWiki.Cryptography;
using SecureWiki.FuseCommunication;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.Views;

namespace SecureWiki
{
    public class Manager
    {
        private Thread TCPListenerThread;
        private Thread WikiHandlerThread;
        private Thread CryptoThread;
        private Thread GUIThread;

        private Dictionary<string, IServerInteraction> wikiHandlers;
        private IServerInteraction localhostWikiHandler;
        private Keyring _keyring;
        private IFuseInteraction tcpListener;
        private static HttpClient httpClient = new();
        public CacheManager cacheManager;
        public ConfigManager configManager;
        public ContactManager contactManager;

        public Logger logger;
        public RootKeyring rootKeyring;
        public Dictionary<string, string> RequestedRevision = new();

        private readonly string _smtpClientEmail = "SecureWikiMails@gmail.com";
        private readonly string _smtpClientPassword = "SecureWiki";

        public delegate void PrintTest(string input);

        public PrintTest printTest;

        public Manager(Thread createrThread, RootKeyring rk, Logger logger)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
            rootKeyring = rk;
            this.logger = logger;
        }

        public void Run()
        {
            InitializeConfigManager();
            InitializeWikiHandlers();

            // localhostWikiHandler = new WikiHandler("new_mysql_user",
            //     "THISpasswordSHOULDbeCHANGED", httpClient, this, "http://localhost/mediawiki/api.php");
            // wikiHandlers.Add("http://localhost/mediawiki/api.php", localhostWikiHandler);

            _keyring = new Keyring(rootKeyring);
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            _keyring.InitKeyring();
            InitializeCacheManager();
            InitializeContactManager();

            TCPListenerThread = new Thread(tcpListener.RunListener) {IsBackground = true};
            TCPListenerThread.Start();
            logger.Add("Starting up TCPListener", null);

            Thread.Sleep(1000);

            Thread fuseThread = new(Program.RunFuse) {IsBackground = true};
            fuseThread.Start();

            logger.Add("Starting up FUSE", null);
            // TestUpload();
        }

        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" +
                              Thread.CurrentThread.Name);
        }

        public string GetConfigManagerFilePath()
        {
            const string filename = "Config.json";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var path = Path.Combine(projectDir, filename);

            return path;
        }

        public void InitializeConfigManager()
        {
            var path = GetConfigManagerFilePath();

            if (File.Exists(path))
            {
                configManager = (ConfigManager) JSONSerialization.ReadFileAndDeserialize(path, typeof(ConfigManager));
            }
            else
            {
                configManager = new ConfigManager();
            }
        }

        public void SaveConfigManagerToFile()
        {
            var path = GetConfigManagerFilePath();

            JSONSerialization.SerializeAndWriteFile(path, configManager);
        }

        public void SetCacheSettingGeneral(CachePreferences.CacheSetting setting)
        {
            configManager!.cachePreferences.GeneralSetting = setting;
        }

        public void SetCacheSettingSingleFile(string pageTitle, CachePreferences.CacheSetting? setting)
        {
            configManager!.cachePreferences.SetPreference(pageTitle, setting);
        }

        public CachePreferences.CacheSetting GetCacheSettingGeneral()
        {
            return configManager!.cachePreferences.GeneralSetting;
        }

        public CachePreferences.CacheSetting? GetCacheSettingSingleFile(string pageTitle)
        {
            return configManager!.cachePreferences.GetSetting(pageTitle);
        }

        public void SetDefaultServerLink(string url)
        {
            configManager!.DefaultServerLink = url;
        }
        
        public string GetContactsFilePath()
        {
            const string filename = "Contacts/Contacts.json";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var path = Path.Combine(projectDir, filename);

            return path;
        }
        
        public string GetOwnContactsFilePath()
        {
            const string filename = "Contacts/OwnContacts.json";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var path = Path.Combine(projectDir, filename);

            return path;
        }
        
        public void InitializeContactManager()
        {
            var contactsPath = GetContactsFilePath();
            var ownContactsPath = GetOwnContactsFilePath();

            contactManager = new ContactManager(this);
            
            // Read existing contacts from their respective files
            if (File.Exists(contactsPath))
            {
                contactManager.Contacts = (List<Contact>) JSONSerialization.ReadFileAndDeserialize(contactsPath, typeof(List<Contact>));
            }
            if (File.Exists(ownContactsPath))
            {
                contactManager.OwnContacts = (List<OwnContact>) JSONSerialization.ReadFileAndDeserialize(ownContactsPath, typeof(List<OwnContact>));
            }
        }

        public void SaveContactManagerToFile()
        {
            var contactsPath = GetContactsFilePath();
            var ownContactsPath = GetOwnContactsFilePath();
            
            JSONSerialization.SerializeAndWriteFile(contactsPath, contactManager.Contacts);
            JSONSerialization.SerializeAndWriteFile(ownContactsPath, contactManager.OwnContacts);
        }

        private void InitializeWikiHandlers()
        {
            wikiHandlers = new();

            // TODO: read from config file
        }

        private IServerInteraction? GetWikiHandler(string url)
        {
            // Console.WriteLine("attempting to get wikihandler with url "+ url);
            if (wikiHandlers.ContainsKey(url))
            {
                // Console.WriteLine("found in existing wikihandlers");
                return wikiHandlers[url];
            }
            else
            {
                // Console.WriteLine("not found in existing wikihandlers");
                var newWikiHandler = CreateNewWikiHandler(url);

                if (newWikiHandler != null && !wikiHandlers.ContainsKey(url))
                {
                    wikiHandlers.Add(url, newWikiHandler);
                    return newWikiHandler;
                }
            }

            return null;
        }

        private IServerInteraction? CreateNewWikiHandler(string url)
        {
            var serverCredentials = configManager.GetServerCredentials(url);

            string? savedUsername = null;

            if (serverCredentials?.Username != null)
            {
                if (serverCredentials.ProtectedPassword != null && serverCredentials.Entropy != null)
                {
                    var unprotectedPassword =
                        ConfigEntry.Unprotect(serverCredentials.ProtectedPassword, serverCredentials.Entropy);
                    if (unprotectedPassword != null)
                        return new WikiHandler(serverCredentials.Username, unprotectedPassword,
                            new HttpClient(), this, url);
                }

                savedUsername = serverCredentials.Username;
            }

            const string title = "MediaWiki server login";
            string content = "Enter credentials for server: " + url;

            CredentialsPopup.CredentialsResult credentialsResult =
                ShowPopupEnterCredentials(title, content, savedUsername);

            if (credentialsResult.ButtonResult == CredentialsPopup.PopupButtonResult.Cancel ||
                credentialsResult.Username.Equals("") || credentialsResult.Password.Equals(""))
            {
                return null;
            }

            // Create new wiki handler from input credentials and attempt login to server
            var wikiHandler = new WikiHandler(credentialsResult.Username, credentialsResult.Password, new HttpClient(),
                this, url);

            // Return wikihandler if login was successful

            if (!wikiHandler.LoggedIn)
            {
                return null;
            }

            if (credentialsResult.SaveUsername)
            {
                configManager.AddEntry(url, credentialsResult.Username,
                    credentialsResult.SavePassword ? credentialsResult.Password : null);
            }
            else
            {
                Console.WriteLine("removing entry");
                configManager.RemoveEntry(url);
            }

            return wikiHandler;
        }

        public MediaWikiObjects.PageQuery.AllRevisions? GetAllRevisions(string pageTitle, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            return wikiHandler?.GetAllRevisions(pageTitle);
        }

        public async void UpdateAllRevisionsAsync(string pageTitle, string url,
            ObservableCollection<Revision> revisions)
        {
            var wikiHandler = GetWikiHandler(url);
            var allRev = wikiHandler?.GetAllRevisions(pageTitle);

            revisions.Clear();

            if (allRev?.revisionList != null)
            {
                revisions.AddRange(allRev.revisionList);
            }
        }

        public string? GetPageContent(string pageTitle, string revID, string url)
        {
            // Write to logger
            string loggerMsg =
                $"Attempting to read file from page title '{pageTitle}', revision {revID} on server '{url}'";
            logger.Add(loggerMsg);

            var wikiHandler = GetWikiHandler(url);
            var output = wikiHandler?.GetPageContent(pageTitle, revID);

            // Write to logger if read fails
            if (output == null)
            {
                logger.Add(wikiHandler == null
                    ? $"File read failed due to missing server credentials"
                    : $"Could not read file from server");
            }

            return output;
        }

        public bool UndoRevisionsByID(string pageTitle, string startID, string endID, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            wikiHandler?.UndoRevisionsByID(pageTitle, startID, endID);
            return true;
        }

        public void DeleteRevisionsByID(string pageTitle, string IDs, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            wikiHandler?.DeleteRevisionsByID(pageTitle, IDs);
        }

        public void UploadNewVersion(string filename, string filepath)
        {
            DataFileEntry? df = GetDataFile(filename, rootKeyring);
            var keyList = df?.keyList.Last();
            if (keyList?.PrivateKey != null)
            {
                var wikiHandler = GetWikiHandler(df!.serverLink);

                if (wikiHandler != null)
                {
                    // Write to logger
                    string loggerMsg = "Attempting to upload file to server '" + df!.serverLink + "'";
                    logger.Add(loggerMsg, filepath);

                    wikiHandler?.Upload(df!, filepath);
                }
                else
                {
                    // Write to logger
                    string loggerMsg = $"File upload to server '{df!.serverLink}' " +
                                       $"failed due to missing server credentials";
                    logger.Add(loggerMsg, null);
                }
            }
            else
            {
                Console.WriteLine("{0}: the corresponding DataFileEntry does not contain" +
                                  " a private key, upload cancelled", filepath);
            }
        }

        public MessageBox.MessageBoxResult ShowMessageBox(string title, string content,
            MessageBox.MessageBoxButtons buttons = MessageBox.MessageBoxButtons.OkCancel)
        {
            // Invoke UI thread with highest priority
            var output = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                MessageBox.MessageBoxResult result = MessageBox.MessageBoxResult.No;
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                    desktop)
                {
                    result = await MessageBox.Show(desktop.MainWindow, content, title,
                        buttons);
                }

                return result;
            }, DispatcherPriority.MaxValue).Result;

            return output;
        }

        public CredentialsPopup.CredentialsResult ShowPopupEnterCredentials(string title, string content,
            string? savedUsername)
        {
            // Invoke UI thread with highest priority
            var output = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                CredentialsPopup credentialsPopup = new();
                CredentialsPopup.CredentialsResult result = new();
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                    desktop)
                {
                    result = await credentialsPopup.Show(desktop.MainWindow, content, title, savedUsername);
                }

                return result;
            }, DispatcherPriority.MaxValue).Result;

            // Console.WriteLine("output: username='{0}', password='{1}', usernameBool='{2}', passwordBool='{3}'", 
            //     output.Username, output.Password, output.SaveUsername, output.SavePassword);

            return output;
        }


        // public void SetNewMediaWikiServer(string url)
        // {
        //     httpClient = new HttpClient();
        //     wikiHandler = new WikiHandler("new_mysql_user",
        //         "THISpasswordSHOULDbeCHANGED", httpClient, this, url);
        // }

        public byte[]? Download(string filename)
        {
            logger.Add($"Attempting read file '{filename}'");
            var dataFile = GetDataFile(filename, rootKeyring);

            if (dataFile == null) return null;

            var wikiHandler = GetWikiHandler(dataFile.serverLink);

            return RequestedRevision.ContainsKey(dataFile.pageName)
                ? wikiHandler?.Download(dataFile, RequestedRevision[dataFile.pageName])
                : wikiHandler?.Download(dataFile);
        }

        public void LoginToMediaWiki(string username, string password)
        {
            throw new NotImplementedException();
        }

        // Delegated Keyring functions
        public void AddNewFile(string filename, string filepath)
        {
            _keyring.AddNewFile(filename, filepath, configManager.DefaultServerLink);
        }

        public void AddNewKeyRing(string filename, string filepath)
        {
            _keyring.AddNewKeyRing(filename, filepath);
        }

        public void RenameFile(string oldPath, string newPath)
        {
            logger.Add($"Renaming '{oldPath}' to '{newPath}'.");
            _keyring.Rename(oldPath, newPath);
        }

        public KeyringEntry ReadKeyRing()
        {
            return _keyring.ReadKeyRing();
        }

        public void RemoveFile(string filePath, string filename)
        {
            WriteToLogger($"Removing file '{filename}'", filePath);
            _keyring.RemoveFile(filePath, filename);
        }

        public void ExportKeyring()
        {
            // TODO: add export path
            logger.Add("Exporting keyring");
            _keyring.ExportRootKeyringBasedOnIsChecked();
        }

        public void ImportKeyring(string importPath)
        {
            Console.WriteLine("Manager:- ImportKeyring('{0}')", importPath);
            logger.Add($"Importing keyring from '{importPath}'");
            _keyring.ImportRootKeyring(importPath);
        }

        public void SaveKeyringToFile()
        {
            _keyring.SaveRootKeyring();
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

            return File.ReadAllText(cacheResult);
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
            cacheManager.CleanCacheDirectory(configManager.cachePreferences ?? new CachePreferences());
        }

        // Delegated Crypto functions
        public DataFileEntry? GetDataFile(string filename, KeyringEntry keyring)
        {
            return _keyring.GetDataFile(filename, keyring);
        }

        public byte[]? Encrypt(byte[] plainText, byte[] symmKey, byte[] iv)
        {
            return Crypto.Encrypt(plainText, symmKey, iv);
        }

        public byte[]? Decrypt(byte[] pageContentBytes, byte[] symmKey, byte[] iv)
        {
            return Crypto.Decrypt(pageContentBytes, symmKey, iv);
        }

        public byte[] SignData(byte[] privateKey, byte[] plainText)
        {
            return Crypto.SignData(privateKey, plainText);
        }

        public bool VerifyData(byte[] publicKey, byte[] plainText, byte[] signedData)
        {
            return Crypto.VerifyData(publicKey, plainText, signedData);
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

        public void RevokeAccess(DataFileEntry datafile, ObservableCollection<Contact> contacts)
        {
            logger.Add($"Attempting to revoke access to file '{datafile.filename}'");

            var wikiHandler = GetWikiHandler(datafile.serverLink);
            var latestRevision = wikiHandler?.GetLatestRevision(datafile);

            if (latestRevision?.revisionID != null && datafile.ownerPrivateKey != null)
            {
                _keyring.RevokeAccess(datafile, latestRevision.revisionID);
            }

            var latestKey = datafile.keyList.Last();
            var latestKeySerializeObject = JSONSerialization.SerializeObject(latestKey);
            foreach (var contact in contacts)
            {
                UploadToInboxPage(contact.ServerLink, contact.PageTitle, latestKeySerializeObject, contact.PublicKey);
            }
        }

        public void WriteToLogger(string content, string? location = null,
            LoggerEntry.LogPriority priority = LoggerEntry.LogPriority.Normal)
        {
            logger.Add(content, location, priority);
        }

        public void ImportContact(string path)
        {
            logger.Add($"Importing contacts from '{path}'");
            var newContacts = (List<Contact>) JSONSerialization.ReadFileAndDeserialize(
                path, typeof(List<Contact>));
            contactManager.MergeContacts(newContacts);
        }

        public void GenerateOwnContact(string serverLink, string nickname)
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            // var url = "http://" + serverLink + "/mediawiki/api.php";
            OwnContact newContact = new(serverLink, pageTitle, nickname);
            contactManager.AddOwnContact(newContact);
        }

        public void GetAllContacts(ObservableCollection<Contact> contactsOwn, ObservableCollection<Contact> contactsOther)
        {
            contactsOwn.Clear();
            contactsOther.Clear();
            if (contactManager.OwnContacts.Count > 0)
            {
                contactsOwn.AddRange(contactManager.OwnContacts);
            }

            if (contactManager.Contacts.Count > 0)
            {
                contactsOther.AddRange(contactManager.Contacts);
            }
        }

        public void ExportContacts(ObservableCollection<Contact> exportContacts)
        {
            logger.Add("Exporting contacts");

            var noDuplicates = exportContacts.Distinct().ToList();
            
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var exportFileName = "ContactExport.json";
            var exportFilePath = Path.Combine(path, exportFileName);
            JSONSerialization.SerializeAndWriteFile(exportFilePath, noDuplicates);
        }

        public void GetOtherContacts(ObservableCollection<Contact> contacts)
        {
            contacts.Clear();
            if (contactManager.Contacts.Count > 0)
            {
                contacts.AddRange(contactManager.Contacts);
            }
        }

        public void UploadToInboxPage(string serverLink, string pageTitle, string content, byte[] publicKey)
        {
            var wikiHandler = GetWikiHandler(serverLink);

            wikiHandler?.UploadToInboxPage(pageTitle, content, publicKey);
        }
        
        public void TestUpload()
        {
            var contact = contactManager.GetOwnContactByNickname("Test");
        
            if (contact == null)
            {
                Console.WriteLine("contact is null");
                return;
            }
            
            // var pubKey =
            //     "MIIBCgKCAQEAug/PiOEJGPvdFdfyhMZLzp1ELdH1UBNMStxnGAQ3eQRJ0RyzgmSvq9FD9g106oPpz+GxaLjPplhz10bn108IwpjcB4+5XLMhedU0K4bOUHpSwsn+af6nkinU5/3BYN2EsI1hR31GNn0HiR0utJVs/6/CIZ/6RWPd4Z4CbD0f+Og4v3x24a0eYgr/vb02+T0HVG9gOyjomPnLiCj+pqnLb+x1Evpyy2y8SXXR76YpP+CVtgMRmQ4k+6YHU3VLCGTmwDEEvhm6KkjozA3A3RAl2M4BvKTZiHG1SxM79pUJkpFSor2SuRmrAr1S4tCgY9wBhBf0yRBZJa9xxjSVnZkWEwIDAQAB";
            // var pubKeyBytes = Convert.FromBase64String(pubKey);
        
            var pubKeyBytes = contact.PublicKey;
        
            var df = rootKeyring.dataFiles.First();
        
            var dfString = JSONSerialization.SerializeObject(df);
        
            Console.WriteLine("dfString");
            Console.WriteLine(dfString);
        
            string content = dfString;
        
            UploadToInboxPage(contact.ServerLink, contact.PageTitle, content, pubKeyBytes);
        }

        public void TestDownloadInboxes()
        {
            var wikiHandler = GetWikiHandler("http://192.168.1.7/mediawiki/api.php");
            wikiHandler?.DownloadFromInboxPages();
        }
        
        public void TestDownload()
        {
            // var contact = contactManager.GetOwnContactByNickname("Test");
            var contact = contactManager.GetContactByPageTitle("tcyaj7kTGsafdsflxVL5vbFsI");
        
            if (contact == null)
            {
                Console.WriteLine("contact is null");
                return;
            }
            Console.WriteLine("contact is not null");
            
            // var pubKey =
            //     "MIIBCgKCAQEAug/PiOEJGPvdFdfyhMZLzp1ELdH1UBNMStxnGAQ3eQRJ0RyzgmSvq9FD9g106oPpz+GxaLjPplhz10bn108IwpjcB4+5XLMhedU0K4bOUHpSwsn+af6nkinU5/3BYN2EsI1hR31GNn0HiR0utJVs/6/CIZ/6RWPd4Z4CbD0f+Og4v3x24a0eYgr/vb02+T0HVG9gOyjomPnLiCj+pqnLb+x1Evpyy2y8SXXR76YpP+CVtgMRmQ4k+6YHU3VLCGTmwDEEvhm6KkjozA3A3RAl2M4BvKTZiHG1SxM79pUJkpFSor2SuRmrAr1S4tCgY9wBhBf0yRBZJa9xxjSVnZkWEwIDAQAB";
            // var pubKeyBytes = Convert.FromBase64String(pubKey);
        
            var wikihandler = GetWikiHandler(contact.ServerLink);
        
            if (wikihandler == null)
            {
                Console.WriteLine("wikihandler is null");
                return;
            }
            Console.WriteLine("wikihandler is not null");
            
            var output = wikihandler.DownloadFromInboxPages();
        
            if (output == null)
            {
                Console.WriteLine("output is null");
                return;
            }
            Console.WriteLine("output is not null");
        
            foreach (var item in output)
            {
                Console.WriteLine("count: " + item.Count);
            }
        }

        public void ShareSelectedKeyring(List<Contact> contacts)
        {
            logger.Add("Sharing keyring");
            
            var dataFileList = _keyring.GetListOfAllCheckedDataFiles();

            foreach (var contact in contacts)
            {
                var newDataFiles = new List<DataFileEntry>();

                foreach (var df in dataFileList)
                {
                    var contactInfo = df.GetContactInfo
                        (contact.PageTitle, contact.ServerLink);

                    if (contactInfo == null)
                    {
                        df.AddContactInfo(contact.PageTitle, contact.ServerLink);
                        newDataFiles.Add(df);   
                    }
                }

                var keyringEntry = new KeyringEntry();
                
                keyringEntry.dataFiles.AddRange(newDataFiles);

                var keyringEntryString = JSONSerialization.SerializeObject(keyringEntry);

                var loggerMsg = $"Sharing {newDataFiles.Count} new files with contact '{contact.Nickname}'.";
                logger.Add(loggerMsg);
                
                UploadToInboxPage(contact.ServerLink, contact.PageTitle, 
                    keyringEntryString, contact.PublicKey);
            }
        }
        
    }
}