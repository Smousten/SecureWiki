using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        public IFuseInteraction tcpListener;

        public KeyringManager _keyringManager;
        public CacheManager cacheManager;

        public ConfigManager configManager;

        public MountedDirMirror mountedDirMirror;
        public Logger logger;
        public MasterKeyring MasterKeyring;
        private Dictionary<(string, string), string> RequestedRevision = new();
        public SymmetricReference symRefToMasterKeyring;
        public Dictionary<string, Keyring> KeyringMapping = new();

        public delegate void PrintTest(string input);

        public bool setupFinished = false;
        public bool GUIRunning = false;
        public uint UploadsInProgress = 0;
        private DateTime _lastUploadTimestamp;

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

            _keyringManager = new KeyringManager(MasterKeyring, this);
            tcpListener = new TCPListener(11111, "127.0.1.1", this);

            InitializeCacheManager();

            TCPListenerThread = new Thread(tcpListener.RunListener) {IsBackground = true};
            TCPListenerThread.Start();
            WriteToLogger("Starting up TCPListener", null);

            Thread.Sleep(1000);

            Thread fuseThread = new(Program.RunFuse) {IsBackground = true};
            fuseThread.Start();

            WriteToLogger("Starting up FUSE", null);
            
            // GUI can now proceed
            MainWindow.ManagerReadyEvent.Set();

            InitializeSymRefMasterKeyring();

            var wh = GetWikiHandler(symRefToMasterKeyring.serverLink);
            var newRootKR = wh?.DownloadMasterKeyring(symRefToMasterKeyring);

            if (newRootKR == null)
            {
                Console.WriteLine("root keyring from server is null");
                symRefToMasterKeyring.targetAccessFile.AccessFileReference.KeyringTarget = MasterKeyring;
                var pagename = GetFreshPageName(symRefToMasterKeyring.serverLink);
                MasterKeyring.OwnContact = new OwnContact("MasterKeyring",
                    new InboxReference(pagename ?? "asd", symRefToMasterKeyring.serverLink));
            }
            else
            {
                Console.WriteLine("root keyring from server is not null");
                newRootKR.name = "root from server";
                MasterKeyring.CopyFromOtherKeyring(newRootKR);
                symRefToMasterKeyring.targetAccessFile.AccessFileReference.KeyringTarget = MasterKeyring;
                UploadsInProgress++;
                wh!.DownloadKeyringsRecursion(MasterKeyring, MasterKeyring);
                UploadsInProgress--;
            }

            PopulateMountedDirMirror(MasterKeyring);
            UpdateMountedDirectory();
            
            UpdateFromInboxes(wh);
            setupFinished = true;
            Console.WriteLine("setupFinished = true;");
        }

        public void UpdateMountedDirectory()
        {
            Console.WriteLine(".printinfo");
            mountedDirMirror.PrintInfo();
            Console.WriteLine("CreateFileStructureRecursion");
            mountedDirMirror.CreateFileStructureRecursion(GetRootDir(""));
        }

        public void PrintTestMethod(string input)
        {
            Console.WriteLine("ManagerThread printing: " + input + " from thread:" +
                              Thread.CurrentThread.Name);
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
        
        public string GetFullFilePath(string filename)
        {
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../"));
            var path = Path.Combine(projectDir, filename);

            return path;
        }
        
        public void InitializeSymRefMasterKeyring()
        {
            Console.WriteLine("InitializeSymRefMasterKeyring entered");
            UploadsInProgress++;
            var path = GetFullFilePath("SymRefMasterKeyring.json");

            if (File.Exists(path))
            {
                symRefToMasterKeyring =
                    (SymmetricReference) JSONSerialization.ReadFileAndDeserialize(path, typeof(SymmetricReference));
                if (symRefToMasterKeyring == null)
                {
                    Console.WriteLine();
                    Console.WriteLine("InitializeSymRefMasterKeyring:- symRefToMasterKeyring null");
                    UploadsInProgress--;
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
                    UploadsInProgress--;
                    return;
                }

                // Create and connect references
                symRefToMasterKeyring.targetAccessFile = af;
            }
            else
            {
                // Create new references
                Console.WriteLine();
                Console.WriteLine("InitializeSymRefMasterKeyring:- creating new master sym ref");
                _keyringManager.CreateAccessFileAndReferences(configManager.DefaultServerLink, PageType.Keyring, 
                    out SymmetricReference symmetricReference, out AccessFile accessFile);
                symRefToMasterKeyring = symmetricReference;
                UploadsInProgress--;
            }
        }

        public void SaveSymRefMasterKeyringToFile()
        {
            UploadsInProgress++;
            var path = GetFullFilePath("SymRefMasterKeyring.json");

            JSONSerialization.SerializeAndWriteFile(path, symRefToMasterKeyring);

            Console.WriteLine("Upload access file to: " + symRefToMasterKeyring.targetPageName);
            Console.WriteLine("Upload keyring file to: " + symRefToMasterKeyring.accessFileTargetPageName);

            var wikihandler = GetWikiHandler(symRefToMasterKeyring.serverLink);
            var res1 = wikihandler?.UploadAccessFile(symRefToMasterKeyring.targetAccessFile);
            var res2 = wikihandler?.UploadKeyring(symRefToMasterKeyring.targetAccessFile, MasterKeyring);

            Console.WriteLine("res1, res2: {0}, {1}", res1, res2);
            UploadsInProgress--;
        }

        public void SaveToServer()
        {
            UploadsInProgress++;
            var (updatedAccessFiles, updatedKeyrings) =
                MasterKeyring.GetAllChangedAccessFilesAndKeyrings(MasterKeyring);

            var groupedAccessFiles = updatedAccessFiles.GroupBy(e =>
                e.AccessFileReference.serverLink).ToList();
            var groupedKeyrings = updatedKeyrings.GroupBy(e =>
                e.accessFileReferenceToSelf.serverLink).ToList();

            foreach (var group in groupedAccessFiles)
            {
                var wikihandler = GetWikiHandler(group.Key);
                foreach (var accessFile in group)
                {
                    wikihandler?.UploadAccessFile(accessFile);
                }
            }

            foreach (var group in groupedKeyrings)
            {
                var wikihandler = GetWikiHandler(group.Key);
                foreach (var keyring in group)
                {
                    if (keyring.accessFileReferenceToSelf.AccessFileParent != null)
                        wikihandler?.UploadKeyring(keyring.accessFileReferenceToSelf.AccessFileParent, keyring);
                }
            }

            UploadsInProgress--;
        }

        // Save accessfiles and keyrings to server if this has not happened recently
        public void AttemptSaveToServer()
        {
            var timestampNow = DateTime.Now;
            var timestampThreshold = _lastUploadTimestamp.AddMinutes(5);

            if (timestampNow > timestampThreshold)
            {
                SaveToServer();
            }
            else
            {
                Console.WriteLine("Keyring has been saved recently");
            }
        }

        // ConfigManager functions
        public void InitializeConfigManager()
        {
            var path = GetFullFilePath("Config.json");

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
            var path = GetFullFilePath("Config.json");

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
        
        // Cache manager functions
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

        public void SaveCacheManagerToFile()
        {
            string path = GetFullFilePath("CacheManager.json");
            SerializeCacheManagerAndWriteToFile(path);
        }

        public void InitializeCacheManager()
        {
            string path = GetFullFilePath("CacheManager.json");

            var existingCacheManager = ReadFromFileAndDeserializeToCacheManager(path) ?? new CacheManager();
            cacheManager = existingCacheManager;
        }

        public void CleanCache()
        {
            cacheManager.CleanCacheDirectory(configManager.CachePreference ?? new CachePreferences());
        }

        // WikiHandler functions
        private void InitializeWikiHandlers()
        {
            wikiHandlers = new();

            // TODO: read from config file
        }

        public IServerInteraction? GetWikiHandler(string url)
        {
            if (wikiHandlers.ContainsKey(url))
            {
                return wikiHandlers[url];
            }
            else
            {
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
                        // UpdateFromInboxes(output);
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

        private void UpdateFromInboxes(IServerInteraction? wikiHandler)
        {
            UpdateFromInboxRecursively(MasterKeyring);
            UpdateMountedDirectory();
        }

        private void UpdateFromInboxRecursively(Keyring kr)
        {
            UpdateKeyringWithNewInboxPageEntries(kr);
            
            foreach (var symmRef in kr.SymmetricReferences)
            {
                if (symmRef.type == PageType.GenericFile) continue;

                if (symmRef.targetAccessFile?.AccessFileReference.KeyringTarget != null)
                    UpdateFromInboxRecursively(symmRef.targetAccessFile.AccessFileReference.KeyringTarget);
            }
            UpdateMountedDirectory();
        }
        

        // Returns true if Access File already exists in Keyring and is thus merged
        private bool CheckIfAccessFileExistsAndMerge(string? existingAccessFilePath, Keyring? keyring, AccessFile? accessFile)
        {
            if (existingAccessFilePath == null) return false;
            var existingMDFile = mountedDirMirror.GetMDFile(existingAccessFilePath);
            var existingMDFileSymmRef = existingMDFile?.symmetricReference;
            // Check if the access file is in the targeted keyring
            if (existingMDFileSymmRef?.keyringParent == null || !existingMDFileSymmRef.keyringParent.Equals(keyring)
                   || existingMDFileSymmRef.targetAccessFile == null) return false;
            WriteToLogger("Received access file to an already existing file, merging access files.");
            var existingAccessFile = existingMDFileSymmRef.targetAccessFile;
            if (accessFile != null) existingAccessFile.MergeWithOtherAccessFileEntry(accessFile);
            existingAccessFile.HasBeenChanged = true;
            
            AttemptSaveToServer();
            return true;
        }

        public void UpdateKeyringWithNewInboxPageEntries(Keyring kr)
        {
            UploadsInProgress++;
            if (kr.OwnContact != null)
            {
                var wh = GetWikiHandler(kr.OwnContact.InboxReference.serverLink);

                if (wh == null)
                {
                    Console.WriteLine("UpdateKeyringWithNewInboxPageEntries:- wh null");
                    return;
                }

                var res = wh.DownloadFromInboxPage(kr.OwnContact);

                var rootDirPath = GetRootDir("");
                var defaultPath = "Unmapped_files/Unmapped_file_from_inbox_";
                var unmappedCnt = 0;

                foreach (var inboxPageEntry in res)
                {
                    if (string.IsNullOrEmpty(inboxPageEntry)) continue;

                    if (JSONSerialization.DeserializeObject(inboxPageEntry, typeof(List<AccessFile>)) is List<AccessFile>
                        afList)
                    {
                        foreach (var af in afList)
                        {
                            // Create symmetric reference for Access File and connect it
                            var serverLink = kr.accessFileReferenceToSelf.serverLink;
                            var pageName = GetFreshPageName(serverLink);
                            if (pageName == null)
                            {
                                Console.WriteLine("GetFreshPageName failed");
                                continue;
                            }
                        
                            var symmetricReference = new SymmetricReference(pageName, serverLink,
                                PageType.GenericFile, af.AccessFileReference.targetPageName, af);
                            af.AccessFileReference.AccessFileParent = af;
                            af.SymmetricReferenceToSelf = symmetricReference;
                        
                            if (!af.IsValid())
                            {
                                Console.WriteLine("af not valid, print inboxPageEntry:");
                                // Console.WriteLine(inboxPageEntry);
                            }
                            else if (kr.AlreadyContainsAccessFileWithAtLeastAsMuchAccess(af))
                            {
                                Console.WriteLine("AlreadyContainsAccessFileWithAtLeastAsMuchAccess, print inboxPageEntry:");
                                // Console.WriteLine(inboxPageEntry);
                            }
                            else
                            {
                                // If access file with same target already exists, merge
                                var existingAccessFilePath =
                                    MasterKeyring.GetMountedDirMapping(af.AccessFileReference.targetPageName);
                                if (CheckIfAccessFileExistsAndMerge(existingAccessFilePath, kr, af)) continue;

                                // Otherwise upload and add Access File
                                wh.UploadAccessFile(af);
                                kr.AddSymmetricReference(symmetricReference);


                                while (File.Exists(rootDirPath + defaultPath + unmappedCnt))
                                {
                                    unmappedCnt++;
                                }

                                var filepath = defaultPath + unmappedCnt;

                                var mdFile = mountedDirMirror.CreateFile(filepath, symmetricReference);
                                if (mdFile == null)
                                {
                                    WriteToLogger("File could not be added to MDMirror, upload failed");
                                    continue;
                                }
                                MasterKeyring.SetMountedDirMapping(af.AccessFileReference.targetPageName, filepath);

                                var symmRefKR = kr.accessFileReferenceToSelf.AccessFileParent?.SymmetricReferenceToSelf;
                                var krFolder = symmRefKR?.MDFile?.Parent;

                                if (krFolder != null)
                                {
                                    var mdFileAF = mountedDirMirror.CreateFile(Path.Combine(krFolder.path, krFolder.name, 
                                        symmRefKR!.accessFileTargetPageName), symmetricReference);
                                }
                            }
                        }
                    }
                
                }
            }

            UploadsInProgress--;
        }
        //
        // private static Dictionary<Contact, List<AccessFile>> DownloadIncomingAccessFiles(IServerInteraction? wikiHandler)
        // {
        //     Dictionary<Contact, List<AccessFile>> incomingAccessFiles = new();
        //     // Download from inbox - iterate through all new revisions for each contact access file
        //     var inboxContent = wikiHandler?.DownloadFromInboxPages();
        //     if (inboxContent != null)
        //     {
        //         foreach (var contactInbox in inboxContent.Keys)
        //         {
        //             foreach (var revision in inboxContent[contactInbox])
        //             {
        //                 if (JSONSerialization.DeserializeObject(revision, typeof(List<AccessFile>)) is List<AccessFile>
        //                     deserializeObject)
        //                 {
        //                     if (incomingAccessFiles.ContainsKey(contactInbox))
        //                     {
        //                         incomingAccessFiles[contactInbox].AddRange(deserializeObject);
        //                     }
        //                     else
        //                     {
        //                         incomingAccessFiles.Add(contactInbox, deserializeObject);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //
        //     return incomingAccessFiles;
        // }

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
        
        // Upload input content to given serverLink and pageName using the wikiHandler
        private bool UploadToInboxPage(string serverLink, string pageName, string content, byte[] publicKey)
        {
            UploadsInProgress++;
            var wikiHandler = GetWikiHandler(serverLink);

            var result = wikiHandler?.UploadToInboxPage(pageName, content, publicKey);
            UploadsInProgress--;
            return result == true;
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
            UploadsInProgress++;
            string pageName;
            var mdFile = mountedDirMirror.GetMDFile(filepath);
            var symmRef = mdFile?.symmetricReference;

            if (symmRef == null)
            {
                // Write to logger
                string loggerMsg = $"File upload to server failed. Could not find Symmetric Reference";
                WriteToLogger(loggerMsg, filepath);
                UploadsInProgress--;
                return;
            }

            var whAF = GetWikiHandler(symmRef.serverLink);
            if (whAF == null)
            {
                UploadsInProgress--;
                return;
            }

            if (mdFile!.TargetType == MDFile.Type.AccessFile)
            {
                var fileContent = File.ReadAllBytes(GetRootDir(filepath));
                whAF.Upload(symmRef, fileContent);
            }
            else
            {
                // Get Access File
                var af = symmRef.targetAccessFile ?? whAF.DownloadAccessFile(symmRef);
                if (af == null)
                {
                    UploadsInProgress--;
                    return;
                }


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

            AttemptSaveToServer();
            UploadsInProgress--;
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
            
                if (cacheResult != null)
                {
                    return Convert.FromBase64String(cacheResult);
                }
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
            return success ? pageName : null;
        }

        private string? TryFreshPageName(string serverLink, IServerInteraction? wikiHandler)
        {
            UploadsInProgress++;
            var tmp = RandomString.GenerateRandomAlphanumericString();
            tmp = char.ToUpper(tmp[0]) + tmp.Substring(1);
            if (!PageAlreadyExists(tmp, "-1", serverLink))
            {
                // Upload placeholder text to page to reserve it
                wikiHandler?.Upload(tmp, "placeholder");
                UploadsInProgress--;
                return tmp;
            }

            UploadsInProgress--;
            return null;
        }

        // public void UploadMasterKeyring()
        // {
        //     UploadsInProgress++;
        //     if (!PageAlreadyExists(_keyringManager.masterKey.pageName, "-1",
        //         configManager.DefaultServerLink))
        //     {
        //         var wikiHandler = GetWikiHandler(configManager.DefaultServerLink);
        //         wikiHandler?.UploadMasterKeyring(_keyringManager.masterKey.symmKey,
        //             _keyringManager.masterKey.pageName,
        //             MasterKeyring);
        //     }
        //
        //     UploadsInProgress--;
        // }

        // Delegated Keyring functions
        public void AddNewFile(string filepath)
        {
            UploadsInProgress++;
            
            _keyringManager.CreateAccessFileAndReferences(configManager.DefaultServerLink, PageType.GenericFile,
                out SymmetricReference symmetricReference, out AccessFile accessFile);

            // Add symmetric reference to newEntries keyring and upload
            AddToDefaultKeyring(symmetricReference);

            // Create new entry in md mirror
            var mdFile = mountedDirMirror.CreateFile(filepath, symmetricReference);
            if (mdFile == null)
            {
                WriteToLogger("File could not be added to MDMirror, upload failed");
                UploadsInProgress--;
                return;
            }

            var wikiHandler = GetWikiHandler(accessFile!.AccessFileReference.serverLink);
            var fileContent = Encoding.ASCII.GetBytes("This is the first revision");
            var uploadResFile = wikiHandler?.Upload(accessFile, fileContent);

            if (uploadResFile == false)
            {
                WriteToLogger("File could not be uploaded", filepath);
                UploadsInProgress--;
                return;
            }

            MasterKeyring.SetMountedDirMapping(accessFile.AccessFileReference.targetPageName, filepath);
            UploadsInProgress--;
            UpdateMountedDirectory();
        }

        private void AddToDefaultKeyring(SymmetricReference symmetricReference)
        {
            var defaultKeyring = _keyringManager.AddToDefaultKeyring(symmetricReference);
            var accessFileToDefaultKeyring = defaultKeyring.accessFileReferenceToSelf.AccessFileParent;

            if (accessFileToDefaultKeyring == null)
            {
                return;
            }

            accessFileToDefaultKeyring.HasTargetBeenChanged = true;
            AttemptSaveToServer();
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
            // UploadsInProgress++;
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
                // UploadsInProgress--;
                return;
            }

            // Add keyring contact to masterkeyring
            MasterKeyring.ContactManager.OwnContacts.Add(new OwnContact(keyring.name, keyring.OwnContact.InboxReference));
            AttemptSaveToServer();
            // UploadsInProgress--;
            UpdateMountedDirectory();
        }

        // private SymmetricReference? GetKeyringReference(string name, Keyring keyring)
        // {
        //     foreach (var symmRef in keyring.SymmetricReferences)
        //     {
        //         if (symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget != null
        //             && symmRef.type == PageType.Keyring &&
        //             symmRef.targetAccessFile.AccessFileReference.KeyringTarget.name.Equals(name))
        //         {
        //             return symmRef;
        //         }
        //
        //         var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
        //         if (kr == null)
        //         {
        //             continue;
        //         }
        //
        //         var res = GetKeyringReference(name, kr);
        //         if (res != null)
        //         {
        //             return res;
        //         }
        //     }
        //
        //     return null;
        // }

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

        public void RevokeAccess(AccessFile accessFile, List<Contact> contacts)
        {
            // UploadsInProgress++;
            WriteToLogger($"Attempting to revoke access to file '{accessFile.filename}'");

            var wikiHandler = GetWikiHandler(accessFile.AccessFileReference.serverLink);
            var latestRevision = wikiHandler?.GetLatestRevision(accessFile.AccessFileReference.targetPageName);

            // Create new cryptographic keys for access file
            if (latestRevision?.revisionID != null && accessFile.ownerPrivateKey != null)
            {
                _keyringManager.RevokeAccess(accessFile, latestRevision.revisionID);
            }

            // List of one access file containing latest key
            // to selected inbox pages. Send keys depending on access level of user. 
            var uploadReadWrite = new List<AccessFile>();
            var uploadReadOnly = new List<AccessFile>();

            var accessFileReadWrite = accessFile.Copy();
            var latestTwoKeys = accessFile.keyList.Skip(accessFile.keyList.Count - 2);

            accessFileReadWrite.keyList = new List<AccessFileKey> {latestTwoKeys};
            accessFileReadWrite.ownerPrivateKey = null;
            uploadReadWrite.Add(accessFileReadWrite);

            var accessFileReadOnly = accessFileReadWrite.Copy();
            foreach (var key in accessFileReadOnly.keyList)
            {
                key.PrivateKey = null;
                key.SignedWriteKey = null;
            }

            uploadReadOnly.Add(accessFileReadOnly);

            var serializeObjectReadWrite = JSONSerialization.SerializeObject(uploadReadWrite);
            var serializeObjectReadOnly = JSONSerialization.SerializeObject(uploadReadOnly);
            foreach (var contact in contacts)
            {
                var inboxReference = accessFile.inboxReferences.FirstOrDefault(e => e.HasSameStaticProperties(contact.InboxReference));
                UploadToInboxPage(contact.InboxReference.serverLink, contact.InboxReference.targetPageName,
                    inboxReference.accessLevel == InboxReference.AccessLevel.ReadWrite ? 
                        serializeObjectReadWrite : serializeObjectReadOnly, contact.InboxReference.publicKey);
            }
            
            // Remove non-selected references from access file reference list

            var removeList = new List<InboxReference>();
            
            foreach (var inboxReference in accessFile.inboxReferences)
            {
                if (!contacts.Exists(c => c.InboxReference.HasSameStaticProperties(inboxReference)))
                {
                    removeList.Add(inboxReference);
                }
            }

            foreach (var item in removeList)
            {
                accessFile.inboxReferences.Remove(item);
            }
            

            accessFile.HasBeenChanged = true;
            AttemptSaveToServer();
            // UploadsInProgress--;
        }
        
        public void Share(List<Contact> contacts)
        {
            var checkedItems = mountedDirMirror.GetNecessarySymmetricReferencesToCheckedItems();

            var accessFilesAndIsChecked = new List<(AccessFile, bool)>();

            foreach (var (symmRef, isCheckedWrite) in checkedItems)
            {
                if (symmRef.targetAccessFile == null) continue;

                accessFilesAndIsChecked.Add((symmRef.targetAccessFile, isCheckedWrite));
            }

            var accessFilesToUpload = _keyringManager.
                AddContactsToAccessFilesInBulk(accessFilesAndIsChecked, contacts);

            var accessFilesPreparedForExport = _keyringManager.PrepareForExport(accessFilesAndIsChecked);

            foreach (var contact in contacts)
            {
                var wh = GetWikiHandler(contact.InboxReference.serverLink);
                if (wh == null)
                {
                    Console.WriteLine("wh == null, serverLink: " + contact.InboxReference.serverLink);
                    continue;
                }

                var afList = accessFilesToUpload[contact].Select(af => accessFilesPreparedForExport[af]).ToList();
                var afListString = JSONSerialization.SerializeObject(afList);

                wh.UploadToInboxPage(contact.InboxReference.targetPageName, afListString,
                    contact.InboxReference.publicKey);
            }
        }
        
        public void MoveFilesToKeyrings(List<Keyring> keyrings)
        {
            UploadsInProgress++;
            var symmetricReferences =
                mountedDirMirror.GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedRootFolder();

            // Remove symmetric references from existing parent keyrings
            foreach (var (symmRef, _) in symmetricReferences)
            {
                symmRef.keyringParent?.SymmetricReferences.Remove(symmRef);
            }

            foreach (var keyring in keyrings)
            {
                foreach (var (symmRef, _) in symmetricReferences)
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
                    
                    _keyringManager.CreateAccessFileAndReferences(configManager.DefaultServerLink, PageType.GenericFile,
                        out SymmetricReference symmetricReference, out AccessFile accessFile, af.AccessFileReference.targetPageName);

                    keyring.AddSymmetricReference(symmetricReference);

                    accessFile.HasBeenChanged = true;
                    if (keyring.accessFileReferenceToSelf.AccessFileParent != null)
                    {
                        keyring.accessFileReferenceToSelf.AccessFileParent.HasTargetBeenChanged = true;
                    }
                }
            }

            AttemptSaveToServer();
            UploadsInProgress--;
            
            PopulateMountedDirMirror(MasterKeyring);
            UpdateMountedDirectory();
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
        
        // write to logger with normal as default priority 
        public void WriteToLogger(string content, string? location = null,
            LoggerEntry.LogPriority priority = LoggerEntry.LogPriority.Normal)
        {
            logger.Add(content, location, priority);
        }

        public void GetKeyrings(ObservableCollection<Keyring> viewModelKeyrings)
        {
            viewModelKeyrings.Clear();
            viewModelKeyrings.AddRange(MasterKeyring.GetAllAndDescendantKeyrings(new List<Keyring>()));
        }

        public void GetOwnContacts(ObservableCollection<OwnContact> ownContacts)
        {
            ownContacts.Clear();
            ownContacts.AddRange(MasterKeyring.ContactManager.OwnContacts);
        }

        public void RenameOwnContact(string nickname, OwnContact selectedOwnContact)
        {
            var contact =
                MasterKeyring.ContactManager.OwnContacts.FirstOrDefault(e =>
                    e.HasSameStaticProperties(selectedOwnContact));
            if (contact != null) contact.Nickname = nickname;
        }

        // Contacts in revoke popup list should only show contacts in access file
        public void GetFileContacts(ObservableCollection<Contact> revokeContacts, AccessFile accessFile)
        {
            revokeContacts.Clear();

            var contactList = new List<Contact>();
            foreach (var inboxReference in accessFile.inboxReferences)
            {
                var contact = MasterKeyring.ContactManager.FindContact(inboxReference) ??
                              MasterKeyring.ContactManager.FindOwnContact(inboxReference);

                if (contact != null)
                {
                    contactList.Add(contact);
                }
            }

            revokeContacts.AddRange(contactList);
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

            MasterKeyring.ContactManager.Contacts.AddRange(newContacts);
        }

        public void GetAllContacts(ObservableCollection<Contact> contactsOwn,
            ObservableCollection<Contact> contactsOther)
        {
            contactsOwn.Clear();
            contactsOther.Clear();
            if (MasterKeyring.ContactManager.OwnContacts.Count > 0)
            {
                contactsOwn.AddRange(MasterKeyring.ContactManager.OwnContacts);
            }

            if (MasterKeyring.ContactManager.Contacts.Count > 0)
            {
                contactsOther.AddRange(MasterKeyring.ContactManager.Contacts);
            }
        }

        public void GetAllContacts(ObservableCollection<Contact> contacts)
        {
            contacts.Clear();

            if (MasterKeyring.ContactManager.OwnContacts.Count > 0)
            {
                contacts.AddRange(MasterKeyring.ContactManager.OwnContacts);
            }

            if (MasterKeyring.ContactManager.Contacts.Count > 0)
            {
                contacts.AddRange(MasterKeyring.ContactManager.Contacts);
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
    }
}