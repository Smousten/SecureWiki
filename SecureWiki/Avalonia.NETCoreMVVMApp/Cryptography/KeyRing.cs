using System;
using System.IO;
using System.Text.Json;
using SecureWiki.Model;

namespace SecureWiki.Cryptography
{
    public class KeyRing
    {

        private Crypto crypto = new();
        
        public void initKeyring()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var filename = "KeyRing.json";
            var filepath = Path.Combine(path, @filename);
            // Check if file exists
            if (File.Exists(filepath))
            {
                readKeyRing();
            }
            // else
            // create new key ring
        }

        public void readKeyRing()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var filename = "KeyRing.json";
            var filepath = Path.Combine(path, @filename);
            var inputJsonString = File.ReadAllText(filepath);
            var inputFile = JsonSerializer.Deserialize<DataFile>(inputJsonString);
            Console.Write(inputFile.fileName);
        }

        public void addNewKeyRing()
        {
            
        }
        
        
        public void addNewFile(string filename)
        {
            var currentDir = Directory.GetCurrentDirectory();
            var path = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            var keyringFileName = "KeyRing.json";
            var filepath = Path.Combine(path, @keyringFileName);
            
            var AESParams = crypto.generateAESparams();
            var RSAParams = crypto.generateRSAparams();

            Model.DataFile newfile = new Model.DataFile()
            {
                fileName = filename,
                symmKey = AESParams.Key,
                iv = AESParams.IV,
                privateKey = RSAParams.privateKey,
                publicKey = RSAParams.publicKey,
                revisionNr = 1,
                serverLink = "http://localhost/mediawiki/api.php",
                pageName = filename
            };
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.WriteIndented = true;
            var jsonString = JsonSerializer.Serialize(newfile, options);



            JsonSerializer.Serialize(newfile, options);
                
                
            File.WriteAllText(filepath, jsonString);
            
            // generate rsa params
            // generate symm key and iv
            // set servername
            // Add new data file to json object

        }
    }
}