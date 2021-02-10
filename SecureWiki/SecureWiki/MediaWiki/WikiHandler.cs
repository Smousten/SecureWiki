using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler
    {
        private readonly string username;
        private readonly string password;

        private const string URL = "http://localhost/mediawiki/api.php";

        // private readonly Wiki wiki;
        static readonly HttpClient client = new HttpClient();

        public WikiHandler(string username, string password)
        {
            this.username = username;
            this.password = password;

            LoginHttpClient();
            GetAllPages();
        }

        private async Task LoginHttpClient()
        {
            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&type=login";
            getData += "&format=json";
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            JObject responseJson = JObject.Parse(responseBody);

            string loginToken = (string) responseJson["query"]?["tokens"]?["logintoken"];

            Console.WriteLine(loginToken);

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
            Console.WriteLine(responseBodyClientLogin);
        }


        public async Task GetAllPages()
        {
            var filepath = "Pyfuse_mediaWiki/srcTest/";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            Console.WriteLine(srcDir);
            string getData = "?action=query";
            getData += "&list=allpages";
            getData += "&apfrom=a";
            getData += "&format=json";
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            JObject responseJson = JObject.Parse(responseBody);
            var pages = responseJson["query"]?["allpages"];
            if (pages != null)
            {
                foreach (var page in pages)
                {
                    var filename = page["title"];
                    Console.WriteLine(filename);
                    var trim = Regex.Replace((string) filename ?? string.Empty, @" ", "");
                    Console.WriteLine(srcDir + trim);
                    using (File.Create(srcDir + trim)) ;
                    await LoadPageContent(srcDir, trim);
                }                
            }
        }

        public async Task CreateNewPage(string filename)
        {
            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&format=json";
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            JObject responseJson = JObject.Parse(responseBody);

            string editToken = (string) responseJson["query"]["tokens"]["csrftoken"];

            string action = "?action=edit";
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("title", filename),
                new KeyValuePair<string, string>("token", editToken),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("text", "")
            };
            HttpResponseMessage responseClientLogin =
                await client.PostAsync(URL + action, new FormUrlEncodedContent(values));
            string responseBodyClientLogin = await responseClientLogin.Content.ReadAsStringAsync();
            Console.WriteLine(responseBodyClientLogin);
        }

        public async Task UploadNewVersion(string filepath)
        {
            // Refactor later - uploadNewVersion and createNewPage use same API
            filepath = filepath.Substring(1);
            filepath = "Pyfuse_mediaWiki/" + filepath;
            var filenameSplit = filepath.Split("/");
            var filename = filenameSplit[filenameSplit.Length - 1];
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            Console.WriteLine(srcDir);
            var plainText = await File.ReadAllTextAsync(srcDir);

            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&format=json";
            HttpResponseMessage response = await client.GetAsync(URL + getData);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseBody);
            JObject responseJson = JObject.Parse(responseBody);

            string editToken = (string) responseJson["query"]?["tokens"]?["csrftoken"];

            string action = "?action=edit";
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("title", filename),
                new KeyValuePair<string, string>("token", editToken),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("text", plainText)
            };
            HttpResponseMessage responseClientLogin =
                await client.PostAsync(URL + action, new FormUrlEncodedContent(values));
            string responseBodyClientLogin = await responseClientLogin.Content.ReadAsStringAsync();
            Console.WriteLine(responseBodyClientLogin);
        }

        public async Task LoadPageContent(string srcDir, string filename)
        {
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
            Console.WriteLine(responseJson);
            // refactor next two lines
            var key = responseJson["query"]?["pages"]?.First?.ToString();
            var pageNr = key?.Split(":", 2)[0];


            // Virker ikke me pageNr som var??????????
            string pageContent = (string) responseJson["query"]?["pages"]?[pageNr]?["revisions"][0]?["slots"]?["main"]?["*"];
            var path = Path.Combine(srcDir, @filename);
            Console.WriteLine(path);
            Console.WriteLine(pageContent);
            await File.WriteAllTextAsync(path, pageContent);
        }
    }
}