using System;
using System.Collections.Generic;
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

        private Model.KeyRing ExistingKeyRing(string keyringFilePath)
        {
            var jsonData = File.ReadAllText(keyringFilePath);
            var existingKeyRing = JsonSerializer.Deserialize<Model.KeyRing>(jsonData)
                                  ?? new Model.KeyRing();
            return existingKeyRing;
        }

        private Model.KeyRing? ReadKeyRing()
        {
            var keyringFilePath = KeyringFilePath();
            return ExistingKeyRing(keyringFilePath);
        }

        private void CreateFileStructure(Model.KeyRing keyRing)
        {
            CreateFileStructureRecursion(keyRing, GetRootDirPath());
        }

        private void CreateFileStructureRecursion(Model.KeyRing keyRing, string path)
        {
            foreach (var file in keyRing.dataFiles)
            {
                File.Create(path + file.fileName).Dispose();
            }

            foreach (var childKeyRing in keyRing.keyRings)
            {
                Directory.CreateDirectory(Path.Combine(path, @childKeyRing.name));
                CreateFileStructureRecursion(childKeyRing, Path.Combine(path, @childKeyRing.name));
            }
        }

        // Create new keyRing file with empty keyRing object
        private void CreateNewKeyRing(string filepath)
        {
            Model.KeyRing neyKeyRing = new()
            {
                name = "",
                dataFiles = new List<DataFile>(),
                keyRings = new List<Model.KeyRing>()
            };
            JsonSerializerOptions options = new() {WriteIndented = true};
            var jsonData = JsonSerializer.Serialize(neyKeyRing, options);
            File.WriteAllText(filepath, jsonData);
        }

        private Model.KeyRing findKeyRingPathOld(Model.KeyRing keyRing, string currentPath, string targetPath,
            string filename)
        {
            Console.WriteLine("Current path is: " + currentPath);
            ;
            if ((currentPath + filename).Equals(targetPath))
            {
                Console.WriteLine("Found correct nested keyring with name:" + keyRing.name);
                return keyRing;
            }
        
            foreach (var childKeyRing in keyRing.keyRings)
            {
                return findKeyRingPathOld(childKeyRing, currentPath + childKeyRing.name, targetPath, filename);
            }
        
            return null;
        }

        private Model.KeyRing findKeyRingPath(Model.KeyRing rootKeyring, string filepath)
        {
            var filepathsplit = filepath.Split("/");
            Console.WriteLine("Curretn file path is: " + filepath);
            if (filepathsplit.Length <= 1)
            {
                Console.WriteLine("filepathsplit length is 1");
                return rootKeyring;
            }
            var childKeyring = rootKeyring.keyRings.Find(f => f.name.Equals(filepathsplit[0]));
            var newPath = String.Join("",filepathsplit.Skip(1).ToArray());
            if (childKeyring != null)
            {
                Console.WriteLine("Found childkeyring at: " + childKeyring.name);
                return findKeyRingPath(childKeyring, newPath);
            }
            else
            {
                Console.WriteLine("Child keyring not found");
                Model.KeyRing intermediateKeyring = new()
                {
                    name = filepathsplit[0],
                    dataFiles = new List<DataFile>(),
                    keyRings = new List<Model.KeyRing>()
                };
                rootKeyring.keyRings.Add(intermediateKeyring);
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

            Model.KeyRing newKeyRing = new()
            {
                name = keyname,
                dataFiles = new List<DataFile>(),
                keyRings = new List<Model.KeyRing>()
            };

            // Find the keyring where the new keyring is inserted
            // var correctKeyRing = findKeyRingPath(existingKeyRing, "", filepath, keyname);
            // correctKeyRing.keyRings.Add(newKeyRing);
            var foundKeyring = findKeyRingPath(existingKeyRing, filepath);
            foundKeyring.keyRings.Add(newKeyRing);
            
            JsonSerializerOptions options = new() {WriteIndented = true};

            var jsonData = JsonSerializer.Serialize(existingKeyRing, options);
            File.WriteAllText(keyringFilePath, jsonData);
        }

        // Return datafile with given filename from key ring
        public DataFile? GetDataFile(string filename, Model.KeyRing keyRing)
        {
            var dataFile = keyRing.dataFiles.Find(f => f.fileName.Equals(filename));
            return dataFile ?? keyRing.keyRings.Select(childKeyRing => GetDataFile(filename, childKeyRing)).FirstOrDefault();
        }

        public List<DataFile> GetAllDataFiles()
        {
            var allDataFiles = new List<DataFile>();
            var keyRing = ReadKeyRing();
            allDataFiles = GetAllFilesRecursion(keyRing);
            return allDataFiles;
        }

        // Returns list of all datafiles in keyRing json file
        private List<DataFile> GetAllFilesRecursion(Model.KeyRing keyRing)
        {
            var dataFiles = keyRing.dataFiles;
            if (keyRing.keyRings.Count == 0)
            {
                return dataFiles;
            }

            foreach (var childKeyRing in keyRing.keyRings)
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