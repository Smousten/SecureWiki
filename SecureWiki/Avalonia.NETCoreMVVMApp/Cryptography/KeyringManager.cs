using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWiki.Cryptography
{
    public class KeyringManager
    {
        private readonly MasterKeyring _masterKeyring;
        private readonly Manager _manager;
        private DateTime _rootKeyringWriteTimestamp;
        public Masterkey masterKey { get; set; }

        public KeyringManager(MasterKeyring rk, Manager manager)
        {
            _masterKeyring = rk;
            _manager = manager;
        }

        // Initialises keyring object by loading data from json file into rootkeyring object
        // Creates the file structure in root directory with empty files
        // public void InitKeyring()
        // {
        //     var filepath = GetFilePath("Keyring.json");
        //     
        //     // Check if Keyring.json exists
        //     if (File.Exists(filepath))
        //     {
        //         // Read Keyring.json into rootKeyring
        //         ReadIntoKeyring(_masterKeyring);
        //         _masterKeyring.SortAllRecursively();
        //         UpdateKeyringParentPropertyRecursively(_masterKeyring);
        //     }
        //
        //     CreateFileStructureRecursion(_masterKeyring, GetRootDirPath());
        // }
        //
        // private Masterkey? GetMasterkey(string filepath)
        // {
        //     // Deserialize master key
        //     var deserialized = JSONSerialization.ReadFileAndDeserialize(
        //         filepath, typeof(Masterkey)) as Masterkey;
        //         
        //     // If import file is not a keyring
        //     if (deserialized == null)
        //     {
        //         var loggerMsg = "Import file cannot be parsed as a master key object.";
        //         _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //         Console.WriteLine(loggerMsg);
        //     }
        //
        //     return deserialized;
        // }
        
        // Generate master key if it does not exist
        // private void InitMasterKey()
        // {
        //     var filepath = GetFilePath("MasterKey.json");
        //     
        //     // Check if MasterKey.json exists
        //     if (File.Exists(filepath))
        //     {
        //         var getMasterkey = GetMasterkey(filepath);
        //         if (getMasterkey != null) masterKey = getMasterkey;
        //     }
        //     else
        //     {
        //         GenerateAndSerializeMasterkey(filepath);
        //     }
        // }

        // private void GenerateAndSerializeMasterkey(string filepath)
        // {
        //     // Generate master key and serialize
        //     masterKey.symmKey = Crypto.GenerateSymmKey();
        //     masterKey.pageTitle = RandomString.GenerateRandomAlphanumericString();
        //     JSONSerialization.SerializeAndWriteFile(filepath, masterKey);
        // }

        // // Returns absolute file path to fuse rootdir as string
        // private static string GetRootDirPath()
        // {
        //     const string? filePath = "fuse/directories/rootdir/";
        //     var currentDir = Directory.GetCurrentDirectory();
        //     var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
        //     var rootDir = Path.Combine(projectDir, filePath);
        //     return rootDir;
        // }

        // // Returns absolute file path to keyring jsonfile as string
        // public string GetFilePath(string file)
        // {
        //     var currentDir = Directory.GetCurrentDirectory();
        //     var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
        //     return Path.Combine(path, file);
        // }

        // // Returns root keyring as deserialized json object
        // private MasterKeyring? GetRootKeyring(string keyringFilePath)
        // {
        //     var existingKeyRing = JSONSerialization.ReadFileAndDeserialize(
        //         keyringFilePath, typeof(MasterKeyring)) as MasterKeyring;
        //     return existingKeyRing;
        // }

        // Returns root keyring as deserialized json object with no arguments
        // public Keyring? ReadKeyRing()
        // {
        //     var keyringFilePath = GetFilePath("Keyring.json");
        //     return GetRootKeyring(keyringFilePath);
        // }

        // Copies all data from json object into root keyring object
        // private void ReadIntoKeyring(MasterKeyring rk)
        // {
        //     var readKeyring = ReadKeyRing();
        //     if (readKeyring != null) rk.CopyFromOtherKeyring(readKeyring);
        // }
        //
        // Recursively creates all files and folders from root keyring
        // public void CreateFileStructureRecursion(Keyring keyring, string path)
        // {
        //     foreach (var reference in keyring.SymmetricReferences)
        //     {
        //         if (reference.type == PageType.GenericFile)
        //         {
        //             File.Create(Path.Combine(path, reference.targetAccessFile.filename)).Dispose();
        //         }
        //         else if (reference.type == PageType.Keyring)
        //         {
        //             Directory.CreateDirectory(Path.Combine(path, reference.targetAccessFile.filename));
        //             CreateFileStructureRecursion(reference.targetAccessFile.AccessFileReference.KeyringTarget, 
        //                 Path.Combine(path, reference.targetAccessFile.filename));
        //         }
        //     }
        // }
        
        // Returns the keyringEntry where the new keyring/access file should be inserted
        // private Keyring FindKeyringPath(Keyring keyring, string filePath)
        // {
        //     var filePathSplit = filePath.Split("/");
        //
        //     // Found keyring entry to insert into
        //     if (filePathSplit.Length <= 1)
        //     {
        //         return keyring;
        //     }
        //
        //     var childKeyring = keyring.keyrings.FirstOrDefault(f => f.name.Equals(filePathSplit[0]));
        //     var newPath = string.Join("/", filePathSplit.Skip(1).ToArray());
        //
        //     if (childKeyring != null)
        //     {
        //         return FindKeyringPath(childKeyring, newPath);
        //     }
        //
        //     Keyring intermediateKeyring = new()
        //     {
        //         name = filePathSplit[0],
        //         accessFiles = new ObservableCollection<AccessFile>(),
        //         keyrings = new ObservableCollection<Keyring>()
        //     };
        //     keyring.AddKeyring(intermediateKeyring);
        //     return FindKeyringPath(intermediateKeyring, newPath);
        // }

        // Add new access file to existing keyring json file
        // public void AddNewFile(string filename, string filepath, string serverLink, string pageTitle)
        // {
        //     // var serverLink = "http://localhost/mediawiki/api.php";
        //     AccessFile accessFile = new(serverLink, pageTitle, filename);
        //     
        //     // Find the keyring where the new access file is inserted
        //     var foundKeyring = FindKeyringPath(_masterKeyring, filepath);
        //     foundKeyring.AddAccessFile(accessFile);
        //
        //     AttemptSaveRootKeyring();
        // }

        // Add new access file to existing keyring json file
        // public void AddNewKeyRing(string filename, string filepath)
        // {
        //     Keyring newKeyring = new()
        //     {
        //         name = filename,
        //         accessFiles = new ObservableCollection<AccessFile>(),
        //         keyrings = new ObservableCollection<Keyring>()
        //     };
        //
        //     // Find the keyring where the new keyring is inserted
        //     var foundKeyring = FindKeyringPath(_masterKeyring, filepath);
        //     foundKeyring.AddKeyring(newKeyring);
        //
        //     // JsonSerializerOptions options = new() {WriteIndented = true};
        //
        //     // var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
        //     AttemptSaveRootKeyring();
        // }

        // Find the access file with the given name -- better performance if whole filepath is given
        // public AccessFile? GetAccessFile(string filename, Keyring keyring)
        // {
        //     var accessFile = keyring.accessFiles.FirstOrDefault(f => f.filename.Equals(filename));
        //     if (accessFile != null)
        //     {
        //         return accessFile;
        //     }
        //
        //     foreach (var childKeyring in keyring.keyrings)
        //     {
        //         var result = GetAccessFile(filename, childKeyring);
        //         if (result != null)
        //         {
        //             return result;
        //         }
        //     }
        //
        //     return null;
        //
        //     // Linq one-line
        //     // return accessFile ?? keyring.keyrings.Select(childKeyRing => GetAccessFile(filename, childKeyRing)).FirstOrDefault(entry => entry != null);
        // }
        
        // public AccessFile? GetAccessFile(string filename, Keyring keyring)
        // {
        //     foreach (var symRef in keyring.SymmetricReferences)
        //     {
        //         if (symRef.targetAccessFile != null && symRef.targetAccessFile.filename.Equals(filename))
        //         {
        //             return symRef.targetAccessFile;
        //         }
        //
        //         if (symRef.type == PageType.GenericFile)
        //         {
        //             continue;
        //         }
        //
        //         var kr = symRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
        //         if (kr == null) continue;
        //         var result = GetAccessFile(filename, kr);
        //         if (result != null)
        //         {
        //             return result;
        //         }
        //     }
        //
        //     return null;
        //
        //     // Linq one-line
        //     // return accessFile ?? keyring.keyrings.Select(childKeyRing => GetAccessFile(filename, childKeyRing)).FirstOrDefault(entry => entry != null);
        // }

        //
        // // Rename or change location of access file/keyring in root keyringEntry 
        // public void Rename(string oldPath, string newPath)
        // {
        //     var oldKeyring = FindKeyringPath(_masterKeyring, oldPath);
        //     var newKeyring = FindKeyringPath(_masterKeyring, newPath);
        //
        //     var oldNameSplit = oldPath.Split("/", 2);
        //     var oldName = oldNameSplit[^1];
        //     oldName = oldName.TrimEnd('\0');
        //
        //     var newNameSplit = newPath.Split("/", 2);
        //     var newName = newNameSplit[^1];
        //     newName = newName.TrimEnd('\0');
        //
        //     // Rename/relocate access file/keyring
        //     // Find access file in oldkeyring
        //     AccessFile? accessFile = oldKeyring.accessFiles.FirstOrDefault(f => f.filename.Equals(oldName));
        //     if (accessFile != null)
        //     {
        //         oldKeyring.accessFiles.Remove(accessFile);
        //         accessFile.filename = newName;
        //         newKeyring.AddAccessFile(accessFile);
        //     }
        //
        //     // Find keyring in oldkeyring
        //     var keyring = oldKeyring.keyrings.FirstOrDefault(f => f.name.Equals(oldName));
        //     if (keyring != null)
        //     {
        //         oldKeyring.keyrings.Remove(keyring);
        //         keyring.name = newName;
        //         newKeyring.AddKeyring(keyring);
        //     }
        //     
        //     AttemptSaveRootKeyring();
        // }
        //
        // // Remove file from keyring object
        // public void RemoveFile(string filePath, string filename)
        // {
        //     // Find the keyring where the access file is located
        //     var foundKeyring = FindKeyringPath(_masterKeyring, filePath);
        //
        //     // Remove file or keyring from parent keyring
        //     var fileToRemove = foundKeyring.accessFiles.FirstOrDefault(f => f.filename.Equals(filename));
        //     if (fileToRemove != null) foundKeyring.accessFiles.Remove(fileToRemove);
        //
        //     var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filename));
        //     if (keyringToRemove != null) foundKeyring.keyrings.Remove(keyringToRemove);
        //
        //     AttemptSaveRootKeyring();
        // }
        //
        // // Save root keyring to file
        // public void SaveRootKeyring()
        // {
        //     Console.WriteLine("Saving to Keyring.json");
        //     var path = GetFilePath("Keyring.json");
        //     JSONSerialization.SerializeAndWriteFile(path, _masterKeyring);
        //     _rootKeyringWriteTimestamp = DateTime.Now;
        // }
        //
        // // Save root keyring to file if this has not happened recently
        // public void AttemptSaveRootKeyring()
        // {
        //     var timestampNow = DateTime.Now;
        //     var timestampThreshold = _rootKeyringWriteTimestamp.AddMinutes(5);
        //
        //     if (timestampNow > timestampThreshold)
        //     {
        //         SaveRootKeyring();
        //     }
        //     else
        //     {
        //         Console.WriteLine("Keyring has been saved recently");
        //     }
        // }

        // // Recursively update keyring parent property
        // private void UpdateKeyringParentPropertyRecursively(Keyring ke)
        // {
        //     foreach (AccessFile item in ke.accessFiles)
        //     {
        //         item.parent = ke;
        //     }
        //
        //     foreach (Keyring item in ke.keyrings)
        //     {
        //         item.parent = ke;
        //         UpdateKeyringParentPropertyRecursively(item);
        //     }
        // }

        // Create and return a root keyring containing only all checked entries
        // public MasterKeyring CreateRootKeyringBasedOnIsChecked()
        // {
        //     MasterKeyring outputMasterKeyring = new();
        //
        //     _masterKeyring.AddToOtherKeyringRecursivelyBasedOnIsChecked(outputMasterKeyring);
        //     outputMasterKeyring.RemoveEmptyDescendantsRecursively();
        //
        //     return outputMasterKeyring;
        // }

        // // Same as CreateRootKeyringBasedOnIsChecked() but uses deep copies instead
        // private MasterKeyring CreateCopyRootKeyringBasedOnIsChecked()
        // {
        //     MasterKeyring outputMasterKeyring = new();
        //
        //     _masterKeyring.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(outputMasterKeyring);
        //     outputMasterKeyring.RemoveEmptyDescendantsRecursively();
        //
        //     return outputMasterKeyring;
        // }
        //
        // public MasterKeyring CreateCopyRootKeyring()
        // {
        //     MasterKeyring outputMasterKeyring = new();
        //
        //     _masterKeyring.AddCopiesToOtherKeyringRecursively(outputMasterKeyring);
        //     outputMasterKeyring.RemoveEmptyDescendantsRecursively();
        //
        //     return outputMasterKeyring;
        // }

        // public void ExportRootKeyringBasedOnIsChecked()
        // {
        //     MasterKeyring rk = CreateCopyRootKeyringBasedOnIsChecked();
        //
        //     // Remove information that should not be shared
        //     rk.PrepareForExportRecursively();
        //
        //     // Get export path            
        //     var currentDir = Directory.GetCurrentDirectory();
        //     var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
        //     var keyringFileName = "KeyringExport.json";
        //     var keyringFilePath = Path.Combine(path, keyringFileName);
        //
        //     JSONSerialization.SerializeAndWriteFile(keyringFilePath, rk);
        // }

        // public void ImportRootKeyring(string importPath)
        // {
        //     // Read RootKeyring from import path and initialise
        //     var rk = GetRootKeyring(importPath);
        //
        //     // If import file is not a keyring
        //     if (rk == null)
        //     {
        //         var loggerMsg = "Import file cannot be parsed as a keyring object. Merged aborted.";
        //         _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //         Console.WriteLine(loggerMsg);
        //         
        //         return;
        //     }
        //     
        //     // If any of the access files in the imported keyring cannot be verified
        //     var (res, failedAF) = VerifyImportKeyring(rk); 
        //     if (res == false)
        //     {
        //         var location = failedAF!.pageName;
        //         var loggerMsg = $"Import keyring contains invalid key in access file with pageName='{location}'. " +
        //                         $"Merge aborted.";
        //         _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //         Console.WriteLine(loggerMsg);
        //         return;
        //     }
        //
        //     UpdateKeyringParentPropertyRecursively(rk);
        //
        //     // Sort imported keyring
        //     rk.SortAllRecursively();
        //     
        //     // Merge imported RootKeyring into current RootKeyring
        //     Console.WriteLine("Merging existing RootKeyring and imported Keyring");
        //     _masterKeyring.MergeAllEntriesFromOtherKeyring(rk);
        //     SortAndUpdatePeripherals();
        // }
        //
        // public void SortAndUpdatePeripherals()
        // {
        //     _masterKeyring.SortAllRecursively();
        //     Console.WriteLine("Updating mounted directory to reflect changes in RootKeyring");
        //     CreateFileStructureRecursion(_masterKeyring, GetRootDirPath());
        //
        //     // Write changes to Keyring.json
        //     SaveRootKeyring();
        // }

        public void RevokeAccess(AccessFile accessFile, string latestRevisionID)
        {
            // If no revisions are known to exist for current latest key
            if (accessFile.keyList.Last().RevisionStart.Equals("-1")) return;
            
            // Set end revision for current latest key
            accessFile.keyList.Last().RevisionEnd = latestRevisionID;
            
            // Create
            AccessFileKey newAccessFileKey = new(accessFile.ownerPrivateKey!);
            accessFile.keyList.Add(newAccessFileKey);
        }

        // // Recursively verify all keys of all access files in given keyring 
        // private (bool, AccessFile?) VerifyImportKeyring(Keyring rk)
        // {
        //     foreach (var df in rk.accessFiles)
        //     {
        //         if (!df.VerifyKeys())
        //         {
        //             return (false, df);
        //         }
        //     }
        //
        //     foreach (var ke in rk.keyrings)
        //     {
        //         var res = VerifyImportKeyring(ke);
        //
        //         if (res.Item1 == false)
        //         {
        //             return res;
        //         }
        //     }
        //
        //     return (true, null);
        // }
        
        // Create new access file and connect it to fresh references
        public void CreateAccessFileAndReferences(string pageNameTarget, string pageNameAccessFile, 
            string serverLink, PageType type, out SymmetricReference symmetricReference, 
            out AccessFile accessFile)
        {
            // Create access file and reference
            accessFile = new AccessFile(serverLink, pageNameTarget, type); 
            
            // Create symmetric reference to access file
            symmetricReference = new SymmetricReference(pageNameAccessFile,
                serverLink, type, pageNameTarget, accessFile);
            accessFile.SymmetricReferenceToSelf = symmetricReference;
        }

        public Keyring? CreateNewKeyring(string name, string serverLink)
        {
            var pageNameKeyring = _manager.GetFreshPageName();
            var pageNameAccessFile = _manager.GetFreshPageName();
            var pageNameInboxPage = _manager.GetFreshPageName();
            
            CreateAccessFileAndReferences(pageNameKeyring, pageNameAccessFile, serverLink, PageType.Keyring, 
                out SymmetricReference symmetricReference,
                out AccessFile accessFile);
            
            // Create new keyring object
            var keyring = new Keyring(accessFile.AccessFileReference, name);
            
            // Create inbox reference to inbox page
            InboxReference inboxReference = new(pageNameInboxPage, serverLink, keyring);
            keyring.InboxReferenceToSelf = inboxReference;
            
            return keyring;
        }
        
        // Add symmetric reference to newEntries keyring
        public Keyring AddToDefaultKeyring(SymmetricReference symmetricReference)
        {
            Console.WriteLine("AddToDefaultKeyring entered");
            // AccessFile? accessFile;
            
            // Check if default Keyring already exists
            var symmRefToDefaultKeyring = _masterKeyring.SymmetricReferences.FirstOrDefault(
                e => e.type == PageType.Keyring 
                     && e.targetAccessFile?.AccessFileReference?.KeyringTarget!.name.Equals("newEntries") == true);
            var defaultKeyring = symmRefToDefaultKeyring?.targetAccessFile?.AccessFileReference?.KeyringTarget;
            
            // If no such keyring already exists
            if (defaultKeyring == null)
            {
                Console.WriteLine("defaultkeyring is null");
                defaultKeyring = CreateNewKeyring("newEntries", _manager.configManager.DefaultServerLink);
                _masterKeyring.AddSymmetricReference(defaultKeyring.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf);
            }
            // else
            // {
            //     Console.WriteLine("defaultkeyring is not null");
            //     if (defaultKeyring.accessFileReferenceToSelf == null)
            //     {
            //         Console.WriteLine("defaultKeyring.accessFileReferenceToSelf is null");
            //         Console.WriteLine("defaultKeyring.name = " + defaultKeyring.name);
            //     }
            //     if (defaultKeyring.accessFileReferenceToSelf?.AccessFileParent == null)
            //     {
            //         Console.WriteLine("defaultKeyring.accessFileReferenceToSelf?.AccessFileParent is null");
            //         Console.WriteLine("defaultKeyring.name = " + defaultKeyring.name);
            //     }
            //     accessFile = defaultKeyring.accessFileReferenceToSelf.AccessFileParent;
            // }
            //
            // if (accessFile == null)
            // {
            //     Console.WriteLine("accessFile is null");
            // }

            defaultKeyring.AddSymmetricReference(symmetricReference);
            return defaultKeyring;
        }
    }
}