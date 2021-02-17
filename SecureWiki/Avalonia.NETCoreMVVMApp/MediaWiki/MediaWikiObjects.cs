using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public abstract class MediaWikiObjects
    {
        private string URL = "http://localhost/mediawiki/api.php";
        private string MWuserID;
        private string MWuserPassword;
        
        public abstract string BuildQuery();
        
        static readonly HttpClient httpClient = new();

        public abstract class PageQuery : MediaWikiObjects
        {
            private string pageID;
            private string pageTitle;

            public class Revision
            {
                public string revisionID;
                public string flags;
                public string timestamp;
                public string user;
                public string size;
                public string content;
            }
        
            public class AllRevisions : PageQuery
            {
                private List<Revision> revisionList = new();
                
                public AllRevisions(string pageTitle)
                {
                    this.pageTitle = pageTitle;
                }

                public List<Revision> GetAllRevisions()
                {

                    string query = BuildQuery();

                    JObject httpResponse = getHttpResponse(query);
                    
                    parseJObject(httpResponse);
                    
                    return revisionList;
                }

                public override string BuildQuery()
                {
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + pageTitle;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=*";
                    queryBody += "&rvlimit=max";
                    queryBody += "&rvprop=ids|flags|timestamp|user|size";
                    queryBody += "&formatversion=2";
                    queryBody += "&format=json";

                    string query = URL + queryBody;
                    
                    return query;
                }

                public void parseJObject(JObject inputJObject)
                {
                    // Print input JObject
                    /*
                    foreach (var pair in inputJObject)
                    {
                        Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
                    }
                    */

                    // Read the relevant fields of each revision entry into a Revision object
                    // and add it to the list of revisions
                    foreach (var token in inputJObject.SelectTokens("query.pages[0].revisions[*]"))
                    {
                        Revision tmp = new Revision();
                        tmp.revisionID = (string) token.SelectToken("revid");
                        tmp.flags = (string) token.SelectToken("flags");
                        tmp.timestamp = (string) token.SelectToken("timestamp");
                        tmp.user = (string) token.SelectToken("user");
                        tmp.size = (string) token.SelectToken("size");

                        revisionList.Add(tmp);
                    }
                    
                    //Console.WriteLine("revCount: " + revCount);
                    //Console.WriteLine("number of entries in revisionList: " + revisionList.Count);
                    //Console.WriteLine("Size of first, last entry: {0}, {1}", revisionList[0].size, revisionList[revisionList.Count-1].size);
                    Console.WriteLine("Loaded {0} entries into revisionList", revisionList.Count);
                }
            }

            public class PageContent : PageQuery
            {
                private Revision revision = new();

                public PageContent(string pageTitle)
                {
                    this.pageTitle = pageTitle;
                }
                
                public string GetContent()
                {
                    string query = BuildQuery();
                    
                    JObject httpResponse = getHttpResponse(query);
                    
                    parseJObject(httpResponse);
                    
                    return revision.content;
                }
                
                public override string BuildQuery()
                {
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + pageTitle;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=*";
                    queryBody += "&rvlimit=1";
                    queryBody += "&rvprop=ids|flags|timestamp|user|size|content";
                    queryBody += "&formatversion=2";
                    queryBody += "&format=json";

                    string query = URL + queryBody;
                    
                    return query;
                }
                
                public void parseJObject(JObject inputJObject)
                {
                    // Print input JObject
                    
                    foreach (var pair in inputJObject)
                    {
                        Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
                    }
                    

                    // Read the relevant fields of each revision entry into a Revision object
                    // and add it to the list of revisions
                    JToken token = inputJObject.SelectToken("query.pages[0].revisions[0]");

                    Console.WriteLine("token.ToString():");
                    Console.WriteLine(token.ToString());
                    
                    Revision rev = new Revision();
                    rev.revisionID = (string) token.SelectToken("revid");
                    rev.flags = (string) token.SelectToken("flags");
                    rev.timestamp = (string) token.SelectToken("timestamp");
                    rev.user = (string) token.SelectToken("user");
                    rev.size = (string) token.SelectToken("size");
                    rev.content = (string) token.SelectToken("slots.main.content");

                    revision = rev;
                    
                    //Console.WriteLine("revCount: " + revCount);
                    //Console.WriteLine("number of entries in revisionList: " + revisionList.Count);
                    //Console.WriteLine("Size of first, last entry: {0}, {1}", revisionList[0].size, revisionList[revisionList.Count-1].size);
                    Console.WriteLine("Length of revision content, first 20 chars: {0}, {1}", 
                        revision.content.Length, revision.content.Substring(0, 20));
                    
                }
            }
        }
        
        public JObject getHttpResponse(string query)
        {
            HttpResponseMessage response = httpClient.GetAsync(query).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            JObject responseJson = JObject.Parse(responseBody);

            return responseJson;
        }

        public virtual void LoginMediaWiki(string username, string password)
        {
            // Build request
            string query = "?action=query";
            query += "&meta=tokens";
            query += "&type=login";
            query += "&format=json";
            
            HttpResponseMessage response = httpClient.GetAsync(URL + query).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("LoginHttpClient:- resonseBody: " + responseBody);
            JObject responseJson = JObject.Parse(responseBody);
            
            string loginToken = (string) responseJson["query"]?["tokens"]?["logintoken"];
            Console.WriteLine("LoginHttpClient:- LoginToken: " + loginToken);
            
            string action = "?action=clientlogin";
            var values = new List<KeyValuePair<string, string>>
            {
                new("format", "json"),
                new("loginreturnurl", "http://example.org"),
                new("logintoken", loginToken),
                new("username", username),
                new("password", password)
            };
            HttpResponseMessage responseClientLogin =
                httpClient.PostAsync(URL + action, new FormUrlEncodedContent(values)).Result;
            string responseBodyClientLogin = responseClientLogin.Content.ReadAsStringAsync().Result;
            Console.WriteLine("LoginHttpClient:- responseBodyClientLogin: " + responseBodyClientLogin);

            MWuserID = username;
            MWuserPassword = password;
        }
        
        
    }
}