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

        public void InitKeyring()
        {
            var filepath = GetKeyringFilePath();
            // Check if file does not exist
            if (!File.Exists(filepath))
            {
                CreateNewKeyRing(filepath);
            }

            var keyring = ReadKeyRing();
            // Create file structure in rootdir
            CreateFileStructureRecursion(keyring, GetRootDirPath());
        }

        // Returns absolute file path to fuse rootdir as string
        private static string GetRootDirPath()
        {
            // Python fuse
            // var filepath = "Pyfuse_mediaWiki/srcTest/";
            // C fuse
            const string? filePath = "fuse/example/rootdir/";
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
        private KeyringEntry GetRootKeyring(string keyringFilePath)
        {
            var jsonData = File.ReadAllText(keyringFilePath);
            // var existingKeyRing = JsonSerializer.Deserialize<KeyringEntry>(jsonData)
            //                       ?? new KeyringEntry();
            var existingKeyRing = JsonConvert.DeserializeObject<KeyringEntry>(jsonData) 
                                  ?? new KeyringEntry();
            return existingKeyRing;
        }

        // Returns root keyring as deserialized json object with no arguments
        public KeyringEntry ReadKeyRing()
        {
            var keyringFilePath = GetKeyringFilePath();
            return GetRootKeyring(keyringFilePath);
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
                name = "",
                dataFiles = new List<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            SerializeAndWriteFile(filepath, newKeyringEntry);
        }


        // Returns the keyringEntry where the new keyring/datafile should be inserted
        private KeyringEntry FindKeyringPath(KeyringEntry rootKeyring, string filePath)
        {
            var filePathSplit = filePath.Split("/");

            // Found keyring entry to insert into
            if (filePathSplit.Length <= 1)
            {
                return rootKeyring;
            }

            var childKeyring = rootKeyring.keyrings.FirstOrDefault(f => f.name.Equals(filePathSplit[0]));
            var newPath = string.Join("/", filePathSplit.Skip(1).ToArray());

            if (childKeyring != null)
            {
                return FindKeyringPath(childKeyring, newPath);
            }

            KeyringEntry intermediateKeyring = new()
            {
                name = filePathSplit[0],
                dataFiles = new List<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            rootKeyring.keyrings.Add(intermediateKeyring);
            return FindKeyringPath(intermediateKeyring, newPath);
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filepath, string filename)
        {
            var keyringFilePath = GetKeyringFilePath();
            var existingKeyRing = GetRootKeyring(keyringFilePath);

            var (key, iv) = _crypto.GenerateAesParams();
            var (privateKey, publicKey) = _crypto.GenerateRsaParams();

            var filenameBytes = _crypto.EncryptAesStringToBytes(filename, key, iv);
            // var encryptedFilename = BitConverter.ToString(filenameBytes);

            var encryptedFilename = Convert.ToBase64String(filenameBytes);
            DataFileEntry dataFileEntry = new()
            {
                filename = filename,
                symmKey = key,
                iv = iv,
                privateKey = privateKey,
                publicKey = publicKey,
                revisionNr = "-1",
                serverLink = "http://localhost/mediawiki/api.php",
                pagename = encryptedFilename
            };

            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(existingKeyRing, filepath);
            foundKeyring.dataFiles.Add(dataFileEntry);

            SerializeAndWriteFile(keyringFilePath, existingKeyRing);
        }

        // Add new data file to existing keyring json file
        public void AddNewKeyRing(string filepath, string keyringName)
        {
            var keyringFilePath = GetKeyringFilePath();
            var existingKeyRing = GetRootKeyring(keyringFilePath);

            KeyringEntry newKeyringEntry = new()
            {
                name = keyringName,
                dataFiles = new List<DataFileEntry>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };

            // Find the keyring where the new keyring is inserted
            var foundKeyring = FindKeyringPath(existingKeyRing, filepath);
            foundKeyring.keyrings.Add(newKeyringEntry);

            SerializeAndWriteFile(keyringFilePath, existingKeyRing);
        }

        // Find the datafile with the given name -- better performance if whole filepath is given
        public DataFileEntry? GetDataFile(string filename, KeyringEntry keyring)
        {
            var dataFile = keyring.dataFiles.Find(f => f.filename.Equals(filename));
            return dataFile ?? keyring.keyrings.Select(childKeyRing => GetDataFile(filename, childKeyRing))
                .FirstOrDefault();
        }

        // Rename or change location of datafile/keyring in root keyringEntry 
        public void Rename(string oldPath, string newPath)
        {
            var keyringFilePath = GetKeyringFilePath();

            var rootKeyring = GetRootKeyring(keyringFilePath);
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
            var dataFile = oldKeyring.dataFiles.Find(f => f.filename.Equals(oldName));
            if (dataFile != null)
            {
                oldKeyring.dataFiles.Remove(dataFile);
                dataFile.filename = newName;
                newKeyring.dataFiles.Add(dataFile);
            }

            // Find keyring in oldkeyring
            var keyring = oldKeyring.keyrings.FirstOrDefault(f => f.name.Equals(oldName));
            if (keyring != null)
            {
                oldKeyring.keyrings.Remove(keyring);
                keyring.name = newName;
                newKeyring.keyrings.Add(keyring);
            }

            SerializeAndWriteFile(keyringFilePath, rootKeyring);
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
            var fileToRemove = foundKeyring.dataFiles.Find(f => f.filename.Equals(filename));
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
    }
}