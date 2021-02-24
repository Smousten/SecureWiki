using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler
    {
        private const string URL = "http://localhost/mediawiki/api.php";

        private readonly HttpClient _client;
        private readonly Manager _manager;
        public readonly MediaWikiObjects MWO;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager)
        {
            MWO = new MediaWikiObjects(inputClient, username, password);
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

        public async Task UploadNewVersion(string filename)
        {
            // Refactor later - uploadNewVersion and createNewPage use same API
            // var filepath = "Pyfuse_mediaWiki/srcTest/" + filename;
            var srcDir = GetRootDir(filename);
            var plainText = await File.ReadAllTextAsync(srcDir);

            var keyring = _manager.ReadKeyRing();
            var dataFile = _manager.GetDataFile(filename, keyring);

            if (dataFile != null)
            {
                var encryptedBytes = _manager.EncryptAesStringToBytes(plainText, dataFile.symmKey, dataFile.iv);
                var encryptedText = BitConverter.ToString(encryptedBytes);

                var encryptedPagetitleBytes = _manager.EncryptAesStringToBytes(filename, dataFile.symmKey, dataFile.iv);
                var encryptedPagetitleString = BitConverter.ToString(encryptedPagetitleBytes);

                // MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, filename);
                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, encryptedPagetitleString);
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

        private static string GetRootDir(string filename)
        {
            var filepath = "fuse/example/rootdir/" + filename;
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
            var encryptedFilenameString = BitConverter.ToString(encryptedFilenameBytes);
            
            // TODO: Use MediaWikiObjects to Get Page content. Handle exceptions when the page does not exist on mediaWiki
            // MediaWikiObjects.PageQuery.PageContent pc = new(MWO, encryptedFilenameString);
            // string pageContent = pc.GetContent();
            //
            // string[] arr = pageContent.Split('-');
            // byte[] array = new byte[arr.Length];
            // for (int i = 0; i < arr.Length; i++) array[i] = Convert.ToByte(arr[i], 16);
            // var pageContentBytes = array;
            //
            // var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, dataFile.symmKey, dataFile.iv);
            // return decryptedText;
            //
            string getData = "?action=query";
            // getData += "&titles=" + filename;
            getData += "&titles=" + encryptedFilenameString;
            getData += "&prop=revisions";
            getData += "&rvslots=*";
            getData += "&rvprop=content";
            getData += "&format=json";
            
            HttpResponseMessage response = await _client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject responseJson = JObject.Parse(responseBody);
            Console.Write(responseJson);
            
            var pageContentPair = responseJson.SelectToken("query.pages.*.revisions[0].slots.main")?.Last?.ToString();
            var pageContent = pageContentPair?.Split(":", 2);
            if (pageContent == null) return "This text is stored securely.";
            var trim = pageContent[1].Substring(2, pageContent[1].Length - 3);
            
            string[] arr = trim.Split('-');
            byte[] array = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++) array[i] = Convert.ToByte(arr[i], 16);
            var pageContentBytes = array;
            
            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, dataFile.symmKey, dataFile.iv);
            return decryptedText;
        }
    }
}