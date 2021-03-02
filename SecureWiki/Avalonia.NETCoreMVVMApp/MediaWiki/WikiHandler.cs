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
        public readonly MediaWikiObjects Mwo;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager, string ip)
        {
            _url = "http://" + ip + "/mediawiki/api.php";
            Mwo = new MediaWikiObjects(inputClient, username, password, ip);
            _client = inputClient;
            _manager = manager;
        }

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager)
        {
            Mwo = new MediaWikiObjects(inputClient, username, password);
            _client = inputClient;
            _manager = manager;
        }

        // private async Task LoginHttpClient()
        // {
        //     // Build request
        //     string getData = "?action=query";
        //     getData += "&meta=tokens";
        //     getData += "&type=login";
        //     getData += "&format=json";
        //     
        //     HttpResponseMessage response = await _client.GetAsync(URL + getData);
        //     response.EnsureSuccessStatusCode();
        //     string responseBody = await response.Content.ReadAsStringAsync();
        //     Console.WriteLine("LoginHttpClient:- resonseBody: " + responseBody);
        //     JObject responseJson = JObject.Parse(responseBody);
        //
        //     string loginToken = (string) responseJson["query"]?["tokens"]?["logintoken"];
        //     Console.WriteLine("LoginHttpClient:- LoginToken: " + loginToken);
        //
        //     string action = "?action=clientlogin";
        //     var values = new List<KeyValuePair<string, string>>
        //     {
        //         new KeyValuePair<string, string>("format", "json"),
        //         new KeyValuePair<string, string>("loginreturnurl", "http://example.org"),
        //         new KeyValuePair<string, string>("logintoken", loginToken),
        //         new KeyValuePair<string, string>("username", _username),
        //         new KeyValuePair<string, string>("password", _password)
        //     };
        //     HttpResponseMessage responseClientLogin =
        //         await _client.PostAsync(URL + action, new FormUrlEncodedContent(values));
        //     string responseBodyClientLogin = await responseClientLogin.Content.ReadAsStringAsync();
        //     Console.WriteLine("LoginHttpClient:- responseBodyClientLogin: " + responseBodyClientLogin);
        // }


        // public async Task GetAllPages()
        // {
        //     var filepath = "Pyfuse_mediaWiki/srcTest/";
        //     var currentDir = Directory.GetCurrentDirectory();
        //     var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
        //     var srcDir = Path.Combine(projectDir, @filepath);
        //     
        //     // Build request
        //     string getData = "?action=query";
        //     getData += "&list=allpages";
        //     getData += "&apfrom=w";
        //     getData += "&format=json";
        //     
        //     HttpResponseMessage response = await client.GetAsync(URL + getData);
        //     response.EnsureSuccessStatusCode();
        //     string responseBody = await response.Content.ReadAsStringAsync();
        //     Console.WriteLine("GetAllPages:- resonseBody: " + responseBody);
        //     JObject responseJson = JObject.Parse(responseBody);
        //     var pages = responseJson["query"]?["allpages"];
        //     if (pages != null)
        //     {
        //         foreach (var page in pages)
        //         {
        //             var filename = page["title"];
        //             var trim = Regex.Replace((string) filename ?? string.Empty, @" ", "");
        //             using (File.Create(srcDir + trim)) ;
        //             LoadPageContent(srcDir, trim);
        //         }                
        //     }
        // }

        public async Task UploadNewVersion(string filename, string filepath)
        {
            // Refactor later - uploadNewVersion and createNewPage use same API
            // var filepath = "Pyfuse_mediaWiki/srcTest/" + filename;
            var srcDir = GetRootDir(filepath);
            var plainText = await File.ReadAllTextAsync(srcDir);

            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile != null && !plainText.Equals("")) ;
            {
                // Sign plaintext
                var hash = _manager.SignData(dataFile.privateKey, plainText);
                var hashString = Convert.ToBase64String(hash);
                // var encryptedBytes = _manager.EncryptAesStringToBytes(plainText, dataFile.symmKey, dataFile.iv);
                // var encryptedBytes = _manager.EncryptAesStringToBytes(plainText + BitConverter.ToString(hash),
                //     dataFile.symmKey, dataFile.iv);
                var encryptedBytes = _manager.EncryptAesStringToBytes(plainText + hashString,
                    dataFile.symmKey, dataFile.iv);

                // var encryptedText = BitConverter.ToString(encryptedBytes);
                var encryptedText = Convert.ToBase64String(encryptedBytes);

                var encryptedPagetitleBytes = _manager.EncryptAesStringToBytes(filename, dataFile.symmKey, dataFile.iv);
                // var encryptedPagetitleString = BitConverter.ToString(encryptedPagetitleBytes);
                var encryptedPagetitleString = Convert.ToBase64String(encryptedPagetitleBytes);

                // MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, filename);
                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(Mwo, encryptedPagetitleString);
                uploadNewRevision.UploadContent(encryptedText);
            }

            /*
            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&format=json";
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("UploadNewVersion:- resonseBody: " + responseBody);
            JObject responseJson = JObject.Parse(responseBody);

            string editToken = (string) responseJson["query"]?["tokens"]?["csrftoken"];

            string action = "?action=edit";
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("title", filename),
                new KeyValuePair<string, string>("token", editToken),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("text", encryptedText)
            };
            HttpResponseMessage responseClientLogin =
                await client.PostAsync(URL + action, new FormUrlEncodedContent(values));
            string responseBodyClientLogin = await responseClientLogin.Content.ReadAsStringAsync();
            Console.WriteLine("UploadNewVersion:- responseBodyClientLogin: " + responseBodyClientLogin);
            */
        }

        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/example/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            return srcDir;
        }

        // public void LoadPageContent(string srcDir, string filename)
        // {
        //     /*
        //     // Building request
        //     string getData = "?action=query";
        //     getData += "&titles=" + filename;
        //     getData += "&prop=revisions";
        //     getData += "&rvslots=*";
        //     getData += "&rvprop=content";
        //     getData += "&format=json";
        //     
        //     HttpResponseMessage response = await client.GetAsync(URL + getData);
        //     response.EnsureSuccessStatusCode();
        //     string responseBody = await response.Content.ReadAsStringAsync();
        //     JObject responseJson = JObject.Parse(responseBody);
        //     
        //     var pageContentPair = responseJson.SelectToken("query.pages.*.revisions[0].slots.main")?.Last?.ToString();
        //     var pageContent = pageContentPair?.Split(":", 2);
        //     var trim = pageContent[1].Substring(2, pageContent[1].Length - 3);
        //     */
        //     
        //     MediaWikiObjects.PageQuery.PageContent pageContent = new(MWO, filename);
        //     string content = pageContent.GetContent();
        //     Console.WriteLine("WikiHandler:- LoadPageContent: content: " + content);
        //     
        //     // Remove '-' separators and convert to byte array
        //     String[] arr=content.Split('-');
        //     byte[] array=new byte[arr.Length];
        //     for(int i=0; i<arr.Length; i++) array[i]=Convert.ToByte(arr[i],16);
        //     
        //     // Decrypt TODO: use proper keys
        //     var decryptedText = crypto.DecryptAESBytesToString(array);
        //     Console.WriteLine("WikiHandler:- LoadPageContent: decryptedText: " + decryptedText);
        //
        //     var path = Path.Combine(srcDir, @filename);
        //     File.WriteAllText(path, decryptedText);
        //     
        // }

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
            // var encryptedFilenameString = BitConverter.ToString(encryptedFilenameBytes);
            var encryptedFilenameString = Convert.ToBase64String(encryptedFilenameBytes);

            string getData = "?action=query";
            // getData += "&titles=" + filename;
            getData += "&titles=" + encryptedFilenameString;
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

            // var pageContentBytes = BitConverterStringToBytes(trim);
            var pageContentBytes = Convert.FromBase64String(trim);

            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, dataFile.symmKey, dataFile.iv);
            // var textString = decryptedText.Substring(0,decryptedText.Length-767);
            // var hashString = decryptedText.Substring(decryptedText.Length - 767);
            // var hashBytes = BitConverterStringToBytes(hashString);

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

        // private static byte[] BitConverterStringToBytes(string input)
        // {
        //     string[] arr = input.Split('-');
        //     byte[] array = new byte[arr.Length];
        //     for (int i = 0; i < arr.Length; i++) array[i] = Convert.ToByte(arr[i], 16);
        //     var pageContentBytes = array;
        //     return pageContentBytes;
        // }
    }
}