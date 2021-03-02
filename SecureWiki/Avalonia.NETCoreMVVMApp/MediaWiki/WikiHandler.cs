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

        public async Task UploadNewVersion(string filename, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = await File.ReadAllTextAsync(srcDir);

            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile != null)
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

        public async Task<string> ReadFile(string filename)
        {
            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile == null) return "This text is stored securely.";
            var encryptedFilenameBytes = _manager.EncryptAesStringToBytes(filename, dataFile.symmKey, dataFile.iv);
            var encryptedFilenameString = Convert.ToBase64String(encryptedFilenameBytes);
            
            // URL does not allow + character, instead encode as hexadecimal
            var encryptedFilenameStringEncoded = encryptedFilenameString.Replace("+", "%2B");

            // TODO: use MediaWikiObjects get page content. Fix null pointers
            string getData = "?action=query";
            getData += "&titles=" + encryptedFilenameStringEncoded;
            getData += "&prop=revisions";
            getData += "&rvslots=*";
            getData += "&rvprop=content";
            getData += "&format=json";

            HttpResponseMessage response = await _client.GetAsync(_url + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject responseJson = JObject.Parse(responseBody);
            Console.WriteLine(responseJson);

            var pageContentPair = responseJson.SelectToken("query.pages.*.revisions[0].slots.main")?.Last?.ToString();
            var pageContent = pageContentPair?.Split(":", 2);
            if (pageContent == null) return "This text is stored securely.";
            var trim = pageContent[1].Substring(2, pageContent[1].Length - 3);

            var pageContentBytes = Convert.FromBase64String(trim);

            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, dataFile.symmKey, dataFile.iv);
            var textString = decryptedText.Substring(0, decryptedText.Length - 344);
            var hashString = decryptedText.Substring(decryptedText.Length - 344);
            var hashBytes = Convert.FromBase64String(hashString);

            if (!_manager.VerifyData(dataFile.publicKey, textString, hashBytes))
            {
                Console.WriteLine("Verifying failed...");
                return "Verifying failed...";
            }

            return textString.Equals("") ? "This text is stored securely." : textString;
        }
    }
}