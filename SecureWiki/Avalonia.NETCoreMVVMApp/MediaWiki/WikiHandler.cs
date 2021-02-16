using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SecureWiki.Cryptography;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler
    {
        private readonly string username;
        private readonly string password;

        private const string URL = "http://localhost/mediawiki/api.php";

        // private readonly Wiki wiki;
        static readonly HttpClient client = new HttpClient();
        private Crypto crypto;

        public WikiHandler(string username, string password)
        {
            this.username = username;
            this.password = password;
            crypto = new Crypto();
            LoginHttpClient();
            GetAllPages();
            
        }

        private async Task LoginHttpClient()
        {
            // Build request
            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&type=login";
            getData += "&format=json";
            
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("LoginHttpClient:- resonseBody: " + responseBody);
            JObject responseJson = JObject.Parse(responseBody);

            string loginToken = (string) responseJson["query"]?["tokens"]?["logintoken"];
            Console.WriteLine("LoginHttpClient:- LoginToken: " + loginToken);

            string action = "?action=clientlogin";
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("loginreturnurl", "http://example.org"),
                new KeyValuePair<string, string>("logintoken", loginToken),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            };
            HttpResponseMessage responseClientLogin =
                await client.PostAsync(URL + action, new FormUrlEncodedContent(values));
            string responseBodyClientLogin = await responseClientLogin.Content.ReadAsStringAsync();
            Console.WriteLine("LoginHttpClient:- responseBodyClientLogin: " + responseBodyClientLogin);
        }


        public async Task GetAllPages()
        {
            var filepath = "Pyfuse_mediaWiki/srcTest/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            
            // Build request
            string getData = "?action=query";
            getData += "&list=allpages";
            getData += "&apfrom=w";
            getData += "&format=json";
            
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("GetAllPages:- resonseBody: " + responseBody);
            JObject responseJson = JObject.Parse(responseBody);
            var pages = responseJson["query"]?["allpages"];
            if (pages != null)
            {
                foreach (var page in pages)
                {
                    var filename = page["title"];
                    var trim = Regex.Replace((string) filename ?? string.Empty, @" ", "");
                    using (File.Create(srcDir + trim)) ;
                    await LoadPageContent(srcDir, trim);
                }                
            }
        }
        
        public async Task UploadNewVersion(string filename)
        {
            // Refactor later - uploadNewVersion and createNewPage use same API
            var filepath = "Pyfuse_mediaWiki/srcTest/" + filename;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            Console.WriteLine("srcDir: " + srcDir);
            var plainText = await File.ReadAllTextAsync(srcDir);
            Console.WriteLine("UploadNewVersion:- plainText:");
            Console.WriteLine(plainText);
            var encryptedBytes = crypto.EncryptAESStringToBytes(plainText);
            var encryptedText = BitConverter.ToString(encryptedBytes);
            Console.WriteLine("Sending Hex to Mediawiki:" + BitConverter.ToString(encryptedBytes));

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
        }

        public async Task LoadPageContent(string srcDir, string filename)
        {
            // Building request
            string getData = "?action=query";
            getData += "&titles=" + filename;
            getData += "&prop=revisions";
            getData += "&rvslots=*";
            getData += "&rvprop=content";
            getData += "&format=json";
            
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject responseJson = JObject.Parse(responseBody);
            
            var pageContentPair = responseJson.SelectToken("query.pages.*.revisions[0].slots.main")?.Last?.ToString();
            var pageContent = pageContentPair?.Split(":", 2);
            var trim = pageContent[1].Substring(2, pageContent[1].Length - 3);
            
            String[] arr=trim.Split('-');
            byte[] array=new byte[arr.Length];
            for(int i=0; i<arr.Length; i++) array[i]=Convert.ToByte(arr[i],16);

            var pageContentBytes = array;
            var decryptedText = crypto.DecryptAESBytesToString(pageContentBytes);

            var path = Path.Combine(srcDir, @filename);
            await File.WriteAllTextAsync(path, decryptedText);
        }

        public void PrintTest(string input)
        {
            Console.WriteLine("WikiHandler printing: " + input);
        }
    }
}