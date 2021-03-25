using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Model;

namespace SecureWiki.Cryptography
{
    public class Keyring
    {
        public RootKeyring rootKeyring;

        public Keyring(RootKeyring rk)
        {
            rootKeyring = rk;
        }

        public void InitKeyring()
        {
            var filepath = GetKeyringFilePath();
            // Check if file does not exist
            if (!File.Exists(filepath))
            {
                CreateNewKeyRing(filepath);
            }

            // Read Keyring.json into rootKeyring
            ReadIntoKeyring(rootKeyring);
            UpdateKeyringParentPropertyRecursively(rootKeyring);

            CreateFileStructureRecursion(rootKeyring, GetRootDirPath());
        }

        // Returns absolute file path to fuse rootdir as string
        private static string GetRootDirPath()
        {
            // Python fuse
            // var filepath = "Pyfuse_mediaWiki/srcTest/";
            // C fuse
            const string? filePath = "fuse/directories/rootdir/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var rootDir = Path.Combine(projectDir, filePath);
            return rootDir;
        }

        // Returns absolute file path to keyring jsonfile as string
        public string GetKeyringFilePath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFileName = "Keyring.json";
            var keyringFilePath = Path.Combine(path, keyringFileName);
            return keyringFilePath;
        }

        // Returns root keyring as deserialized json object
        private RootKeyring GetRootKeyring(string keyringFilePath)
        {
            var jsonData = File.ReadAllText(keyringFilePath);
            Console.WriteLine("GetRootKeyring:- File.ReadAllText('{0}')", keyringFilePath);
            // Console.WriteLine(jsonData);
            // var existingKeyRing = JsonSerializer.Deserialize<KeyringEntry>(jsonData)
            //                       ?? new KeyringEntry();
            var existingKeyRing = JsonConvert.DeserializeObject<RootKeyring>(jsonData)
                                  ?? new RootKeyring();
            Console.WriteLine("Deserialize passed");
            return existingKeyRing;
        }

        // Returns root keyring as deserialized json object with no arguments
        public KeyringEntry ReadKeyRing()
        {
            var keyringFilePath = GetKeyringFilePath();
            return GetRootKeyring(keyringFilePath);
        }

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

        // Create new keyring.json file with empty keyring object
        private void CreateNewKeyRing(string filepath)
        {
            KeyringEntry newKeyringEntry = new()
            {
                name = "Root",
                dataFiles = new ObservableCollection<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            // JsonSerializerOptions options = new() {WriteIndented = true};
            // var jsonData = JsonSerializer.Serialize(newKeyringEntry, options);
            var jsonData = JsonConvert.SerializeObject(newKeyringEntry, Formatting.Indented);
            File.WriteAllText(filepath, jsonData);
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
        public void AddNewFile(string filename, string filepath, string serverLink)
        {
            var keyringFilePath = GetKeyringFilePath();

            // var serverLink = "http://localhost/mediawiki/api.php";
            DataFileEntry dataFileEntry = new(serverLink, filename);
            
            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(rootKeyring, filepath);
            foundKeyring.AddDataFile(dataFileEntry);

            var jsonData = JsonConvert.SerializeObject(rootKeyring, Formatting.Indented);
            File.WriteAllText(keyringFilePath, jsonData);
        }

        // Add new data file to existing keyring json file
        public void AddNewKeyRing(string filename, string filepath)
        {
            var keyringFilePath = GetKeyringFilePath();
            // var existingKeyRing = GetRootKeyring(keyringFilePath);

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
            SerializeAndWriteFile(keyringFilePath, rootKeyring);
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
            var keyringFilePath = GetKeyringFilePath();

            // var rootKeyring = GetRootKeyring(keyringFilePath);
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
            SerializeAndWriteFile(keyringFilePath, this.rootKeyring);
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


        public void RemoveFile(string filePath, string filename)
        {
            var keyringFilePath = GetKeyringFilePath();
            var existingKeyRing = GetRootKeyring(keyringFilePath);

            // Find the keyring where the data file is located
            var foundKeyring = FindKeyringPath(existingKeyRing, filePath);

            // Remove file or keyring from parent keyring
            var fileToRemove = foundKeyring.dataFiles.FirstOrDefault(f => f.filename.Equals(filename));
            if (fileToRemove != null) foundKeyring.dataFiles.Remove(fileToRemove);

            var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filename));
            if (keyringToRemove != null) foundKeyring.keyrings.Remove(keyringToRemove);

            SerializeAndWriteFile(keyringFilePath, existingKeyRing);
        }

        private static void SerializeAndWriteFile(string filepath, KeyringEntry newKeyringEntry)
        {
            var jsonData = JsonConvert.SerializeObject(newKeyringEntry, Formatting.Indented);
            File.WriteAllText(filepath, jsonData);
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

        public void ImportRootKeyring(string importPath)
        {
            // Read RootKeyring from import path and initialise
            RootKeyring rk = GetRootKeyring(importPath);
            UpdateKeyringParentPropertyRecursively(rk);

            // Merge imported RootKeyring into current RootKeyring
            Console.WriteLine("Merging existing RootKeyring and imported Keyring");
            rootKeyring.MergeAllEntriesFromOtherKeyring(rk);
            Console.WriteLine("Updating mounted directory to reflect changes in RootKeyring");
            CreateFileStructureRecursion(rootKeyring, GetRootDirPath());

            // Write changes to Keyring.json
            // var keyringFilePath = GetKeyringFilePath();
            // SerializeAndWriteFile(keyringFilePath, rootKeyring);
        }

        private string GetDataFileFilePath(DataFileEntry datafile)
        {
            var filepath = datafile.filename;
            var datafilePath = datafile.parent != null ? GetDataFilePathLoop(datafile.parent, filepath) : filepath;

            // Remove Root/ from string
            var datafilePathSplit = datafilePath.Split("Root/", 2);
            return datafilePathSplit[^1];
        }

        private string GetDataFilePathLoop(KeyringEntry keyring, string filepath)
        {
            while (true)
            {
                filepath = keyring.name + "/" + filepath;
                if (keyring.parent == null) return filepath;
                keyring = keyring.parent;
            }
        }

        public void RevokeAccess(DataFileEntry datafile, string latestRevisionID)
        {
            var keyringFilePath = GetKeyringFilePath();
            var existingKeyRing = GetRootKeyring(keyringFilePath);
            var datafilePath = GetDataFileFilePath(datafile);

            // Find the keyring where the data file is located
            var foundKeyring = FindKeyringPath(existingKeyRing, datafilePath);

            var dataFileEntry = foundKeyring.dataFiles.First(e => e.pageName.Equals(datafile.pageName));
            dataFileEntry.keyList.Last().revisionStart = datafile.keyList.Last().revisionStart;
            dataFileEntry.keyList.Last().revisionEnd = latestRevisionID;

            DataFileKey newDataFileKey = new();
            dataFileEntry.keyList.Add(newDataFileKey);

            SerializeAndWriteFile(keyringFilePath, existingKeyRing);
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
        //     SerializeAndWriteFile(keyringFilePath, existingKeyRing);
        // }
    }
}