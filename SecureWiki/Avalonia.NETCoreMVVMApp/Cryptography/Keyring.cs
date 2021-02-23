using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private string GetRootDirPath()
        {
            // Python fuse
            // var filepath = "Pyfuse_mediaWiki/srcTest/";
            // C fuse
            var filepath = "fuse/example/rootdir/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var rootDir = Path.Combine(projectDir, filepath);
            return rootDir;
        }

        // Returns absolute file path to keyring jsonfile as string
        private string GetKeyringFilePath()
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
            var existingKeyRing = JsonSerializer.Deserialize<KeyringEntry>(jsonData)
                                  ?? new KeyringEntry();
            return existingKeyRing;
        }

        // Returns root keyring as deserialized json object with no arguments
        private KeyringEntry ReadKeyRing()
        {
            var keyringFilePath = GetKeyringFilePath();
            return GetRootKeyring(keyringFilePath);
        }

        // Recursively creates all files and folders from root keyring
        private void CreateFileStructureRecursion(KeyringEntry keyringEntry, string path)
        {
            foreach (var file in keyringEntry.dataFiles)
            {
                File.Create(path + file.fileName).Dispose();
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
                keyrings = new List<KeyringEntry>()
            };
            JsonSerializerOptions options = new() {WriteIndented = true};
            var jsonData = JsonSerializer.Serialize(newKeyringEntry, options);
            File.WriteAllText(filepath, jsonData);
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
            var childKeyring = rootKeyring.keyrings.Find(f => f.name.Equals(filePathSplit[0]));
            var newPath = string.Join("",filePathSplit.Skip(1).ToArray());
            if (childKeyring != null)
            {
                return FindKeyringPath(childKeyring, newPath);
            }
            KeyringEntry intermediateKeyring = new()
            {
                name = filePathSplit[0],
                dataFiles = new List<DataFileEntry>(),
                keyrings = new List<KeyringEntry>()
            };
            rootKeyring.keyrings.Add(intermediateKeyring);
            return FindKeyringPath(intermediateKeyring, newPath);
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filepath, string filename)
        {
            var keyringFilePath = GetKeyringFilePath();

            var existingKeyRing = GetRootKeyring(keyringFilePath);

            var (key, iv) = _crypto.generateAESparams();
            var (privateKey, publicKey) = _crypto.generateRSAparams();

            // TODO: Generate encrypted pageName on wikipedia
            DataFileEntry dataFileEntry = new()
            {
                fileName = filename,
                symmKey = key,
                iv = iv,
                privateKey = privateKey,
                publicKey = publicKey,
                revisionNr = -1,
                serverLink = "http://localhost/mediawiki/api.php",
                pageName = filename
            };

            // Find the keyring where the new datafile is inserted
            var foundKeyring = FindKeyringPath(existingKeyRing, filepath);
            foundKeyring.dataFiles.Add(dataFileEntry);

            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
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
                keyrings = new List<KeyringEntry>()
            };

            // Find the keyring where the new keyring is inserted
            var foundKeyring = FindKeyringPath(existingKeyRing, filepath);
            foundKeyring.keyrings.Add(newKeyringEntry);
            
            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }
        
        // Rename or change location of datafile in root keyringEntry 
        public void RenameFile(string oldPath, string newPath)
        {
            var keyringFilePath = GetKeyringFilePath();

            var rootKeyring = GetRootKeyring(keyringFilePath);
            var oldKeyring = FindKeyringPath(rootKeyring, oldPath);
            var newKeyring = FindKeyringPath(rootKeyring, newPath);

            var oldFileNameSplit = newPath.Split("/", 2);
            var oldFileName = oldFileNameSplit[^1];
            oldFileName = oldFileName.TrimEnd('\0');

            var newFileNameSplit = newPath.Split("/", 2);
            var newFileName = newFileNameSplit[^1];
            newFileName = newFileName.TrimEnd('\0');
            
            var oldDataFile = oldKeyring.dataFiles.Find(f => f.fileName.Equals(oldFileName));
            if (oldDataFile != null)
            {
                oldKeyring.dataFiles.Remove(oldDataFile);
                oldDataFile.fileName = newFileName;
                newKeyring.dataFiles.Add(oldDataFile);
            }

            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(rootKeyring, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }
    }
}