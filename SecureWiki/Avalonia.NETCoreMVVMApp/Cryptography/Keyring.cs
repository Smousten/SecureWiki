using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Model;

namespace SecureWiki.Cryptography
{
    public class Keyring
    {
        private readonly Crypto _crypto = new();
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
                Directory.CreateDirectory(Path.Combine(path, childKeyRing.Name));
                CreateFileStructureRecursion(childKeyRing, Path.Combine(path, childKeyRing.Name));
            }
        }

        // Create new keyring.json file with empty keyring object
        private void CreateNewKeyRing(string filepath)
        {
            KeyringEntry newKeyringEntry = new()
            {
                Name = "Root",
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

            var childKeyring = keyring.keyrings.FirstOrDefault(f => f.Name.Equals(filePathSplit[0]));
            var newPath = string.Join("/", filePathSplit.Skip(1).ToArray());

            if (childKeyring != null)
            {
                return FindKeyringPath(childKeyring, newPath);
            }

            KeyringEntry intermediateKeyring = new()
            {
                Name = filePathSplit[0],
                dataFiles = new ObservableCollection<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            keyring.AddKeyring(intermediateKeyring);
            return FindKeyringPath(intermediateKeyring, newPath);
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filepath, string filename)
        {
            var keyringFilePath = GetKeyringFilePath();

            // var existingKeyRing = GetRootKeyring(keyringFilePath);


            var (key, iv) = _crypto.GenerateAESParams();
            var (privateKey, publicKey) = _crypto.GenerateRSAParams();

            // var filenameBytes = _crypto.EncryptAESStringToBytes(filename, key, iv);
            // var encryptedFilename = Convert.ToBase64String(filenameBytes);

            var pagename = GenerateRandomAlphanumericString();

            // DataFileEntry dataFileEntry = new()
            // {
            //     filename = filename,
            //     symmKey = key,
            //     iv = iv,
            //     privateKey = privateKey,
            //     publicKey = publicKey,
            //     revisionNr = "-1",
            //     serverLink = "http://localhost/mediawiki/api.php",
            //     pagename = pagename
            // };

            DataFileKey dataFileKey = new();
            DataFileEntry dataFileEntry = new()
            {
                filename = filename,
                keyList = new List<DataFileKey> {dataFileKey},
                serverLink = "http://localhost/mediawiki/api.php",
                pagename = pagename
            };

            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(rootKeyring, filepath);
            foundKeyring.AddDataFile(dataFileEntry);

            // JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonConvert.SerializeObject(rootKeyring, Formatting.Indented);
            // var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }

        // Add new data file to existing keyring json file
        public void AddNewKeyRing(string filepath, string keyringName)
        {
            var keyringFilePath = GetKeyringFilePath();
            // var existingKeyRing = GetRootKeyring(keyringFilePath);

            KeyringEntry newKeyringEntry = new()
            {
                Name = keyringName,
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

            var oldNameSplit = newPath.Split("/", 2);
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
            var keyring = oldKeyring.keyrings.FirstOrDefault(f => f.Name.Equals(oldName));
            if (keyring != null)
            {
                oldKeyring.keyrings.Remove(keyring);
                keyring.Name = newName;
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

            var keyringToRemove = foundKeyring.keyrings.FirstOrDefault(f => f.Name.Equals(filename));
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
                item.Parent = ke;
            }

            foreach (KeyringEntry item in ke.keyrings)
            {
                item.Parent = ke;
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
            var datafilePath = datafile.Parent != null ? GetDataFilePathLoop(datafile.Parent, filepath) : filepath;

            // Remove Root/ from string
            var datafilePathSplit = datafilePath.Split("Root/", 2);
            return datafilePathSplit[^1];
        }

        private string GetDataFilePathLoop(KeyringEntry keyring, string filepath)
        {
            while (true)
            {
                filepath = keyring.Name + "/" + filepath;
                if (keyring.Parent == null) return filepath;
                keyring = keyring.Parent;
            }
        }

        public void RevokeAccess(DataFileEntry datafile, string latestRevisionID)
        {
            var keyringFilePath = GetKeyringFilePath();
            var existingKeyRing = GetRootKeyring(keyringFilePath);
            var datafilePath = GetDataFileFilePath(datafile);

            // Find the keyring where the data file is located
            var foundKeyring = FindKeyringPath(existingKeyRing, datafilePath);

            var dataFileEntry = foundKeyring.dataFiles.First(e => e.pagename.Equals(datafile.pagename));
            dataFileEntry.keyList.Last().revisionStart = datafile.keyList.Last().revisionStart;
            dataFileEntry.keyList.Last().revisionEnd = latestRevisionID;

            DataFileKey newDataFileKey = new();
            dataFileEntry.keyList.Add(newDataFileKey);

            SerializeAndWriteFile(keyringFilePath, existingKeyRing);
        }

        private static string GenerateRandomAlphanumericString(int length = 20)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            var random = new Random();
            var randomString = new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return randomString;
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