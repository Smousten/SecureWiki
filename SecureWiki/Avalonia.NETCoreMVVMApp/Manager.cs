using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
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
        // public ContactManager contactManager;

        public MountedDirMirror mountedDirMirror;
        public Logger logger;
        public MasterKeyring MasterKeyring;
        private Dictionary<(string, string), string> RequestedRevision = new();
        public SymmetricReference symRefToMasterKeyring;

        private readonly string _smtpClientEmail = "SecureWikiMails@gmail.com";
        private readonly string _smtpClientPassword = "SecureWiki";

        public delegate void PrintTest(string input);

        public bool setupFinished = false;

        public PrintTest printTest;

        public Manager(Thread createrThread, MasterKeyring rk, Logger logger, MountedDirMirror mountedDirMirror)
        {
            GUIThread = createrThread;
            printTest = PrintTestMethod;
            MasterKeyring = rk;
            this.logger = logger;
            this.mountedDirMirror = mountedDirMirror;
        }

        public void Run()
        {
            
            InitializeConfigManager();
            InitializeWikiHandlers();

            // localhostWikiHandler = new WikiHandler("new_mysql_user",
            //     "THISpasswordSHOULDbeCHANGED", httpClient, this, "http://localhost/mediawiki/api.php");
            // wikiHandlers.Add("http://localhost/mediawiki/api.php", localhostWikiHandler);

            _keyringManager = new KeyringManager(MasterKeyring, this);
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            // _keyringManager.InitKeyring();
            InitializeCacheManager();
            // InitializeContactManager();

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

            InitializeSymRefMasterKeyring();

            var wh = GetWikiHandler(symRefToMasterKeyring.serverLink);
            var newRootKR = wh?.DownloadMasterKeyring(symRefToMasterKeyring);

            if (newRootKR == null)
            {
                Console.WriteLine("root keyring from server is null");
                symRefToMasterKeyring.targetAccessFile.AccessFileReference.KeyringTarget = MasterKeyring;
            }
            else
            {
                Console.WriteLine("root keyring from server is not null");
                newRootKR.name = "root from server";
                // MasterKeyring.CopyFromOtherKeyring(newRootKR);
                // MasterKeyring = newRootKR;
                MasterKeyring.CopyFromOtherKeyringNonRecursively(newRootKR);
                symRefToMasterKeyring.targetAccessFile.AccessFileReference.KeyringTarget = MasterKeyring;
                wh!.DownloadKeyringsRecursion(MasterKeyring, MasterKeyring);
            }

            PopulateMountedDirMirror(MasterKeyring);
            mountedDirMirror.CreateFileStructureRecursion(GetRootDir(""));
            // mountedDirMirror.PrintInfo();

            // var res = ShowMessageBox("some very loooooooooooooooooooooooooong title", " and some very loooooooooooooooooooooooooong title", MessageBox.Buttons.YesNoCancel);
            // Console.WriteLine(res.ToString());
            UpdateFromInboxes(wh);
            

            setupFinished = true;
            Console.WriteLine("setupFinished = true;");
        }

        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" +
                              Thread.CurrentThread.Name);
        }

        public string GetSymRefMasterKeyringFilePath()
        {
            const string filename = "SymRefMasterKeyring.json";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../"));
            var path = Path.Combine(projectDir, filename);

            return path;
        }

        public void InitializeSymRefMasterKeyring()
        {
            Console.WriteLine("InitializeSymRefMasterKeyring entered");
            var path = GetSymRefMasterKeyringFilePath();

            if (File.Exists(path))
            {
                symRefToMasterKeyring =
                    (SymmetricReference) JSONSerialization.ReadFileAndDeserialize(path, typeof(SymmetricReference));
                if (symRefToMasterKeyring == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("InitializeSymRefMasterKeyring:- symRefToMasterKeyring null");
                    return;
                }
                else
                {
                    Console.WriteLine("InitializeSymRefMasterKeyring:- symRefToMasterKeyring parsed successfully");
                }

                // Download target Access File
                var wh = GetWikiHandler(symRefToMasterKeyring.serverLink);
                var af = wh?.DownloadAccessFile(symRefToMasterKeyring);

                if (af == null)
                {
                    Console.WriteLine("InitializeSymRefMasterKeyring:- af null");
                    return;
                }

                // Create and connect references
                // var afr = new AccessFileReference(af.pageName, af.serverLink, af, PageType.Keyring);
                // af.AccessFileReference = afr;
                symRefToMasterKeyring.targetAccessFile = af;
            }
            else
            {
                // Create new references
                Console.WriteLine();
                Console.WriteLine("InitializeSymRefMasterKeyring:- creating new master sym ref");
                _keyringManager.CreateAccessFileAndReferences(GetFreshPageName(), GetFreshPageName(),
                    configManager.DefaultServerLink, PageType.Keyring, out SymmetricReference symmetricReference,
                    out AccessFile accessFile);
                symRefToMasterKeyring = symmetricReference;
            }
        }

        public void SaveSymRefMasterKeyringToFile()
        {
            var path = GetSymRefMasterKeyringFilePath();

            JSONSerialization.SerializeAndWriteFile(path, symRefToMasterKeyring);

            Console.WriteLine("Upload access file to: " + symRefToMasterKeyring.targetPageName);
            Console.WriteLine("Upload keyring file to: " + symRefToMasterKeyring.accessFileTargetPageName);

            var wikihandler = GetWikiHandler(symRefToMasterKeyring.serverLink);
            var res1 = wikihandler?.UploadAccessFile(symRefToMasterKeyring.targetAccessFile);
            var res2 = wikihandler?.UploadKeyring(symRefToMasterKeyring.targetAccessFile, MasterKeyring);

            Console.WriteLine("res1, res2: {0}, {1}", res1, res2);
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

        public void SetCacheSettingSingleFile(string pageName, CachePreferences.CacheSetting? setting)
        {
            configManager!.CachePreference.SetPreference(pageName, setting);
        }

        public CachePreferences.CacheSetting GetCacheSettingGeneral()
        {
            return configManager!.CachePreference.GeneralSetting;
        }

        public CachePreferences.CacheSetting? GetCacheSettingSingleFile(string pageName)
        {
            return configManager!.CachePreference.GetSetting(pageName);
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

        // public void InitializeContactManager()
        // {
        //     var contactsPath = GetContactsFilePath();
        //     var ownContactsPath = GetOwnContactsFilePath();
        //
        //     contactManager = new ContactManager(this);
        //
        //     // Read existing contacts from their respective files
        //     if (File.Exists(contactsPath))
        //     {
        //         contactManager.Contacts =
        //             (List<Contact>) JSONSerialization.ReadFileAndDeserialize(contactsPath, typeof(List<Contact>));
        //     }
        //
        //     if (File.Exists(ownContactsPath))
        //     {
        //         contactManager.OwnContacts =
        //             (List<OwnContact>) JSONSerialization.ReadFileAndDeserialize(ownContactsPath,
        //                 typeof(List<OwnContact>));
        //     }
        // }
        //
        // public void SaveContactManagerToFile()
        // {
        //     var contactsPath = GetContactsFilePath();
        //     var ownContactsPath = GetOwnContactsFilePath();
        //
        //     JSONSerialization.SerializeAndWriteFile(contactsPath, contactManager.Contacts);
        //     JSONSerialization.SerializeAndWriteFile(ownContactsPath, contactManager.OwnContacts);
        // }

        // Contacts in revoke popup list should only show contacts in access file
        // public void GetFileContacts(ObservableCollection<Contact> revokeContacts, AccessFile accessFile)
        // {
        //     revokeContacts.Clear();
        //     foreach (var (pageTitle, serverLink) in accessFile.contactList)
        //     {
        //         var contact =
        //             contactManager.GetContactByPageTitleAndServerLink(pageTitle, serverLink ?? accessFile.serverLink);
        //         if (contact != null) revokeContacts.Add(contact);
        //     }
        // }

        // Contacts in revoke popup list should only show contacts in access file
        public void GetFileContacts(ObservableCollection<InboxReference> revokeContacts, AccessFile accessFile)
        {
            revokeContacts.Clear();
            foreach (var inboxRef in accessFile.inboxReferences)
            {
                revokeContacts.Add(inboxRef);
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

            MasterKeyring.Contacts.AddRange(newContacts);
        }

        //
        // public void GenerateOwnContact(string serverLink, string nickname)
        // {
        //     var pageTitle = RandomString.GenerateRandomAlphanumericString();
        //     while (true)
        //     {
        //         if (PageAlreadyExists(pageTitle, "-1", serverLink))
        //         {
        //             WriteToLogger($"Auto generated page title ({pageTitle}) already exists on server. Retrying...");
        //             pageTitle = RandomString.GenerateRandomAlphanumericString();
        //         }
        //         else
        //         {
        //             break;
        //         }
        //     }
        //
        //     // var url = "http://" + serverLink + "/mediawiki/api.php";
        //     OwnContact newContact = new(serverLink, pageTitle, nickname);
        //     contactManager.AddOwnContact(newContact);
        // }
        //
        public void GetAllContacts(ObservableCollection<Contact> contactsOwn,
            ObservableCollection<Contact> contactsOther)
        {
            contactsOwn.Clear();
            contactsOther.Clear();
            if (MasterKeyring.OwnContacts.Count > 0)
            {
                contactsOwn.AddRange(MasterKeyring.OwnContacts);
            }

            if (MasterKeyring.Contacts.Count > 0)
            {
                contactsOther.AddRange(MasterKeyring.Contacts);
            }
        }
        
        public void GetAllContacts(ObservableCollection<Contact> contacts)
        {
            contacts.Clear();
            
            if (MasterKeyring.OwnContacts.Count > 0)
            {
                contacts.AddRange(MasterKeyring.OwnContacts);
            }

            if (MasterKeyring.Contacts.Count > 0)
            {
                contacts.AddRange(MasterKeyring.Contacts);
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
        //
        // public void GetOtherContacts(ObservableCollection<Contact> contacts)
        // {
        //     contacts.Clear();
        //     if (contactManager.Contacts.Count > 0)
        //     {
        //         contacts.AddRange(contactManager.Contacts);
        //     }
        // }

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

        // Check inboxes of own contacts to update keyrings on server
        private void UpdateFromInboxes(IServerInteraction? wikiHandler)
        {
            Dictionary<Contact, List<AccessFile>> incomingAccessFiles = new();
            // Download from inbox - iterate through all new revisions for each contact access file
            var inboxContent = wikiHandler?.DownloadFromInboxPages();
            if (inboxContent != null)
            {
                foreach (var contactInbox in inboxContent.Keys)
                {
                    foreach (var revision in inboxContent[contactInbox])
                    {
                        if (JSONSerialization.DeserializeObject(revision, typeof(List<AccessFile>)) is List<AccessFile>
                            deserializeObject)
                        {
                            incomingAccessFiles.Add(contactInbox, deserializeObject);
                        }
                    }
                }
            }

            foreach (var contact in incomingAccessFiles.Keys)
            {
                var keyring = contact.InboxReference.KeyringTarget;
                var count = 0;
                
                // Create new access file page and reference for each received
                foreach (var accessFile in incomingAccessFiles[contact])
                {
                    var pageNameAccessFile = GetFreshPageName();

                    // Create symmetric reference to access file
                    var symmetricReference = new SymmetricReference(pageNameAccessFile,
                        configManager.DefaultServerLink, accessFile.AccessFileReference.type, 
                        accessFile.AccessFileReference.targetPageName, accessFile);
                    accessFile.SymmetricReferenceToSelf = symmetricReference;
                    keyring.AddSymmetricReference(symmetricReference);
                    
                    // Check if received access file already exists
                    var existingAccessFilePath = MasterKeyring.GetMountedDirMapping(accessFile.AccessFileReference.targetPageName);
                    if (existingAccessFilePath != null)
                    {
                        WriteToLogger("Received access file to an already existing file, merging access files.");
                        var existingMDFile = mountedDirMirror.GetMDFile(existingAccessFilePath);
                        var existingAccessFile = existingMDFile?.symmetricReference.targetAccessFile;

                        if (existingAccessFile != null)
                        {
                            // existingAccessFile.MergeWithOtherAccessFileEntry(accessFile);
                            // var uploadResAFExisting = wikiHandler?.UploadAccessFile(existingAccessFile);
                            // if (uploadResAFExisting == false)
                            // {
                            //     WriteToLogger("Access File could not be uploaded, aborting.");
                            // }
                            
                            
                            // refactor later. 
                            var uploadResAFt = wikiHandler?.UploadAccessFile(accessFile);

                            if (uploadResAFt == false)
                            {
                                WriteToLogger("Access File could not be uploaded, aborting.");
                                continue;
                            }

                            continue;
                        }
                    }
                    
                    // Create new entry in md mirror
                    var filepath = keyring.name + "_inbox" + '/' + accessFile.filename + '_' + count;
                    count++;
                    var mdFile = mountedDirMirror.CreateFile(filepath, symmetricReference);
                    if (mdFile == null)
                    {
                        WriteToLogger("File could not be added to MDMirror, upload failed");
                        continue;
                    }
                    
                    var uploadResAF = wikiHandler?.UploadAccessFile(accessFile);

                    if (uploadResAF == false)
                    {
                        WriteToLogger("Access File could not be uploaded, aborting.");
                        continue;
                    }
                    
                    MasterKeyring.SetMountedDirMapping(accessFile.AccessFileReference.targetPageName, filepath);
                }

                var accessFileToKeyring = keyring.accessFileReferenceToSelf.AccessFileParent;
                if (accessFileToKeyring == null) continue;
                if (accessFileToKeyring.HasBeenChanged)
                {
                    wikiHandler?.UploadAccessFile(accessFileToKeyring);
                }

                var uploadResKR = wikiHandler?.UploadKeyring(
                    accessFileToKeyring, keyring);
                if (uploadResKR == false)
                {
                    WriteToLogger($"Keyring '{keyring.name}' could not be uploaded.");
                }
            }

            // incomingAccessFiles = incomingAccessFiles.OrderBy(e => e.AccessFileReference.targetPageName).ToList();
            // List<AccessFile> intermediateList = new();
            //
            // // Merge updates to same access files and remove duplicates 
            // int i = 0;
            // while (i < incomingAccessFiles.Count)
            // {
            //     int cnt = 1;
            //
            //     while (i + cnt < incomingAccessFiles.Count &&
            //            incomingAccessFiles[i].AccessFileReference.targetPageName
            //                .SequenceEqual(incomingAccessFiles[i + cnt].AccessFileReference.targetPageName))
            //     {
            //         if (incomingAccessFiles[i].AccessFileReference.serverLink
            //             .Equals(incomingAccessFiles[i + cnt].AccessFileReference.serverLink))
            //         {
            //             incomingAccessFiles[i].MergeWithOtherAccessFileEntry(incomingAccessFiles[i + cnt]);
            //         }
            //
            //         cnt++;
            //     }
            //
            //     intermediateList.Add(incomingAccessFiles[i]);
            //     i += cnt;
            // }
            //
            // // Get all existing access files in a list
            // List<AccessFile> newAccessFiles = new();
            // var existingAccessFiles = MasterKeyring.GetAllAndDescendantAccessFileEntries();
            // existingAccessFiles =
            //     existingAccessFiles.OrderBy(entry => entry.AccessFileReference.targetPageName).ToList();
            //
            // // For each incoming access file from inbox, merge with existing access file or add to list of new files
            // foreach (var accessFile in intermediateList)
            // {
            //     var existingAf = existingAccessFiles.Find(e =>
            //         e.AccessFileReference.targetPageName.Equals(accessFile.AccessFileReference.targetPageName));
            //     if (existingAf != null)
            //     {
            //         existingAf.MergeWithOtherAccessFileEntry(accessFile);
            //     }
            //     else
            //     {
            //         newAccessFiles.Add(accessFile);
            //     }
            // }
            //
            // // Add new access files to folder the same import folder
            // if (newAccessFiles.Count > 0)
            // {
            //     if (!MasterKeyring.keyrings.Any(e => e.name.Equals("ImportedFromContacts")))
            //     {
            //         MasterKeyring.AddKeyring(new Keyring("ImportedFromContacts"));
            //     }
            //
            //     var importFolder = MasterKeyring.keyrings.First(e => e.name.Equals("ImportedFromContacts"));
            //     importFolder.AddRangeAccessFile(newAccessFiles);
            // }
            //
            // // _keyringManager.SortAndUpdatePeripherals();
        }

        public void ForceUpdateFromAllInboxPages()
        {
            var serverLinks = MasterKeyring.GetAllUniqueServerLinksFromOwnContacts();

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

        public MediaWikiObject.PageQuery.AllRevisions? GetAllRevisions(string pageName, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            return wikiHandler?.GetAllRevisions(pageName);
        }

        public async void UpdateAllRevisionsAsync(string pageName, string url,
            ObservableCollection<Revision> revisions)
        {
            var wikiHandler = GetWikiHandler(url);
            var allRev = wikiHandler?.GetAllRevisions(pageName);

            revisions.Clear();

            if (allRev?.revisionList != null)
            {
                revisions.AddRange(allRev.revisionList);
            }
        }

        public string? GetPageContent(string pageName, string revID, string url)
        {
            // Write to logger
            WriteToLogger($"Attempting to read file from page title '{pageName}', revision {revID} on server '{url}'");

            var wikiHandler = GetWikiHandler(url);
            var output = wikiHandler?.GetPageContent(pageName, revID);

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

        private bool PageAlreadyExists(string pageName, string revID, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            return wikiHandler != null && wikiHandler.PageAlreadyExists(pageName, revID);
        }

        public bool UndoRevisionsByID(string pageName, string startID, string endID, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            wikiHandler?.UndoRevisionsByID(pageName, startID, endID);
            return true;
        }

        public void DeleteRevisionsByID(string pageName, string IDs, string url)
        {
            var wikiHandler = GetWikiHandler(url);
            wikiHandler?.DeleteRevisionsByID(pageName, IDs);
        }

        public void UploadNewVersion(string filename, string filepath)
        {
            string pageName;
            var mdFile = mountedDirMirror.GetMDFile(filepath);
            var symmRef = mdFile?.symmetricReference;

            if (symmRef == null)
            {
                // Write to logger
                string loggerMsg = $"File upload to server failed. Could not find Symmetric Reference";
                WriteToLogger(loggerMsg, filepath);
                return;
            }

            var whAF = GetWikiHandler(symmRef.serverLink);
            if (whAF == null) return;

            if (mdFile!.TargetType == MDFile.Type.AccessFile)
            {
                var fileContent = File.ReadAllBytes(GetRootDir(filepath));
                whAF.Upload(symmRef, fileContent);
            }
            else
            {
                // Get Access File
                var af = symmRef.targetAccessFile ?? whAF.DownloadAccessFile(symmRef);
                if (af == null) return;


                var keyList = af.keyList.Last();
                if (keyList.PrivateKey != null)
                {
                    var whAFTarget = GetWikiHandler(af.AccessFileReference.serverLink);

                    if (whAFTarget != null)
                    {
                        // Write to logger
                        string loggerMsg = "Attempting to upload file to server '" + af.AccessFileReference.serverLink +
                                           "'";
                        WriteToLogger(loggerMsg, filepath);

                        var fileContent = File.ReadAllBytes(GetRootDir(filepath));
                        whAFTarget.Upload(af!, fileContent);
                    }
                    else
                    {
                        // Write to logger
                        string loggerMsg = $"File upload to server '{af!.AccessFileReference.serverLink}' " +
                                           $"failed due to missing server credentials";
                        WriteToLogger(loggerMsg, null);
                    }
                }
                else
                {
                    Console.WriteLine("{0}: the corresponding AccessFileEntry does not contain" +
                                      " a private key, upload cancelled", filepath);
                }
            }
        }

        public AccessFile? GetAccessFile(string filepath)
        {
            var symmRef = mountedDirMirror.GetMDFile(filepath)?.symmetricReference;

            if (symmRef == null) return null;

            if (symmRef.targetAccessFile != null)
            {
                return symmRef.targetAccessFile;
            }

            // Attempt to download access file from server
            var wh = GetWikiHandler(symmRef.serverLink);
            var af = wh?.DownloadAccessFile(symmRef);

            return af;
        }

        // Get content for the file specified. Checks if a specific revision has been requested, if not gets newest
        // valid revision. If revision content is not in cache, it is fetched from the server through the WikiHandler
        public byte[]? GetContent(string filepath)
        {
            WriteToLogger($"Attempting to read file '{filepath}'", filepath);
            string? revid = null;
            string pageName;

            var mdFile = mountedDirMirror.GetMDFile(filepath);
            var symmRef = mdFile?.symmetricReference;

            if (symmRef == null) return null;
            if (mdFile!.TargetType == MDFile.Type.AccessFile)
            {
                pageName = symmRef.targetPageName;
            }
            else
            {
                pageName = symmRef.accessFileTargetPageName;
            }

            // Check if any specific revision has been requested
            if (RequestedRevision.ContainsKey((pageName, symmRef.serverLink)))
            {
                revid = RequestedRevision[(pageName, symmRef.serverLink)];

                // Check if content already is in cache
                var cacheResult = AttemptReadFileFromCache(pageName, revid);
                if (cacheResult != null) return Convert.FromBase64String(cacheResult);
            }

            // Get valid WikiHandler or return null
            var wikiHandler = GetWikiHandler(symmRef.serverLink);
            if (wikiHandler == null) return null;

            // If no specific revid has been requested, get newest revision id, if any exists
            revid ??= wikiHandler.GetLatestRevisionID(pageName);

            // Check if content already is in cache
            if (revid != null)
            {
                var cacheResult = AttemptReadFileFromCache(pageName, revid);
                if (cacheResult != null) return Convert.FromBase64String(cacheResult);
            }

            byte[]? textBytes;

            if (mdFile!.TargetType == MDFile.Type.AccessFile)
            {
                textBytes = wikiHandler.Download(symmRef, revid);
            }
            else
            {
                // Get Access File
                var accessFile = symmRef.targetAccessFile ?? wikiHandler.DownloadAccessFile(symmRef);
                if (accessFile == null) return null;

                // Download page content from server
                textBytes = wikiHandler.Download(accessFile, revid);
            }

            // Add plaintext to cache
            if (textBytes != null && revid != null)
            {
                AddEntryToCache(pageName, revid, Convert.ToBase64String(textBytes));
            }

            return textBytes;
        }

        public void UploadMasterKeyring()
        {
            if (!PageAlreadyExists(_keyringManager.masterKey.pageName, "-1",
                configManager.DefaultServerLink))
            {
                var wikiHandler = GetWikiHandler(configManager.DefaultServerLink);
                wikiHandler?.UploadMasterKeyring(_keyringManager.masterKey.symmKey,
                    _keyringManager.masterKey.pageName,
                    MasterKeyring);
            }
        }

        // Delegated Keyring functions
        public void AddNewFile(string filepath)
        {
            var pageNameFile = GetFreshPageName();
            var pageNameAccessFile = GetFreshPageName();

            _keyringManager.CreateAccessFileAndReferences(pageNameFile, pageNameAccessFile,
                configManager.DefaultServerLink, PageType.GenericFile,
                out SymmetricReference symmetricReference,
                out AccessFile accessFile);

            // Add symmetric reference to newEntries keyring and upload
            AddToDefaultKeyring(symmetricReference);
            
            // Create new entry in md mirror
            var mdFile = mountedDirMirror.CreateFile(filepath, symmetricReference);
            if (mdFile == null)
            {
                WriteToLogger("File could not be added to MDMirror, upload failed");
                return;
            }

            // Upload new files to server
            var wikiHandler = GetWikiHandler(accessFile!.AccessFileReference.serverLink);
            var uploadResAF = wikiHandler?.UploadAccessFile(accessFile);

            if (uploadResAF == false)
            {
                WriteToLogger("Access File could not be uploaded, aborting.");
                return;
            }

            // var fileContent = File.ReadAllBytes(GetRootDir(filepath));
            var fileContent = Encoding.ASCII.GetBytes("This is the first revision");
            var uploadResFile = wikiHandler?.Upload(accessFile, fileContent);

            if (uploadResFile == false)
            {
                WriteToLogger("File could not be uploaded", filepath);
                return;
            }

            MasterKeyring.SetMountedDirMapping(accessFile.AccessFileReference.targetPageName, filepath);
        }

        private void AddToDefaultKeyring(SymmetricReference symmetricReference)
        {
            var defaultKeyring = _keyringManager.AddToDefaultKeyring(symmetricReference);
            var accessFileToDefaultKeyring = defaultKeyring.accessFileReferenceToSelf.AccessFileParent;

            // Upload updated keyring
            var wikiHandler = GetWikiHandler(configManager.DefaultServerLink);

            if (accessFileToDefaultKeyring == null) return;
            if (accessFileToDefaultKeyring.HasBeenChanged)
            {
                wikiHandler?.UploadAccessFile(accessFileToDefaultKeyring);
            }

            var uploadResKR = wikiHandler?.UploadKeyring(
                accessFileToDefaultKeyring, defaultKeyring);
            if (uploadResKR == false)
            {
                WriteToLogger($"Keyring '{defaultKeyring.name}' could not be uploaded.");
            }
        }

        public string? GetFreshPageName(string? serverLink = null)
        {
            serverLink ??= configManager.DefaultServerLink;
            var wikiHandler = GetWikiHandler(serverLink);
            string? pageName = null;

            // Try for 5 seconds to get fresh page name
            var success = SpinWait.SpinUntil(() =>
            {
                pageName = TryFreshPageName(serverLink, wikiHandler);
                return pageName != null;
            }, TimeSpan.FromSeconds(5));
            Console.WriteLine("GetFreshPageName:- pageName='{0}'", pageName);
            return success ? pageName : null;
        }

        private string? TryFreshPageName(string serverLink, IServerInteraction? wikiHandler)
        {
            var tmp = RandomString.GenerateRandomAlphanumericString();
            tmp = char.ToUpper(tmp[0]) + tmp.Substring(1);
            if (!PageAlreadyExists(tmp, "-1", serverLink))
            {
                // Upload placeholder text to page to reserve it
                wikiHandler?.Upload(tmp, "placeholder");
                return tmp;
            }

            return null;
        }

        public void AddNewFolder(string filename, string filepath)
        {
            // Create new entry in md mirror
            var mdFolder = mountedDirMirror.AddFolder(filepath);
            if (mdFolder == null)
            {
                WriteToLogger("Keyring could not be added to MDMirror, upload failed");
            }
        }

        public void AddNewKeyring(string filename)
        {
            var keyring = _keyringManager.CreateNewKeyring(filename, configManager.DefaultServerLink);
            // Add symmetric reference to newEntries keyring and upload
            AddToDefaultKeyring(keyring.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf);
            var accessFile = keyring?.accessFileReferenceToSelf.AccessFileParent;
            var symmetricReference = accessFile?.SymmetricReferenceToSelf;

            // If construction failed
            if (keyring == null || accessFile == null || symmetricReference == null)
            {
                var loggerMsg = $"Creating new Keyring of name '{filename}' failed.";
                WriteToLogger(loggerMsg);
                return;
            }

            // Upload new files to server
            var wikiHandler = GetWikiHandler(accessFile!.AccessFileReference.serverLink);
            var uploadResAF = wikiHandler?.UploadAccessFile(accessFile);
            if (uploadResAF == false)
            {
                WriteToLogger($"Access File for Keyring '{keyring.name}' could not be uploaded.");
            }

            // Upload new keyring to server
            var uploadResKR = wikiHandler?.UploadKeyring(accessFile, keyring);
            if (uploadResKR == false)
            {
                WriteToLogger($"Keyring '{keyring.name}' could not be uploaded.");
            }

            // Add keyring contact to masterkeyring
            MasterKeyring.OwnContacts.Add(new OwnContact(keyring.name, keyring.InboxReferenceToSelf));
        }

        private SymmetricReference? GetKeyringReference(string name, Keyring keyring)
        {
            foreach (var symmRef in keyring.SymmetricReferences)
            {
                if (symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget != null
                    && symmRef.type == PageType.Keyring &&
                    symmRef.targetAccessFile.AccessFileReference.KeyringTarget.name.Equals(name))
                {
                    return symmRef;
                }

                var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                if (kr == null)
                {
                    continue;
                }

                var res = GetKeyringReference(name, kr);
                if (res != null)
                {
                    return res;
                }
            }

            return null;
        }

        public void RenameFile(string oldPath, string newPath)
        {
            WriteToLogger($"Renaming '{oldPath}' to '{newPath}'.");
            // _keyringManager.Rename(oldPath, newPath);
            var mdFile = mountedDirMirror.Move(oldPath, newPath);
            if (mdFile == null)
            {
                var mdFolder = mountedDirMirror.MoveFolder(oldPath, newPath);
                if (mdFolder != null)
                {
                    // Update mapping for all nested files
                    MasterKeyring.SetMountedDirMappingNested(mdFolder, newPath);
                }
                else
                {
                    Console.WriteLine($"Renaming '{oldPath}' to '{newPath}' failed, creating new file instead");
                    AddNewFile(newPath);
                }
            }
            else
            {
                MasterKeyring.SetMountedDirMapping(mdFile.symmetricReference.accessFileTargetPageName, newPath);
            }
        }

        // public Keyring? ReadKeyRing()
        // {
        //     return _keyringManager.ReadKeyRing();
        // }
        //
        // public void RemoveFile(string filePath, string filename)
        // {
        //     WriteToLogger($"Removing file '{filename}'", filePath);
        //     _keyringManager.RemoveFile(filePath, filename);
        // }
        //
        // public void ExportKeyring()
        // {
        //     // TODO: add export path
        //     WriteToLogger("Exporting keyring");
        //     _keyringManager.ExportRootKeyringBasedOnIsChecked();
        // }
        //
        // public void ImportKeyring(string importPath)
        // {
        //     Console.WriteLine("Manager:- ImportKeyring('{0}')", importPath);
        //     WriteToLogger($"Importing keyring from '{importPath}'");
        //     _keyringManager.ImportRootKeyring(importPath);
        // }
        //
        // public void SaveKeyringToFile()
        // {
        //     _keyringManager.SaveRootKeyring();
        // }

        public string? AttemptReadFileFromCache(string pageName, string revid)
        {
            string? cacheResult;

            if (revid.Equals("-1"))
            {
                Console.WriteLine("AttemptReadFileFromCache:- revid==-1");
                cacheResult = cacheManager.GetFilePath(pageName);
            }
            else
            {
                cacheResult = cacheManager.GetFilePath(pageName, revid);
            }

            if (cacheResult == null || File.Exists(cacheResult) == false)
            {
                return null;
            }

            return File.ReadAllText(cacheResult);
        }

        public void AddEntryToCache(string pageName, string revid, string content)
        {
            cacheManager.AddEntry(pageName, revid, content);
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
        // public AccessFile? GetAccessFile(string filename, Keyring keyring)
        // {
        //     return _keyringManager.GetAccessFile(filename, keyring);
        // }


        // // Revoke access to the selected access file. Create new cryptographic keys and the given contacts
        // // will receive the new key in their inbox
        // public void RevokeAccess(AccessFile accessFile, ObservableCollection<Contact> contacts)
        // {
        //     WriteToLogger($"Attempting to revoke access to file '{accessFile.filename}'");
        //
        //     var wikiHandler = GetWikiHandler(accessFile.serverLink);
        //     var latestRevision = wikiHandler?.GetLatestRevision(accessFile);
        //
        //     // Create new cryptographic keys for access file
        //     if (latestRevision?.revisionID != null && accessFile.ownerPrivateKey != null)
        //     {
        //         _keyringManager.RevokeAccess(accessFile, latestRevision.revisionID);
        //     }
        //
        //     // Send new cryptographic keys as a keyring with one access file containing latest key
        //     // to selected contacts
        //     var uploadObject = new Keyring("revocation");
        //     var accessFileCopy = accessFile.Copy();
        //     var latestKey = accessFile.keyList.Last();
        //     accessFileCopy.keyList = new List<AccessFileKey> {latestKey};
        //     uploadObject.accessFiles.Add(accessFileCopy);
        //     uploadObject.PrepareForExportRecursively();
        //
        //     var serializeObject = JSONSerialization.SerializeObject(uploadObject);
        //     foreach (var contact in contacts)
        //     {
        //         UploadToInboxPage(contact.ServerLink, contact.PageName, serializeObject, contact.PublicKey);
        //     }
        //
        //     // Remove non-selected contacts from access file contact list
        //     foreach (var afContact in accessFile.contactList.ToList())
        //     {
        //         var contact =
        //             contactManager.GetContactByPageTitleAndServerLink(afContact.Item1,
        //                 afContact.Item2 ?? accessFile.serverLink);
        //         if (contact == null) continue;
        //         if (!contacts.Contains(contact))
        //         {
        //             accessFile.contactList.Remove(afContact);
        //         }
        //     }
        // }

        public void RevokeAccess(AccessFile accessFile, List<InboxReference> inboxRefs)
        {
            WriteToLogger($"Attempting to revoke access to file '{accessFile.filename}'");

            var wikiHandler = GetWikiHandler(accessFile.AccessFileReference.serverLink);
            var latestRevision = wikiHandler?.GetLatestRevision(accessFile.AccessFileReference.targetPageName);

            // Create new cryptographic keys for access file
            if (latestRevision?.revisionID != null && accessFile.ownerPrivateKey != null)
            {
                _keyringManager.RevokeAccess(accessFile, latestRevision.revisionID);
            }

            // Send new cryptographic keys as a keyring with one access file containing latest key
            // to selected inbox pages - TODO: private key should depend on inbox reference access level
            var uploadObject = new Keyring("revocation");
            var accessFileCopy = accessFile.Copy();
            var latestKey = accessFile.keyList.Last();
            accessFileCopy.keyList = new List<AccessFileKey> {latestKey};
            uploadObject.accessFiles.Add(accessFileCopy);
            uploadObject.PrepareForExportRecursively();

            var serializeObject = JSONSerialization.SerializeObject(uploadObject);
            foreach (var inboxRef in inboxRefs)
            {
                UploadToInboxPage(inboxRef.serverLink, inboxRef.targetPageName, serializeObject, inboxRef.publicKey);
            }

            // Remove non-selected references from access file reference list
            foreach (var inboxRef in accessFile.inboxReferences)
            {
                if (!inboxRefs.Contains(inboxRef))
                {
                    accessFile.inboxReferences.Remove(inboxRef);
                }
            }

            wikiHandler?.UploadAccessFile(accessFile);
        }

        // write to logger with normal as default priority 
        public void WriteToLogger(string content, string? location = null,
            LoggerEntry.LogPriority priority = LoggerEntry.LogPriority.Normal)
        {
            logger.Add(content, location, priority);
        }

        // Upload input content to given serverLink and pageName using the wikiHandler
        private bool UploadToInboxPage(string serverLink, string pageName, string content, byte[] publicKey)
        {
            var wikiHandler = GetWikiHandler(serverLink);

            var result = wikiHandler?.UploadToInboxPage(pageName, content, publicKey);
            return result == true;
        }
        //
        // public void TestUpload()
        // {
        //     var contact = contactManager.GetOwnContactByNickname("Test");
        //
        //     if (contact == null)
        //     {
        //         Console.WriteLine("contact is null");
        //         return;
        //     }
        //
        //     // var pubKey =
        //     //     "MIIBCgKCAQEAug/PiOEJGPvdFdfyhMZLzp1ELdH1UBNMStxnGAQ3eQRJ0RyzgmSvq9FD9g106oPpz+GxaLjPplhz10bn108IwpjcB4+5XLMhedU0K4bOUHpSwsn+af6nkinU5/3BYN2EsI1hR31GNn0HiR0utJVs/6/CIZ/6RWPd4Z4CbD0f+Og4v3x24a0eYgr/vb02+T0HVG9gOyjomPnLiCj+pqnLb+x1Evpyy2y8SXXR76YpP+CVtgMRmQ4k+6YHU3VLCGTmwDEEvhm6KkjozA3A3RAl2M4BvKTZiHG1SxM79pUJkpFSor2SuRmrAr1S4tCgY9wBhBf0yRBZJa9xxjSVnZkWEwIDAQAB";
        //     // var pubKeyBytes = Convert.FromBase64String(pubKey);
        //
        //     var pubKeyBytes = contact.PublicKey;
        //
        //     var af = MasterKeyring.accessFiles.First();
        //
        //     var afString = JSONSerialization.SerializeObject(af);
        //
        //     Console.WriteLine("afString");
        //     Console.WriteLine(afString);
        //
        //     string content = afString;
        //
        //     UploadToInboxPage(contact.ServerLink, contact.PageName, content, pubKeyBytes);
        // }
        //
        // public void TestDownloadInboxes()
        // {
        //     var wikiHandler = GetWikiHandler("http://192.168.1.7/mediawiki/api.php");
        //
        //     wikiHandler?.DownloadFromInboxPages();
        // }
        //
        // public void TestDownload()
        // {
        //     // var contact = contactManager.GetOwnContactByNickname("Test");
        //     var contact = contactManager.GetContactByPageTitle("InboxPageTest");
        //
        //     if (contact == null)
        //     {
        //         Console.WriteLine("contact is null");
        //         return;
        //     }
        //
        //     Console.WriteLine("contact is not null");
        //
        //     // var pubKey =
        //     //     "MIIBCgKCAQEAug/PiOEJGPvdFdfyhMZLzp1ELdH1UBNMStxnGAQ3eQRJ0RyzgmSvq9FD9g106oPpz+GxaLjPplhz10bn108IwpjcB4+5XLMhedU0K4bOUHpSwsn+af6nkinU5/3BYN2EsI1hR31GNn0HiR0utJVs/6/CIZ/6RWPd4Z4CbD0f+Og4v3x24a0eYgr/vb02+T0HVG9gOyjomPnLiCj+pqnLb+x1Evpyy2y8SXXR76YpP+CVtgMRmQ4k+6YHU3VLCGTmwDEEvhm6KkjozA3A3RAl2M4BvKTZiHG1SxM79pUJkpFSor2SuRmrAr1S4tCgY9wBhBf0yRBZJa9xxjSVnZkWEwIDAQAB";
        //     // var pubKeyBytes = Convert.FromBase64String(pubKey);
        //
        //     var wikihandler = GetWikiHandler(contact.ServerLink);
        //
        //     if (wikihandler == null)
        //     {
        //         Console.WriteLine("wikihandler is null");
        //         return;
        //     }
        //
        //     Console.WriteLine("wikihandler is not null");
        //
        //     var output = wikihandler.DownloadFromInboxPages();
        //
        //     if (output == null)
        //     {
        //         Console.WriteLine("output is null");
        //         return;
        //     }
        //
        //     Console.WriteLine("output is not null");
        //
        //     foreach (var item in output)
        //     {
        //         Console.WriteLine("count: " + item.Count);
        //     }
        // }

        // // create new keyring containing all access files selected by user in GUI
        // // send new Keyring to the selected contacts inbox page
        // public void ShareSelectedKeyring(List<Contact> contacts)
        // {
        //     Console.WriteLine(contacts.Count);
        //     WriteToLogger("Sharing specified parts of keyring");
        //
        //     // create new keyring with all selected folder and files
        //     var keyringEntry = _keyringManager.CreateRootKeyringBasedOnIsChecked();
        //     
        //     // get the list of all the access files in the new keyring
        //     var accessFileList = keyringEntry.GetAllAndDescendantAccessFileEntries();
        //
        //     // for each contact create a new list with access files not previously received
        //     foreach (var contact in contacts)
        //     {
        //         var newAccessFiles = new List<AccessFile>();
        //
        //         foreach (var af in accessFileList)
        //         {
        //             var contactInfo = af.GetContactInfo
        //                 (contact.PageTitle, contact.ServerLink);
        //
        //             if (contactInfo == null)
        //             {
        //                 af.AddContactInfo(contact.PageTitle, contact.ServerLink);
        //                 newAccessFiles.Add(af);
        //             }
        //         }
        //
        //         if (newAccessFiles.Count == 0)
        //         {
        //             continue;
        //         }
        //
        //         // Create new keyring containing copies of the access files to be shared
        //         var intermediateKeyringEntry = new Keyring(keyringEntry.name);
        //         var keyringEntryToExport = new Keyring(intermediateKeyringEntry.name);
        //
        //         intermediateKeyringEntry.accessFiles.AddRange(newAccessFiles);
        //         intermediateKeyringEntry.AddCopiesToOtherKeyringRecursively(keyringEntryToExport);
        //         keyringEntryToExport.PrepareForExportRecursively();
        //
        //         var keyringEntryString = JSONSerialization.SerializeObject(keyringEntryToExport);
        //
        //         var loggerMsg = $"Sharing {newAccessFiles.Count} new files with contact '{contact.Nickname}'.";
        //         WriteToLogger(loggerMsg);
        //
        //         var httpResponse = UploadToInboxPage(contact.ServerLink, contact.PageTitle,
        //             keyringEntryString, contact.PublicKey);
        //
        //         // Write result to logger
        //         WriteToLogger(
        //             httpResponse
        //                 ? $"Upload to inbox page belonging to contact '{contact.Nickname}' complete."
        //                 : $"Upload to inbox page belonging to contact '{contact.Nickname}' failed.",
        //             null, LoggerEntry.LogPriority.Low);
        //     }
        // }


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

        // Return absolute path to fuse root directory
        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/directories/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, filepath);
            return srcDir;
        }

        private void PopulateMountedDirMirror(MasterKeyring rk)
        {
            var symmRefList = rk.GetAllAndDescendantSymmetricReferencesToGenericFiles(new List<Keyring>());
            mountedDirMirror.Clear();

            mountedDirMirror.RootFolder.name = rk.name;

            var defaultPath = "Unmapped_files/Unmapped_file_";
            var unmappedCnt = 0;

            foreach (var symmRef in symmRefList)
            {
                var mapping = rk.GetMountedDirMapping(symmRef.accessFileTargetPageName);

                if (mapping != null)
                {
                    mountedDirMirror.CreateFile(mapping, symmRef);
                }
                else
                {
                    mountedDirMirror.CreateFile(defaultPath + unmappedCnt, symmRef);
                    unmappedCnt++;
                }
            }

            var keyringPath = "Keyrings/";
            PopulateMountedDirKeyrings(MasterKeyring, keyringPath, new List<Keyring>());
            if (rk.accessFileReferenceToSelf != null)
            {
                var mdFile = mountedDirMirror.CreateFile(Path.Combine(keyringPath, "self"), 
                    rk.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf);
                if (mdFile != null) mdFile.TargetType = MDFile.Type.Keyring;
            }
        }

        private void PopulateMountedDirKeyrings(Keyring keyring, string path, List<Keyring> visitedKeyrings)
        {
            visitedKeyrings.Add(keyring);
            foreach (var symmRef in keyring.SymmetricReferences)
            {
                switch (symmRef.type)
                {
                    case PageType.GenericFile:
                    {
                        // Add MDFile representing Access File. Let name start with '_' and be either mapping
                        // from Master Keyring or page name of target file
                        var mapping = MasterKeyring.GetMountedDirMapping(symmRef.accessFileTargetPageName);
                        mountedDirMirror.CreateFile(
                            Path.Combine(path, "_" + (mapping ?? symmRef.accessFileTargetPageName)), symmRef);
                        break;
                    }
                    case PageType.Keyring:
                    {
                        var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                        if (kr == null)
                        {
                            Console.WriteLine("PopulateMountedDirKeyrings:- Keyring pn='{0}' is null",
                                symmRef.accessFileTargetPageName);
                            continue;
                        }

                        // Add MDFile representing Access File to Keyring.
                        mountedDirMirror.CreateFile(Path.Combine(path, "_" + kr.name), symmRef);
                        
                        if (visitedKeyrings.Contains(kr))
                        {
                            Console.WriteLine("keyring already visited, name = " + keyring.name);
                            continue;
                        }

                        // Continue recursively with children Keyrings
                        var mdFile = mountedDirMirror.CreateFile(Path.Combine(path, kr.name, "self"), symmRef);
                        if (mdFile != null) mdFile.TargetType = MDFile.Type.Keyring;
                        PopulateMountedDirKeyrings(kr, Path.Combine(path, kr.name), visitedKeyrings);
                        break;
                    }
                    default:
                        break;
                }
            }
        }

        public void GetKeyrings(ObservableCollection<Keyring> viewModelKeyrings)
        {
            viewModelKeyrings.Clear();
            viewModelKeyrings.AddRange(MasterKeyring.GetAllAndDescendantKeyrings(new List<Keyring>()));
        }

        public void GetOwnContacts(ObservableCollection<OwnContact> ownContacts)
        {
            ownContacts.Clear();
            ownContacts.AddRange(MasterKeyring.OwnContacts);
        }

        public void AddFilesToKeyring(List<Keyring> keyrings)
        {
            var symmetricReferences =
                mountedDirMirror.GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedRootFolder();

            // Remove symmetric references from existing parent keyrings? 
            // foreach (var symmRef in symmetricReferences)
            // {
            //     symmRef.keyringParent?.SymmetricReferences.Remove(symmRef);
            // }

            foreach (var keyring in keyrings)
            {
                foreach (var symmRef in symmetricReferences)
                {
                    var af = symmRef.targetAccessFile;

                    if (af == null)
                    {
                        var wh = GetWikiHandler(symmRef.serverLink);
                        af = wh?.DownloadAccessFile(symmRef);
                        if (af == null)
                        {
                            Console.WriteLine("AddFilesToKeyring:- Could not get AF from server, pageName='{0}",
                                symmRef.targetPageName);
                            continue;
                        }
                    }

                    // Check if the keyring already has an access file to the file
                    if (keyring.SymmetricReferences.Contains(symmRef)) continue;

                    var pageNameAccessFile = GetFreshPageName();
                    _keyringManager.CreateAccessFileAndReferences(af.AccessFileReference.targetPageName,
                        pageNameAccessFile,
                        configManager.DefaultServerLink, PageType.GenericFile,
                        out SymmetricReference symmetricReference,
                        out AccessFile accessFile);

                    keyring.AddSymmetricReference(symmetricReference);

                    // Upload new files to server
                    var wikiHandler = GetWikiHandler(accessFile!.AccessFileReference.serverLink);
                    var uploadResAF = wikiHandler?.UploadAccessFile(accessFile);
                    Console.WriteLine("uploadResAF:" + uploadResAF);

                    if (keyring.accessFileReferenceToSelf.AccessFileParent != null)
                    {
                        var uploadResKR = wikiHandler?.UploadKeyring(
                            keyring.accessFileReferenceToSelf.AccessFileParent, keyring);
                        Console.WriteLine("uploadResKR:" + uploadResKR);
                    }
                }
            }
        }

        public void RenameOwnContact(string nickname, OwnContact selectedOwnContact)
        {
            var contact = MasterKeyring.OwnContacts.FirstOrDefault(e => e.HasSameStaticProperties(selectedOwnContact));
            if (contact != null) contact.Nickname = nickname;
        }
        
        public void ShareFiles(List<Contact> contacts)
        {
            Console.WriteLine(contacts.Count);
            WriteToLogger("Sharing specified parts of keyring");

            // get the list of all clicked symmetric references
            var symmetricReferences = mountedDirMirror.GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedRootFolder();
            
            // get the list of all clicked keyrings
            var (FileList, FolderList) =
                mountedDirMirror.GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring();
            var keyringSymmrefs = mountedDirMirror.GetDescendantKeyringsBasedOnIsCheckedKeyring();

            // for each contact create a new list with access files not previously received
            foreach (var contact in contacts)
            {
                var newAccessFiles = new List<AccessFile>();

                foreach (var symmRef in symmetricReferences)
                {
                    var accessFile = symmRef.targetAccessFile;
                    
                    // The access file should not already contain a inbox reference to the contact
                    // otherwise the contact has received the file before
                    if (accessFile != null && !accessFile.inboxReferences.Contains(contact.InboxReference))
                    {
                        newAccessFiles.Add(accessFile);
                    }
                }

                foreach (var symmRef in keyringSymmrefs)
                {
                    var accessFile = symmRef.targetAccessFile;
                    
                    // The access file should not already contain a inbox reference to the contact
                    // otherwise the contact has received the file before
                    if (accessFile != null && !accessFile.inboxReferences.Contains(contact.InboxReference))
                    {
                        newAccessFiles.Add(accessFile);
                    }
                }

                if (newAccessFiles.Count == 0)
                {
                    continue;
                }
                
                foreach (var af in newAccessFiles)
                {
                    var path = MasterKeyring.GetMountedDirMapping(af.AccessFileReference.targetPageName);
                    if (path == null) continue;
                    var mdFile = mountedDirMirror.GetMDFile(path);
                    af.PrepareForExport(mdFile is {isCheckedWrite: true});
                    af.inboxReferences.Add(contact.InboxReference);
                }
                
                var newAccessFilesString = JSONSerialization.SerializeObject(newAccessFiles);

                var loggerMsg = $"Sharing {newAccessFiles.Count} new files with contact '{contact.Nickname}'.";
                WriteToLogger(loggerMsg);

                var httpResponse = UploadToInboxPage(contact.InboxReference.serverLink, 
                    contact.InboxReference.targetPageName, newAccessFilesString, contact.InboxReference.publicKey);

                // Write result to logger
                WriteToLogger(
                    httpResponse
                        ? $"Upload to inbox page belonging to contact '{contact.Nickname}' complete."
                        : $"Upload to inbox page belonging to contact '{contact.Nickname}' failed.",
                    null, LoggerEntry.LogPriority.Low);
            }
        }
    }
}