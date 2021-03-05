using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler
    {
        private readonly string _url = "http://localhost/mediawiki/api.php";

        private readonly HttpClient _client;
        private readonly Manager _manager;
        public readonly MediaWikiObjects MWO;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager, string ip)
        {
            _url = "http://" + ip + "/mediawiki/api.php";
            MWO = new MediaWikiObjects(inputClient, username, password, ip);
            _client = inputClient;
            _manager = manager;
        }

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager)
        {
            MWO = new MediaWikiObjects(inputClient, username, password);
            _client = inputClient;
            _manager = manager;
        }

        public void UploadNewVersion(string filename, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = File.ReadAllText(srcDir);
            Console.WriteLine("Upload plain text: " + plainText);

            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile != null && !plainText.Equals(""))
            {
                // Sign plaintext
                var hash = _manager.SignData(dataFile.privateKey, plainText);
                var hashString = Convert.ToBase64String(hash);
                var encryptedBytes = _manager.EncryptAesStringToBytes(plainText + hashString,
                    dataFile.symmKey, dataFile.iv);

                var encryptedText = Convert.ToBase64String(encryptedBytes);

                var encryptedPagetitleBytes = _manager.EncryptAesStringToBytes(filename, dataFile.symmKey, dataFile.iv);
                var encryptedPagetitleString = Convert.ToBase64String(encryptedPagetitleBytes);

                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, encryptedPagetitleString);
                uploadNewRevision.UploadContent(encryptedText);
            }
        }

        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/example/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            return srcDir;
        }

        public void PrintTest(string input)
        {
            Console.WriteLine("WikiHandler printing: " + input);
        }

        public string ReadFile(string filename)
        {
            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile == null) return "This text is stored securely.";
            var encryptedFilenameBytes = _manager.EncryptAesStringToBytes(filename, dataFile.symmKey, dataFile.iv);
            var encryptedFilenameString = Convert.ToBase64String(encryptedFilenameBytes);
            
            // URL does not allow + character, instead encode as hexadecimal
            var encryptedFilenameStringEncoded = encryptedFilenameString.Replace("+", "%2B");
            
            // Check if user has requested old page revision
            MediaWikiObjects.PageQuery.PageContent getPageContent;

            Console.WriteLine("Read manager has requestedRevision for {0} datafiles", _manager.RequestedRevision.Count);
            
            if (_manager.RequestedRevision.ContainsKey(dataFile))
            {
                var revID = _manager.RequestedRevision[dataFile];
                getPageContent = new(MWO, encryptedFilenameStringEncoded, revID);
            }
            else
            {
                getPageContent = new(MWO, encryptedFilenameStringEncoded, "-1");
            }
            var pageContent = getPageContent.GetContent();
            
            // MediaWikiObjects.PageQuery.PageContent getPageContent = new(MWO, encryptedFilenameStringEncoded);
            // var pageContent = getPageContent.GetContent();
            
            if (pageContent.Equals("")) return "File does not exist on server";
            var pageContentBytes = Convert.FromBase64String(pageContent);
            
            // string getData = "?action=query";
            // getData += "&titles=" + encryptedFilenameStringEncoded;
            // getData += "&prop=revisions";
            // getData += "&rvslots=*";
            // getData += "&rvprop=content";
            // getData += "&format=json";
            //
            // HttpResponseMessage response = await _client.GetAsync(_url + getData);
            // response.EnsureSuccessStatusCode();
            // string responseBody = await response.Content.ReadAsStringAsync();
            // JObject responseJson = JObject.Parse(responseBody);
            // Console.WriteLine(responseJson);
            //
            // var pageContentPair = responseJson.SelectToken("query.pages.*.revisions[0].slots.main")?.Last?.ToString();
            // var pageContent = pageContentPair?.Split(":", 2);
            // if (pageContent == null) return "This text is stored securely.";
            
            // var trim = pageContent[1].Substring(2, pageContent[1].Length - 3);

            // var pageContentBytes = Convert.FromBase64String(trim);
                
            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, dataFile.symmKey, dataFile.iv);
            Console.WriteLine("Read file decrypted " + decryptedText);
            var textString = decryptedText.Substring(0, decryptedText.Length - 344);
            var hashString = decryptedText.Substring(decryptedText.Length - 344);
            var hashBytes = Convert.FromBase64String(hashString);

            Console.WriteLine("Read file decrypted text: " + textString);

            if (!_manager.VerifyData(dataFile.publicKey, textString, hashBytes))
            {
                Console.WriteLine("Verifying failed...");
                return "Verifying failed...";
            }

            return textString.Equals("") ? "This text is stored securely." : textString;
        }
    }
}