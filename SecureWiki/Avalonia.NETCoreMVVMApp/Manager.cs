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
        private IFuseInteraction tcpListener;

        private KeyringManager _keyringManager;
        public CacheManager cacheManager;
        public ConfigManager configManager;
        public ContactManager contactManager;

        public Logger logger;
        public RootKeyring rootKeyring;
        private Dictionary<(string, string), string> RequestedRevision = new();

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

            _keyringManager = new KeyringManager(rootKeyring, this);
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            _keyringManager.InitKeyring();
            InitializeCacheManager();
            InitializeContactManager();

            TCPListenerThread = new Thread(tcpListener.RunListener) {IsBackground = true};
            TCPListenerThread.Start();
            WriteToLogger("Starting up TCPListener", null);

            Thread.Sleep(1000);

            Thread fuseThread = new(Program.RunFuse) {IsBackground = true};
            fuseThread.Start();

            WriteToLogger("Starting up FUSE", null);
            // TestUpload();

            // GUI can now proceed
            MainWindow.ManagerReadyEvent.Set();


            // var res = ShowMessageBox("some very loooooooooooooooooooooooooong title", " and some very loooooooooooooooooooooooooong title", MessageBox.Buttons.YesNoCancel);
            // Console.WriteLine(res.ToString());
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
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../"));
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
            configManager!.CachePreference.GeneralSetting = setting;
        }

        public void SetCacheSettingSingleFile(string pageTitle, CachePreferences.CacheSetting? setting)
        {
            configManager!.CachePreference.SetPreference(pageTitle, setting);
        }

        public CachePreferences.CacheSetting GetCacheSettingGeneral()
        {
            return configManager!.CachePreference.GeneralSetting;
        }

        public CachePreferences.CacheSetting? GetCacheSettingSingleFile(string pageTitle)
        {
            return configManager!.CachePreference.GetSetting(pageTitle);
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
                contactManager.Contacts =
                    (List<Contact>) JSONSerialization.ReadFileAndDeserialize(contactsPath, typeof(List<Contact>));
            }

            if (File.Exists(ownContactsPath))
            {
                contactManager.OwnContacts =
                    (List<OwnContact>) JSONSerialization.ReadFileAndDeserialize(ownContactsPath,
                        typeof(List<OwnContact>));
            }
        }

        public void SaveContactManagerToFile()
        {
            var contactsPath = GetContactsFilePath();
            var ownContactsPath = GetOwnContactsFilePath();

            JSONSerialization.SerializeAndWriteFile(contactsPath, contactManager.Contacts);
            JSONSerialization.SerializeAndWriteFile(ownContactsPath, contactManager.OwnContacts);
        }

        // Contacts in revoke popup list should only show contacts in data file
        public void GetFileContacts(ObservableCollection<Contact> revokeContacts, DataFile dataFile)
        {
            revokeContacts.Clear();
            foreach (var (pageTitle, serverLink) in dataFile.contactList)
            {
                var contact =
                    contactManager.GetContactByPageTitleAndServerLink(pageTitle, serverLink ?? dataFile.serverLink);
                if (contact != null) revokeContacts.Add(contact);
            }
        }

        public void ImportContact(string path)
        {
            WriteToLogger($"Importing contacts from '{path}'");
            var newContacts = JSONSerialization.ReadFileAndDeserialize(
                path, typeof(List<Contact>)) as List<Contact>;

            if (newContacts == null)
            {
                const string loggerMsg = "Import file cannot be parsed as a list of contact objects. Merged aborted.";
                WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                return;
            }

            contactManager.MergeContacts(newContacts);
        }

        public void GenerateOwnContact(string serverLink, string nickname)
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            while (true)
            {
                if (PageAlreadyExists(pageTitle, "-1", serverLink))
                {
                    WriteToLogger($"Auto generated page title ({pageTitle}) already exists on server. Retrying...");
                    pageTitle = RandomString.GenerateRandomAlphanumericString();
                }
                else
                {
                    break;
                }
            }

            // var url = "http://" + serverLink + "/mediawiki/api.php";
            OwnContact newContact = new(serverLink, pageTitle, nickname);
            contactManager.AddOwnContact(newContact);
        }

        public void GetAllContacts(ObservableCollection<Contact> contactsOwn,
            ObservableCollection<Contact> contactsOther)
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
            WriteToLogger("Exporting contacts");

            var contactList = new List<Contact>();

            foreach (var contact in exportContacts)
            {
                if (contact is OwnContact ownContact)
                {
                    contactList.Add(ownContact.ConvertToBaseClass());
                }
                else
                {
                    contactList.Add(contact);
                }
            }

            var noDuplicates = contactList.Distinct().ToList();

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
                    {
                        var output = new WikiHandler(serverCredentials.Username, unprotectedPassword,
                            new HttpClient(), this, url);
                        UpdateFromInboxes(output);
                        return output;
                    }
                }

                savedUsername = serverCredentials.Username;
            }

            const string title = "MediaWiki server login";
            string content = "Enter credentials for server: " + url;

            CredentialsPopup.CredentialsResult credentialsResult =
                ShowPopupEnterCredentials(title, content, savedUsername);

            if (credentialsResult.ButtonResult == CredentialsPopup.Result.Cancel ||
                credentialsResult.Username.Equals("") || credentialsResult.Password.Equals(""))
            {
                return null;
            }

            // Create new wiki handler from input credentials and attempt login to server
            var wikiHandler = new WikiHandler(credentialsResult.Username, credentialsResult.Password, new HttpClient(),
                this, url);
            UpdateFromInboxes(wikiHandler);

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

        // Check inboxes of own contacts to update existing keyring with new updates
        private void UpdateFromInboxes(WikiHandler? wikiHandler)
        {
            List<DataFile> incomingDataFiles = new();
            // Download from inbox - iterate through all new revisions for each contact
            var inboxContent = wikiHandler?.DownloadFromInboxPages();
            if (inboxContent != null)
            {
                foreach (var contactInbox in inboxContent)
                {
                    foreach (var revision in contactInbox)
                    {
                        if (JSONSerialization.DeserializeObject(revision, typeof(Keyring)) is Keyring
                            deserializeObject)
                        {
                            incomingDataFiles.AddRange(deserializeObject.dataFiles);
                        }
                    }
                }
            }

            incomingDataFiles = incomingDataFiles.OrderBy(e => e.pageName).ToList();
            List<DataFile> intermediateList = new();

            // Merge updates to same datafiles and remove duplicates 
            int i = 0;
            while (i < incomingDataFiles.Count)
            {
                int cnt = 1;

                while (i + cnt < incomingDataFiles.Count &&
                       incomingDataFiles[i].pageName.SequenceEqual(incomingDataFiles[i + cnt].pageName))
                {
                    if (incomingDataFiles[i].serverLink.Equals(incomingDataFiles[i + cnt].serverLink))
                    {
                        incomingDataFiles[i].MergeWithOtherDataFileEntry(incomingDataFiles[i + cnt]);
                    }

                    cnt++;
                }

                intermediateList.Add(incomingDataFiles[i]);
                i += cnt;
            }

            // Get all existing datafiles in a list
            List<DataFile> newDataFiles = new();
            var existingDataFiles = rootKeyring.GetAllAndDescendantDataFileEntries();
            existingDataFiles = existingDataFiles.OrderBy(entry => entry.pageName).ToList();

            // For each incoming datafile from inbox, merge with existing datafile or add to list of new files
            foreach (var dataFile in intermediateList)
            {
                var existingDf = existingDataFiles.Find(e => e.pageName.Equals(dataFile.pageName));
                if (existingDf != null)
                {
                    existingDf.MergeWithOtherDataFileEntry(dataFile);
                }
                else
                {
                    newDataFiles.Add(dataFile);
                }
            }

            // Add new datafiles to folder the same import folder
            if (newDataFiles.Count > 0)
            {
                if (!rootKeyring.keyrings.Any(e => e.name.Equals("ImportedFromContacts")))
                {
                    rootKeyring.AddKeyring(new Keyring("ImportedFromContacts"));
                }

                var importFolder = rootKeyring.keyrings.First(e => e.name.Equals("ImportedFromContacts"));
                importFolder.AddRangeDataFile(newDataFiles);
            }
            
            _keyringManager.SortAndUpdatePeripherals();
        }

        public void ForceUpdateFromAllInboxPages()
        {
            var serverLinks = contactManager.GetAllUniqueServerLinksFromOwnContacts();

            if (serverLinks == null)
            {
                return;
            }

            foreach (var serverLink in serverLinks)
            {
                // If wikihandler already exists, let it update from inbox pages again
                if (wikiHandlers.ContainsKey(serverLink))
                {
                    if (GetWikiHandler(serverLink) is WikiHandler wh)
                    {
                        UpdateFromInboxes(wh);
                    }
                }
                // else attempt to create a new wikihandler of that serverlink
                // this will automatically call UpdateFromInboxes() if wikihandler can be created
                else
                {
                    GetWikiHandler(serverLink);
                }
            }
            
        }

        public MediaWikiObject.PageQuery.AllRevisions? GetAllRevisions(string pageTitle, string url)
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
            WriteToLogger($"Attempting to read file from page title '{pageTitle}', revision {revID} on server '{url}'");

            var wikiHandler = GetWikiHandler(url);
            var output = wikiHandler?.GetPageContent(pageTitle, revID);

            // Write to logger if read fails
            if (output == null)
            {
                var loggerMsg = (wikiHandler == null
                    ? $"File read failed due to missing server credentials"
                    : $"Could not read file from server");
                WriteToLogger(loggerMsg);
            }

            return output;
        }

        private bool PageAlreadyExists(string pageTitle, string revID, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            return wikiHandler != null && wikiHandler.PageAlreadyExists(pageTitle, revID);
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
            DataFile? df = GetDataFile(filename, rootKeyring);
            var keyList = df?.keyList.Last();
            if (keyList?.PrivateKey != null)
            {
                var wikiHandler = GetWikiHandler(df!.serverLink);

                if (wikiHandler != null)
                {
                    // Write to logger
                    string loggerMsg = "Attempting to upload file to server '" + df!.serverLink + "'";
                    WriteToLogger(loggerMsg, filepath);

                    wikiHandler.Upload(df!, filepath);
                }
                else
                {
                    // Write to logger
                    string loggerMsg = $"File upload to server '{df!.serverLink}' " +
                                       $"failed due to missing server credentials";
                    WriteToLogger(loggerMsg, null);
                }
            }
            else
            {
                Console.WriteLine("{0}: the corresponding DataFileEntry does not contain" +
                                  " a private key, upload cancelled", filepath);
            }
        }

        // Get content for the file specified. Checks if a specific revision has been requested, if not gets newest
        // valid revision. If revision content is not in cache, it is fetched from the server through the WikiHandler
        public byte[]? GetContent(string filename)
        {
            WriteToLogger($"Attempting to read file '{filename}'", filename);
            string? revid = null;
            
            var dataFile = GetDataFile(filename, rootKeyring);
            if (dataFile == null) return null;
            
            // Check if any specific revision has been requested
            if (RequestedRevision.ContainsKey((dataFile.pageName, dataFile.serverLink)))
            {
                revid = RequestedRevision[(dataFile.pageName, dataFile.serverLink)];
                
                // Check if content already is in cache
                var cacheResult = AttemptReadFileFromCache(dataFile.pageName, revid);
                if (cacheResult != null) return Convert.FromBase64String(cacheResult);
            }

            // Get valid WikiHandler or return null
            var wikiHandler = GetWikiHandler(dataFile.serverLink);
            if (wikiHandler == null) return null;

            // If no specific revid has been requested, get newest revision id, if any exists
            revid ??= wikiHandler.GetLatestRevisionID(dataFile.pageName);

            // Check if content already is in cache
            if (revid != null)
            {
                var cacheResult = AttemptReadFileFromCache(dataFile.pageName, revid);
                if (cacheResult != null) return Convert.FromBase64String(cacheResult);
            } 
            
            // Download page content from server
            var textBytes = wikiHandler.Download(dataFile, revid);

            // Add plaintext to cache
            if (textBytes != null && revid != null)
            {
                AddEntryToCache(dataFile.pageName, revid, Convert.ToBase64String(textBytes)); 
            }

            return textBytes;
        }

        public void LoginToMediaWiki(string username, string password)
        {
            throw new NotImplementedException();
        }

        // Delegated Keyring functions
        public void AddNewFile(string filename, string filepath)
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            while (true)
            {
                if (PageAlreadyExists(pageTitle, "-1", configManager.DefaultServerLink))
                {
                    WriteToLogger($"Auto generated page title ({pageTitle}) already exists on server. Retrying...");
                    pageTitle = RandomString.GenerateRandomAlphanumericString();
                }
                else
                {
                    break;
                }
            }

            _keyringManager.AddNewFile(filename, filepath, configManager.DefaultServerLink, pageTitle);
        }

        public void AddNewKeyRing(string filename, string filepath)
        {
            _keyringManager.AddNewKeyRing(filename, filepath);
        }

        public void RenameFile(string oldPath, string newPath)
        {
            WriteToLogger($"Renaming '{oldPath}' to '{newPath}'.");
            _keyringManager.Rename(oldPath, newPath);
        }

        public Keyring? ReadKeyRing()
        {
            return _keyringManager.ReadKeyRing();
        }

        public void RemoveFile(string filePath, string filename)
        {
            WriteToLogger($"Removing file '{filename}'", filePath);
            _keyringManager.RemoveFile(filePath, filename);
        }

        public void ExportKeyring()
        {
            // TODO: add export path
            WriteToLogger("Exporting keyring");
            _keyringManager.ExportRootKeyringBasedOnIsChecked();
        }

        public void ImportKeyring(string importPath)
        {
            Console.WriteLine("Manager:- ImportKeyring('{0}')", importPath);
            WriteToLogger($"Importing keyring from '{importPath}'");
            _keyringManager.ImportRootKeyring(importPath);
        }

        public void SaveKeyringToFile()
        {
            _keyringManager.SaveRootKeyring();
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

        public void AddEntryToCache(string pageTitle, string revid, string content)
        {
            cacheManager.AddEntry(pageTitle, revid, content);
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
            cacheManager.CleanCacheDirectory(configManager.CachePreference ?? new CachePreferences());
        }

        // Delegated Crypto functions
        public DataFile? GetDataFile(string filename, Keyring keyring)
        {
            return _keyringManager.GetDataFile(filename, keyring);
        }

        public void SendEmail(string recipientEmail)
        {
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
            var keyringPath = _keyringManager.GetFilePath("Keyring.json");
            var attachment = new Attachment(keyringPath,
                MediaTypeNames.Application.Json);
            mailMessage.Attachments.Add(attachment);
            mailMessage.To.Add(recipientEmail);

            Console.WriteLine(recipientEmail);
            smtpClient.Send(mailMessage);
        }

        // Revoke access to the selected data file. Create new cryptographic keys and the given contacts
        // will receive the new key in their inbox
        public void RevokeAccess(DataFile datafile, ObservableCollection<Contact> contacts)
        {
            WriteToLogger($"Attempting to revoke access to file '{datafile.filename}'");

            var wikiHandler = GetWikiHandler(datafile.serverLink);
            var latestRevision = wikiHandler?.GetLatestRevision(datafile);

            // Create new cryptographic keys for datafile
            if (latestRevision?.revisionID != null && datafile.ownerPrivateKey != null)
            {
                _keyringManager.RevokeAccess(datafile, latestRevision.revisionID);
            }

            // Send new cryptographic keys as a keyring with one datafile containing latest key
            // to selected contacts
            var uploadObject = new Keyring("revocation");
            var datafileCopy = datafile.Copy();
            var latestKey = datafile.keyList.Last();
            datafileCopy.keyList = new List<DataFileKey> {latestKey};
            uploadObject.dataFiles.Add(datafileCopy);
            uploadObject.PrepareForExportRecursively();

            var serializeObject = JSONSerialization.SerializeObject(uploadObject);
            foreach (var contact in contacts)
            {
                UploadToInboxPage(contact.ServerLink, contact.PageTitle, serializeObject, contact.PublicKey);
            }

            // Remove non-selected contacts from data file contact list
            foreach (var dfContact in datafile.contactList.ToList())
            {
                var contact =
                    contactManager.GetContactByPageTitleAndServerLink(dfContact.Item1,
                        dfContact.Item2 ?? datafile.serverLink);
                if (contact == null) continue;
                if (!contacts.Contains(contact))
                {
                    datafile.contactList.Remove(dfContact);
                }
            }
        }

        // write to logger with normal as default priority 
        public void WriteToLogger(string content, string? location = null,
            LoggerEntry.LogPriority priority = LoggerEntry.LogPriority.Normal)
        {
            logger.Add(content, location, priority);
        }

        // Upload input content to given serverLink and pageTitle using the wikiHandler
        private bool UploadToInboxPage(string serverLink, string pageTitle, string content, byte[] publicKey)
        {
            var wikiHandler = GetWikiHandler(serverLink);

            var result = wikiHandler?.UploadToInboxPage(pageTitle, content, publicKey);
            return result == true;
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
            var contact = contactManager.GetContactByPageTitle("InboxPageTest");

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

        public void TestIfPageExists()
        {
            var pageTitle = "fdafadssfd";
            var wikiHandler = GetWikiHandler("http://192.168.1.7/mediawiki/api.php");
            var content = wikiHandler.PageAlreadyExists(pageTitle, "-1");
            if (content)
            {
                Console.WriteLine("Page exists with pageTitle: " + pageTitle);
            }
            else
            {
                Console.WriteLine("Page does not exist with pageTitle: " + pageTitle);
            }
        }

        // create new keyring containing all data files selected by user in GUI
        // send new Keyring to the selected contacts inbox page
        public void ShareSelectedKeyring(List<Contact> contacts)
        {
            Console.WriteLine(contacts.Count);
            WriteToLogger("Sharing specified parts of keyring");

            // create new keyring with all selected folder and files
            var keyringEntry = _keyringManager.CreateRootKeyringBasedOnIsChecked();
            
            // get the list of all the data files in the new keyring
            var dataFileList = keyringEntry.GetAllAndDescendantDataFileEntries();

            // for each contact create a new list with data files not previously received
            foreach (var contact in contacts)
            {
                var newDataFiles = new List<DataFile>();

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

                if (newDataFiles.Count == 0)
                {
                    continue;
                }

                // Create new keyring containing copies of the datafiles to be shared
                var intermediateKeyringEntry = new Keyring(keyringEntry.name);
                var keyringEntryToExport = new Keyring(intermediateKeyringEntry.name);

                intermediateKeyringEntry.dataFiles.AddRange(newDataFiles);
                intermediateKeyringEntry.AddCopiesToOtherKeyringRecursively(keyringEntryToExport);
                keyringEntryToExport.PrepareForExportRecursively();

                var keyringEntryString = JSONSerialization.SerializeObject(keyringEntryToExport);

                var loggerMsg = $"Sharing {newDataFiles.Count} new files with contact '{contact.Nickname}'.";
                WriteToLogger(loggerMsg);

                var httpResponse = UploadToInboxPage(contact.ServerLink, contact.PageTitle,
                    keyringEntryString, contact.PublicKey);

                // Write result to logger
                WriteToLogger(
                    httpResponse
                        ? $"Upload to inbox page belonging to contact '{contact.Nickname}' complete."
                        : $"Upload to inbox page belonging to contact '{contact.Nickname}' failed.",
                    null, LoggerEntry.LogPriority.Low);
            }
        }


        // Show popup window alerting the user about information. User can click confirm or cancel to exit
        public static MessageBox.Result ShowMessageBox(string title, string content,
            MessageBox.Buttons buttons = MessageBox.Buttons.OkCancel)
        {
            // Invoke UI thread with highest priority
            var output = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var result = MessageBox.Result.No;
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                    desktop)
                {
                    result = await MessageBox.ShowMessageBox(content, title,
                        buttons);
                }

                return result;
            }, DispatcherPriority.MaxValue).Result;

            return output;
        }

        // Show popup window where the user can enter credentials and click confirm or cancel to exit
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

        // Reset queue in TCPListener (set false that last operation was read/write)
        // If the input revision id is null then remove key with given pageName and serverLink
        // otherwise update revision id of key
        public void UpdateRequestedRevision(string pageName, string serverLink, string? revid)
        {
            tcpListener.ResetQueue();

            if (revid == null)
            {
                RequestedRevision.Remove((pageName, serverLink));
                return;
            }
            
            RequestedRevision[(pageName, serverLink)] = revid;
        }
        
        // If the RequestedRevision dictionary contains key with given pageName and serverLink 
        // then return true, otherwise return false
        public bool RequestedRevisionContains(string pageName, string serverLink)
        {
            return RequestedRevision.ContainsKey((pageName, serverLink));
        }

        // If the RequestedRevision dictionary contains key with given pageName and serverLink
        // then return value, otherwise return null
        public string? GetRequestedRevision(string pageName, string serverLink)
        {
            if (!RequestedRevisionContains(pageName, serverLink))
            {
                return null;
            }
            
            var output = RequestedRevision[(pageName, serverLink)];

            return output;
        }
    }
}