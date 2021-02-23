using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using SecureWiki.Model;

namespace SecureWiki.Cryptography
{
    public class KeyRing
    {
        private readonly Crypto crypto = new();

        public void InitKeyring()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            const string? filename = "KeyRing.json";
            var filepath = Path.Combine(path, filename);
            // Check if file exists
            if (File.Exists(filepath))
            {
                // ReadKeyRing();
                // GetAllDataFiles();
            }
            else
            {
                CreateNewKeyRing(filepath);
            }

            var keyring = ReadKeyRing();
            if (keyring.name != null)
            {
                CreateFileStructure(keyring);
            }
        }

        private string GetRootDirPath()
        {
            // Python
            // var filepath = "Pyfuse_mediaWiki/srcTest/";
            // C
            var filepath = "fuse/example/rootdir/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var rootDir = Path.Combine(projectDir, @filepath);
            return rootDir;
        }

        private string KeyringFilePath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFileName = "KeyRing.json";
            var keyringFilePath = Path.Combine(path, keyringFileName);
            return keyringFilePath;
        }

        private Model.KeyringEntry ExistingKeyRing(string keyringFilePath)
        {
            var jsonData = File.ReadAllText(keyringFilePath);
            var existingKeyRing = JsonSerializer.Deserialize<Model.KeyringEntry>(jsonData)
                                  ?? new Model.KeyringEntry();
            Console.WriteLine("existingKeyRing.name: " + existingKeyRing.name);
            return existingKeyRing;
        }

        public Model.KeyringEntry? ReadKeyRing()
        {
            var keyringFilePath = KeyringFilePath();
            return ExistingKeyRing(keyringFilePath);
        }

        private void CreateFileStructure(Model.KeyringEntry keyringEntry)
        {
            CreateFileStructureRecursion(keyringEntry, GetRootDirPath());
        }

        private void CreateFileStructureRecursion(Model.KeyringEntry keyringEntry, string path)
        {
            foreach (var file in keyringEntry.dataFiles)
            {
                File.Create(Path.Combine(path, file.filename)).Dispose();
            }

            foreach (KeyringEntry childKeyRing in keyringEntry.keyrings)
            {
                Directory.CreateDirectory(Path.Combine(path, @childKeyRing.name));
                CreateFileStructureRecursion(childKeyRing, Path.Combine(path, @childKeyRing.name));
            }
        }

        // Create new keyRing file with empty keyRing object
        private void CreateNewKeyRing(string filepath)
        {
            Model.KeyringEntry neyKeyringEntry = new()
            {
                name = "",
                dataFiles = new List<DataFile>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };
            JsonSerializerOptions options = new() {WriteIndented = true};
            var jsonData = JsonSerializer.Serialize(neyKeyringEntry, options);
            File.WriteAllText(filepath, jsonData);
        }

        private Model.KeyringEntry findKeyRingPathOld(Model.KeyringEntry keyringEntry, string currentPath, string targetPath,
            string filename)
        {
            Console.WriteLine("Current path is: " + currentPath);
            ;
            if ((currentPath + filename).Equals(targetPath))
            {
                Console.WriteLine("Found correct nested keyring with name:" + keyringEntry.name);
                return keyringEntry;
            }
        
            foreach (var childKeyRing in keyringEntry.keyrings)
            {
                return findKeyRingPathOld(childKeyRing, currentPath + childKeyRing.name, targetPath, filename);
            }
        
            return null;
        }

        private Model.KeyringEntry findKeyRingPath(Model.KeyringEntry rootKeyring, string filepath)
        {
            var filepathsplit = filepath.Split("/");
            Console.WriteLine("Curretn file path is: " + filepath);
            if (filepathsplit.Length <= 1)
            {
                Console.WriteLine("filepathsplit length is 1");
                return rootKeyring;
            }

            KeyringEntry childKeyring = rootKeyring.keyrings.Where(f => f.name.Equals(filepathsplit[0])).First();
            var newPath = String.Join("",filepathsplit.Skip(1).ToArray());
            if (childKeyring != null)
            {
                Console.WriteLine("Found childkeyring at: " + childKeyring.name);
                return findKeyRingPath(childKeyring, newPath);
            }
            else
            {
                Console.WriteLine("Child keyring not found");
                Model.KeyringEntry intermediateKeyring = new()
                {
                    name = filepathsplit[0],
                    dataFiles = new List<DataFile>(),
                    keyrings = new ObservableCollection<KeyringEntry>()
                };
                rootKeyring.keyrings.Add(intermediateKeyring);
                return findKeyRingPath(intermediateKeyring, newPath);
            }
        }

        // Add new data file to existing keyring json file
        public void AddNewFile(string filepath, string filename)
        {
            var keyringFilePath = KeyringFilePath();

            var existingKeyRing = ExistingKeyRing(keyringFilePath);

            var (key, iv) = crypto.generateAESparams();
            var (privateKey, publicKey) = crypto.generateRSAparams();

            // TODO: Generate encrypted pageName on wikipedia
            DataFile newfile = new()
            {
                filename = filename,
                symmKey = key,
                iv = iv,
                privateKey = privateKey,
                publicKey = publicKey,
                revisionNr = -1,
                serverLink = "http://localhost/mediawiki/api.php",
                pagename = filename
            };

            // Find the keyring where the new datafile is inserted
            var foundKeyring = findKeyRingPath(existingKeyRing, filepath);
            foundKeyring.dataFiles.Add(newfile);

            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }

        public void AddNewKeyRing(string filepath, string keyname)
        {
            var keyringFilePath = KeyringFilePath();
            var existingKeyRing = ExistingKeyRing(keyringFilePath);

            Model.KeyringEntry newKeyringEntry = new()
            {
                name = keyname,
                dataFiles = new List<DataFile>(),
                keyrings = new ObservableCollection<KeyringEntry>()
            };

            // Find the keyring where the new keyring is inserted
            // var correctKeyRing = findKeyRingPath(existingKeyRing, "", filepath, keyname);
            // correctKeyRing.keyRings.Add(newKeyRing);
            var foundKeyring = findKeyRingPath(existingKeyRing, filepath);
            foundKeyring.keyrings.Add(newKeyringEntry);
            
            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }

        // Return datafile with given filename from key ring
        public DataFile? GetDataFile(string filename, Model.KeyringEntry keyringEntry)
        {
            var dataFile = keyringEntry.dataFiles.Find(f => f.filename.Equals(filename));
            return dataFile ?? keyringEntry.keyrings.Select(childKeyRing => GetDataFile(filename, childKeyRing)).FirstOrDefault();
        }

        public List<DataFile> GetAllDataFiles()
        {
            var allDataFiles = new List<DataFile>();
            var keyRing = ReadKeyRing();
            allDataFiles = GetAllFilesRecursion(keyRing);
            return allDataFiles;
        }

        // Returns list of all datafiles in keyRing json file
        private List<DataFile> GetAllFilesRecursion(Model.KeyringEntry keyringEntry)
        {
            var dataFiles = keyringEntry.dataFiles;
            if (keyringEntry.keyrings.Count == 0)
            {
                return dataFiles;
            }

            foreach (var childKeyRing in keyringEntry.keyrings)
            {
                dataFiles.AddRange(GetAllFilesRecursion(childKeyRing));
            }

            return dataFiles;
        }

        public void RenameFile(string filepath, string oldname, string newname)
        {
            
        }
    }
}