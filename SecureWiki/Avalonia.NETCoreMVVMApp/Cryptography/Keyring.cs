using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWiki.Cryptography
{
    public class Keyring
    {
        public RootKeyring rootKeyring;

        private DateTime rootKeyringWriteTimestamp;

        public Keyring(RootKeyring rk)
        {
            rootKeyring = rk;
        }

        // Initialises keyring object by loading data from json file into rootkeyring object
        // Creates the file structure in root directory with empty files
        public void InitKeyring()
        {
            var filepath = GetKeyringFilePath();
            
            // Check if Keyring.json exists
            if (File.Exists(filepath))
            {
                // Read Keyring.json into rootKeyring
                ReadIntoKeyring(rootKeyring);
                rootKeyring.SortAllRecursively();
                UpdateKeyringParentPropertyRecursively(rootKeyring);
            }

            CreateFileStructureRecursion(rootKeyring, GetRootDirPath());
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
        public string GetKeyringFilePath()
        {
            const string? keyringFileName = "Keyring.json";
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFilePath = Path.Combine(path, keyringFileName);
            return keyringFilePath;
        }

        // Returns root keyring as deserialized json object
        private RootKeyring GetRootKeyring(string keyringFilePath)
        {
            RootKeyring existingKeyRing = (RootKeyring) JSONSerialization.ReadFileAndDeserialize(
                keyringFilePath, typeof(RootKeyring));
            return existingKeyRing;
        }

        // Returns root keyring as deserialized json object with no arguments
        public KeyringEntry ReadKeyRing()
        {
            var keyringFilePath = GetKeyringFilePath();
            return GetRootKeyring(keyringFilePath);
        }

        // Copies all data from json object into root keyring object
        public void ReadIntoKeyring(RootKeyring rk)
        {
            KeyringEntry readKeyring = ReadKeyRing();
            rk.CopyFromOtherKeyring(readKeyring);
        }

        // Recursively creates all files and folders from root keyring
        private void CreateFileStructureRecursion(KeyringEntry keyringEntry, string path)
        {
            foreach (var file in keyringEntry.dataFiles)
            {
                File.Create(Path.Combine(path, file.filename)).Dispose();
            }

            foreach (var childKeyRing in keyringEntry.keyrings)
            {
                Directory.CreateDirectory(Path.Combine(path, childKeyRing.name));
                CreateFileStructureRecursion(childKeyRing, Path.Combine(path, childKeyRing.name));
            }
        }

        // Returns the keyringEntry where the new keyring/datafile should be inserted
        private KeyringEntry FindKeyringPath(KeyringEntry keyring, string filePath)
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

            KeyringEntry intermediateKeyring = new()
            {
                name = filePathSplit[0],
                dataFiles = new ObservableCollection<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            keyring.AddKeyring(intermediateKeyring);
            return FindKeyringPath(intermediateKeyring, newPath);
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filename, string filepath, string serverLink, string pageTitle)
        {
            // var serverLink = "http://localhost/mediawiki/api.php";
            DataFileEntry dataFileEntry = new(serverLink, pageTitle, filename);
            
            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(rootKeyring, filepath);
            foundKeyring.AddDataFile(dataFileEntry);

            AttemptSaveRootKeyring();
        }

        // Add new data file to existing keyring json file
        public void AddNewKeyRing(string filename, string filepath)
        {
            KeyringEntry newKeyringEntry = new()
            {
                name = filename,
                dataFiles = new ObservableCollection<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };

            // Find the keyring where the new keyring is inserted
            var foundKeyring = FindKeyringPath(rootKeyring, filepath);
            foundKeyring.AddKeyring(newKeyringEntry);

            // JsonSerializerOptions options = new() {WriteIndented = true};

            // var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            AttemptSaveRootKeyring();
        }

        // Find the datafile with the given name -- better performance if whole filepath is given
        public DataFileEntry? GetDataFile(string filename, KeyringEntry keyring)
        {
            var dataFile = keyring.dataFiles.FirstOrDefault(f => f.filename.Equals(filename));
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
            var oldKeyring = FindKeyringPath(rootKeyring, oldPath);
            var newKeyring = FindKeyringPath(rootKeyring, newPath);

            var oldNameSplit = oldPath.Split("/", 2);
            var oldName = oldNameSplit[^1];
            oldName = oldName.TrimEnd('\0');

            var newNameSplit = newPath.Split("/", 2);
            var newName = newNameSplit[^1];
            newName = newName.TrimEnd('\0');

            // Rename/relocate datafile/keyring
            // Find data file in oldkeyring
            DataFileEntry? dataFile = oldKeyring.dataFiles.FirstOrDefault(f => f.filename.Equals(oldName));
            if (dataFile != null)
            {
                oldKeyring.dataFiles.Remove(dataFile);
                dataFile.filename = newName;
                newKeyring.AddDataFile(dataFile);
            }

            // Find keyring in oldkeyring
            var keyring = oldKeyring.keyrings.FirstOrDefault(f => f.name.Equals(oldName));
            if (keyring != null)
            {
                oldKeyring.keyrings.Remove(keyring);
                keyring.name = newName;
                newKeyring.AddKeyring(keyring);
            }

            // JsonSerializerOptions options = new() {WriteIndented = true};

            // var jsonData = JsonSerializer.Serialize(rootKeyring, options);
            AttemptSaveRootKeyring();
        }

        // public void RemoveFile(string filePath, string filename, string type)
        // {
        //     var keyringFilePath = GetKeyringFilePath();
        //     var existingKeyRing = GetRootKeyring(keyringFilePath);
        //
        //     // Find the keyring where the data file is located
        //     var foundKeyring = FindKeyringPath(existingKeyRing, filePath);
        //
        //     if (type.Equals("file"))
        //     {
        //         var fileToRemove = foundKeyring.dataFiles.Find(f => f.filename.Equals(filename));
        //         if (fileToRemove != null) foundKeyring.dataFiles.Remove(fileToRemove);
        //     }
        //     else
        //     {
        //         var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filename));
        //         if (keyringToRemove != null) foundKeyring.keyrings.Remove(keyringToRemove);
        //     }
        //
        //     JsonSerializerOptions options = new() {WriteIndented = true};
        //
        //     var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
        //     File.WriteAllText(keyringFilePath, jsonData);
        // }

        // Remove file from keyring object
        public void RemoveFile(string filePath, string filename)
        {
            // Find the keyring where the data file is located
            var foundKeyring = FindKeyringPath(rootKeyring, filePath);

            // Remove file or keyring from parent keyring
            var fileToRemove = foundKeyring.dataFiles.FirstOrDefault(f => f.filename.Equals(filename));
            if (fileToRemove != null) foundKeyring.dataFiles.Remove(fileToRemove);

            var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filename));
            if (keyringToRemove != null) foundKeyring.keyrings.Remove(keyringToRemove);

            AttemptSaveRootKeyring();
        }

        public void SerializeAndWriteFile(string filepath, KeyringEntry keyring)
        {
            Utilities.JSONSerialization.SerializeAndWriteFile(filepath, keyring);
            // var jsonData = JsonConvert.SerializeObject(newKeyringEntry, Formatting.Indented);
            // File.WriteAllText(filepath, jsonData);
        }

        public void SaveRootKeyring()
        {
            Console.WriteLine("Saving to Keyring.json");
            var path = GetKeyringFilePath();
            SerializeAndWriteFile(path, rootKeyring);
            rootKeyringWriteTimestamp = DateTime.Now;
        }

        public void AttemptSaveRootKeyring()
        {
            var timestampNow = DateTime.Now;
            var timestampThreshold = rootKeyringWriteTimestamp.AddMinutes(10);;

            if (timestampNow > timestampThreshold)
            {
                SaveRootKeyring();
            }
            else
            {
                Console.WriteLine("Keyring has been saved recently");
            }
        }

        private void UpdateKeyringParentPropertyRecursively(KeyringEntry ke)
        {
            foreach (DataFileEntry item in ke.dataFiles)
            {
                item.parent = ke;
            }

            foreach (KeyringEntry item in ke.keyrings)
            {
                item.parent = ke;
                UpdateKeyringParentPropertyRecursively(item);
            }
        }

        public RootKeyring CreateRootKeyringBasedOnIsChecked()
        {
            RootKeyring outputRootKeyring = new();

            rootKeyring.AddToOtherKeyringRecursivelyBasedOnIsChecked(outputRootKeyring);

            return outputRootKeyring;
        }

        public void ExportRootKeyringBasedOnIsChecked()
        {
            RootKeyring rk = CreateRootKeyringBasedOnIsChecked();

            rk.RemoveEmptyDescendantsRecursively();
            rk.PrepareForExportRecursively();

            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFileName = "KeyringExport.json";
            var keyringFilePath = Path.Combine(path, keyringFileName);

            var filepath = keyringFilePath;

            SerializeAndWriteFile(filepath, rk);
        }
        
        public List<DataFileEntry> GetListOfAllCheckedDataFiles()
        {
            RootKeyring rk = CreateRootKeyringBasedOnIsChecked();

            rk.RemoveEmptyDescendantsRecursively();

            var dataFileList = rk.GetAllDescendantDataFileEntries();

            return dataFileList;
        }

        public void ImportRootKeyring(string importPath)
        {
            // Read RootKeyring from import path and initialise
            RootKeyring rk = GetRootKeyring(importPath);

            if (!VerifyImportKeyring(rk))
            {
                Console.WriteLine("Import keyring contains invalid key");
                return;
            }

            UpdateKeyringParentPropertyRecursively(rk);

            // Sort imported keyring
            rk.SortAllRecursively();
            
            // Merge imported RootKeyring into current RootKeyring
            Console.WriteLine("Merging existing RootKeyring and imported Keyring");
            rootKeyring.MergeAllEntriesFromOtherKeyring(rk);
            rootKeyring.SortAllRecursively();
            Console.WriteLine("Updating mounted directory to reflect changes in RootKeyring");
            CreateFileStructureRecursion(rootKeyring, GetRootDirPath());

            // Write changes to Keyring.json
            SaveRootKeyring();
        }

        // private string GetDataFileFilePath(DataFileEntry datafile)
        // {
        //     var filepath = datafile.filename;
        //     var datafilePath = datafile.parent != null ? GetDataFilePathLoop(datafile.parent, filepath) : filepath;
        //
        //     // Remove Root/ from string
        //     var datafilePathSplit = datafilePath.Split("Root/", 2);
        //     return datafilePathSplit[^1];
        // }
        //
        // private string GetDataFilePathLoop(KeyringEntry keyring, string filepath)
        // {
        //     while (true)
        //     {
        //         filepath = keyring.name + "/" + filepath;
        //         if (keyring.parent == null) return filepath;
        //         keyring = keyring.parent;
        //     }
        // }

        public void RevokeAccess(DataFileEntry datafile, string latestRevisionID)
        {
            if (datafile.keyList.Last().RevisionStart.Equals("-1")) return;
            
            datafile.keyList.Last().RevisionEnd = latestRevisionID;
            
            DataFileKey newDataFileKey = new();
            newDataFileKey.SignKey(datafile.ownerPrivateKey!);
            datafile.keyList.Add(newDataFileKey);
            
            AttemptSaveRootKeyring();
        }

        private bool VerifyImportKeyring(KeyringEntry rk)
        {
            if (rk.dataFiles.Any(file => !file.VerifyKeys()))
            {
                return false;
            }

            return rk.keyrings.Select(childKeyring => VerifyImportKeyring(childKeyring)).All(res => res);
        }

        // public void SetKeyStartRevision(DataFileEntry datafile, string revisionID)
        // {
        //     var keyringFilePath = GetKeyringFilePath();
        //     var existingKeyRing = GetRootKeyring(keyringFilePath);
        //     var datafilePath = GetDataFileFilePath(datafile);
        //     // Find the keyring where the data file is located
        //     var foundKeyring = FindKeyringPath(existingKeyRing, datafilePath);
        //
        //     var dataFileEntry = foundKeyring.dataFiles.First(e => e.pagename.Equals(datafile.pagename));
        //     dataFileEntry.keyList.Last().revisionStart = revisionID;
        //     AttemptSaveRootKeyring();
        // }
    }
}