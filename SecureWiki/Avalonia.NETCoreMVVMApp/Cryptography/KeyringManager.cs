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
        private readonly RootKeyring _rootKeyring;
        private readonly Manager _manager;
        private DateTime _rootKeyringWriteTimestamp;
        public Masterkey masterKey { get; set; }

        public KeyringManager(RootKeyring rk, Manager manager)
        {
            _rootKeyring = rk;
            _manager = manager;
        }

        // Initialises keyring object by loading data from json file into rootkeyring object
        // Creates the file structure in root directory with empty files
        public void InitKeyring()
        {
            var filepath = GetFilePath("Keyring.json");
            
            // Check if Keyring.json exists
            if (File.Exists(filepath))
            {
                // Read Keyring.json into rootKeyring
                ReadIntoKeyring(_rootKeyring);
                _rootKeyring.SortAllRecursively();
                UpdateKeyringParentPropertyRecursively(_rootKeyring);
            }

            CreateFileStructureRecursion(_rootKeyring, GetRootDirPath());
        }

        private Masterkey? GetMasterkey(string filepath)
        {
            // Deserialize master key
            var deserialized = JSONSerialization.ReadFileAndDeserialize(
                filepath, typeof(Masterkey)) as Masterkey;
                
            // If import file is not a keyring
            if (deserialized == null)
            {
                var loggerMsg = "Import file cannot be parsed as a master key object.";
                _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                Console.WriteLine(loggerMsg);
            }

            return deserialized;
        }
        
        // Generate master key if it does not exist
        private void InitMasterKey(string serverLink)
        {
            var filepath = GetFilePath("MasterKey.json");
            
            // Check if MasterKey.json exists
            if (File.Exists(filepath))
            {
                var getMasterkey = GetMasterkey(filepath);
                if (getMasterkey != null) masterKey = getMasterkey;
                
                if (!masterKey.Dictionary.ContainsKey(serverLink))
                {
                    GenerateAndSerializeMasterkey(serverLink, filepath);
                }
            }
            else
            {
                GenerateAndSerializeMasterkey(serverLink, filepath);
            }
        }

        private void GenerateAndSerializeMasterkey(string serverLink, string? filepath)
        {
            // Generate master key and serialize
            var newKey = Crypto.GenerateSymmKey();
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            masterKey.Dictionary.Add(serverLink, (pageTitle, newKey));
            JSONSerialization.SerializeAndWriteFile(filepath, masterKey);
        }

        // Returns absolute file path to fuse rootdir as string
        private static string GetRootDirPath()
        {
            const string? filePath = "fuse/directories/rootdir/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var rootDir = Path.Combine(projectDir, filePath);
            return rootDir;
        }

        // Returns absolute file path to keyring jsonfile as string
        public string GetFilePath(string file)
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            return Path.Combine(path, file);
        }

        // Returns root keyring as deserialized json object
        private RootKeyring? GetRootKeyring(string keyringFilePath)
        {
            var existingKeyRing = JSONSerialization.ReadFileAndDeserialize(
                keyringFilePath, typeof(RootKeyring)) as RootKeyring;
            return existingKeyRing;
        }

        // Returns root keyring as deserialized json object with no arguments
        public Keyring? ReadKeyRing()
        {
            var keyringFilePath = GetFilePath("Keyring.json");
            return GetRootKeyring(keyringFilePath);
        }

        // Copies all data from json object into root keyring object
        private void ReadIntoKeyring(RootKeyring rk)
        {
            var readKeyring = ReadKeyRing();
            if (readKeyring != null) rk.CopyFromOtherKeyring(readKeyring);
        }

        // Recursively creates all files and folders from root keyring
        private void CreateFileStructureRecursion(Keyring keyring, string path)
        {
            foreach (var file in keyring.accessFiles)
            {
                File.Create(Path.Combine(path, file.filename)).Dispose();
            }

            foreach (var childKeyRing in keyring.keyrings)
            {
                Directory.CreateDirectory(Path.Combine(path, childKeyRing.name));
                CreateFileStructureRecursion(childKeyRing, Path.Combine(path, childKeyRing.name));
            }
        }

        // Returns the keyringEntry where the new keyring/datafile should be inserted
        private Keyring FindKeyringPath(Keyring keyring, string filePath)
        {
            var filePathSplit = filePath.Split("/");

            // Found keyring entry to insert into
            if (filePathSplit.Length <= 1)
            {
                return keyring;
            }

            var childKeyring = keyring.keyrings.FirstOrDefault(f => f.name.Equals(filePathSplit[0]));
            var newPath = string.Join("/", filePathSplit.Skip(1).ToArray());

            if (childKeyring != null)
            {
                return FindKeyringPath(childKeyring, newPath);
            }

            Keyring intermediateKeyring = new()
            {
                name = filePathSplit[0],
                accessFiles = new ObservableCollection<AccessFile>(),
                keyrings = new ObservableCollection<Keyring>()
            };
            keyring.AddKeyring(intermediateKeyring);
            return FindKeyringPath(intermediateKeyring, newPath);
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filename, string filepath, string serverLink, string pageTitle)
        {
            // var serverLink = "http://localhost/mediawiki/api.php";
            AccessFile accessFile = new(serverLink, pageTitle, filename);
            
            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(_rootKeyring, filepath);
            foundKeyring.AddAccessFile(accessFile);

            AttemptSaveRootKeyring();
        }

        // Add new data file to existing keyring json file
        public void AddNewKeyRing(string filename, string filepath)
        {
            Keyring newKeyring = new()
            {
                name = filename,
                accessFiles = new ObservableCollection<AccessFile>(),
                keyrings = new ObservableCollection<Keyring>()
            };

            // Find the keyring where the new keyring is inserted
            var foundKeyring = FindKeyringPath(_rootKeyring, filepath);
            foundKeyring.AddKeyring(newKeyring);

            // JsonSerializerOptions options = new() {WriteIndented = true};

            // var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            AttemptSaveRootKeyring();
        }

        // Find the datafile with the given name -- better performance if whole filepath is given
        public AccessFile? GetDataFile(string filename, Keyring keyring)
        {
            var dataFile = keyring.accessFiles.FirstOrDefault(f => f.filename.Equals(filename));
            if (dataFile != null)
            {
                return dataFile;
            }

            foreach (var childKeyring in keyring.keyrings)
            {
                var result = GetDataFile(filename, childKeyring);
                if (result != null)
                {
                    return result;
                }
            }

            return null;

            // Linq one-line
            // return dataFile ?? keyring.keyrings.Select(childKeyRing => GetDataFile(filename, childKeyRing)).FirstOrDefault(entry => entry != null);
        }

        // Rename or change location of datafile/keyring in root keyringEntry 
        public void Rename(string oldPath, string newPath)
        {
            var oldKeyring = FindKeyringPath(_rootKeyring, oldPath);
            var newKeyring = FindKeyringPath(_rootKeyring, newPath);

            var oldNameSplit = oldPath.Split("/", 2);
            var oldName = oldNameSplit[^1];
            oldName = oldName.TrimEnd('\0');

            var newNameSplit = newPath.Split("/", 2);
            var newName = newNameSplit[^1];
            newName = newName.TrimEnd('\0');

            // Rename/relocate datafile/keyring
            // Find data file in oldkeyring
            AccessFile? dataFile = oldKeyring.accessFiles.FirstOrDefault(f => f.filename.Equals(oldName));
            if (dataFile != null)
            {
                oldKeyring.accessFiles.Remove(dataFile);
                dataFile.filename = newName;
                newKeyring.AddAccessFile(dataFile);
            }

            // Find keyring in oldkeyring
            var keyring = oldKeyring.keyrings.FirstOrDefault(f => f.name.Equals(oldName));
            if (keyring != null)
            {
                oldKeyring.keyrings.Remove(keyring);
                keyring.name = newName;
                newKeyring.AddKeyring(keyring);
            }
            
            AttemptSaveRootKeyring();
        }
        
        // Remove file from keyring object
        public void RemoveFile(string filePath, string filename)
        {
            // Find the keyring where the data file is located
            var foundKeyring = FindKeyringPath(_rootKeyring, filePath);

            // Remove file or keyring from parent keyring
            var fileToRemove = foundKeyring.accessFiles.FirstOrDefault(f => f.filename.Equals(filename));
            if (fileToRemove != null) foundKeyring.accessFiles.Remove(fileToRemove);

            var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filename));
            if (keyringToRemove != null) foundKeyring.keyrings.Remove(keyringToRemove);

            AttemptSaveRootKeyring();
        }

        // Save root keyring to file
        public void SaveRootKeyring()
        {
            Console.WriteLine("Saving to Keyring.json");
            var path = GetFilePath("Keyring.json");
            JSONSerialization.SerializeAndWriteFile(path, _rootKeyring);
            _rootKeyringWriteTimestamp = DateTime.Now;
        }

        // Save root keyring to file if this has not happened recently
        public void AttemptSaveRootKeyring()
        {
            var timestampNow = DateTime.Now;
            var timestampThreshold = _rootKeyringWriteTimestamp.AddMinutes(5);

            if (timestampNow > timestampThreshold)
            {
                SaveRootKeyring();
            }
            else
            {
                Console.WriteLine("Keyring has been saved recently");
            }
        }

        // Recursively update keyring parent property
        private void UpdateKeyringParentPropertyRecursively(Keyring ke)
        {
            foreach (AccessFile item in ke.accessFiles)
            {
                item.parent = ke;
            }

            foreach (Keyring item in ke.keyrings)
            {
                item.parent = ke;
                UpdateKeyringParentPropertyRecursively(item);
            }
        }

        // Create and return a root keyring containing only all checked entries
        public RootKeyring CreateRootKeyringBasedOnIsChecked()
        {
            RootKeyring outputRootKeyring = new();

            _rootKeyring.AddToOtherKeyringRecursivelyBasedOnIsChecked(outputRootKeyring);
            outputRootKeyring.RemoveEmptyDescendantsRecursively();

            return outputRootKeyring;
        }

        // Same as CreateRootKeyringBasedOnIsChecked() but uses deep copies instead
        private RootKeyring CreateCopyRootKeyringBasedOnIsChecked()
        {
            RootKeyring outputRootKeyring = new();

            _rootKeyring.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(outputRootKeyring);
            outputRootKeyring.RemoveEmptyDescendantsRecursively();

            return outputRootKeyring;
        }
        
        public RootKeyring CreateCopyRootKeyring()
        {
            RootKeyring outputRootKeyring = new();

            _rootKeyring.AddCopiesToOtherKeyringRecursively(outputRootKeyring);
            outputRootKeyring.RemoveEmptyDescendantsRecursively();

            return outputRootKeyring;
        }

        public void ExportRootKeyringBasedOnIsChecked()
        {
            RootKeyring rk = CreateCopyRootKeyringBasedOnIsChecked();

            // Remove information that should not be shared
            rk.PrepareForExportRecursively();

            // Get export path            
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFileName = "KeyringExport.json";
            var keyringFilePath = Path.Combine(path, keyringFileName);

            JSONSerialization.SerializeAndWriteFile(keyringFilePath, rk);
        }

        public void ImportRootKeyring(string importPath)
        {
            // Read RootKeyring from import path and initialise
            var rk = GetRootKeyring(importPath);

            // If import file is not a keyring
            if (rk == null)
            {
                var loggerMsg = "Import file cannot be parsed as a keyring object. Merged aborted.";
                _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                Console.WriteLine(loggerMsg);
                
                return;
            }
            
            // If any of the datafiles in the imported keyring cannot be verified
            var (res, failedDF) = VerifyImportKeyring(rk); 
            if (res == false)
            {
                var location = failedDF!.pageName;
                var loggerMsg = $"Import keyring contains invalid key in data file with pageName='{location}'. " +
                                $"Merge aborted.";
                _manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                Console.WriteLine(loggerMsg);
                return;
            }

            UpdateKeyringParentPropertyRecursively(rk);

            // Sort imported keyring
            rk.SortAllRecursively();
            
            // Merge imported RootKeyring into current RootKeyring
            Console.WriteLine("Merging existing RootKeyring and imported Keyring");
            _rootKeyring.MergeAllEntriesFromOtherKeyring(rk);
            SortAndUpdatePeripherals();
        }

        public void SortAndUpdatePeripherals()
        {
            _rootKeyring.SortAllRecursively();
            Console.WriteLine("Updating mounted directory to reflect changes in RootKeyring");
            CreateFileStructureRecursion(_rootKeyring, GetRootDirPath());

            // Write changes to Keyring.json
            SaveRootKeyring();
        }

        public void RevokeAccess(AccessFile datafile, string latestRevisionID)
        {
            // If no revisions are known to exist for current latest key
            if (datafile.keyList.Last().RevisionStart.Equals("-1")) return;
            
            // Set end revision for current latest key
            datafile.keyList.Last().RevisionEnd = latestRevisionID;
            
            // Create
            AccessFileKey newAccessFileKey = new(datafile.ownerPrivateKey!);
            datafile.keyList.Add(newAccessFileKey);
            
            SaveRootKeyring();
        }

        // Recursively verify all keys of all datafiles in given keyring 
        private (bool, AccessFile?) VerifyImportKeyring(Keyring rk)
        {
            foreach (var df in rk.accessFiles)
            {
                if (!df.VerifyKeys())
                {
                    return (false, df);
                }
            }

            foreach (var ke in rk.keyrings)
            {
                var res = VerifyImportKeyring(ke);

                if (res.Item1 == false)
                {
                    return res;
                }
            }

            return (true, null);
        }
    }
}