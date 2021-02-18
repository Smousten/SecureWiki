using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public class Revision
    {
        public string revisionID;
        public string flags;
        public string timestamp;
        public string user;
        public string size;
        public string content;
    }

    public class Action
    {
        public string action;
        public List<KeyValuePair<string, string>> values = new();

        public void AddValuePair(string key, string value)
        {
            int cnt = values.Count;
            values.Insert(cnt, new KeyValuePair<string, string>(key, value));
        }
    }
    
    public class MediaWikiObjects
    {
        private string URL = "http://localhost/mediawiki/api.php";
        private string MWuserID;
        private string MWuserPassword;

        public JObject JOTokens;

        private HttpClient httpClient;
        public bool loggedIn = false;

        public MediaWikiObjects()
        {
            
        }

        public MediaWikiObjects(MediaWikiObjects source)
        {
            URL = source.URL;
            MWuserID = source.MWuserID;
            MWuserPassword = source.MWuserPassword;
            httpClient = source.httpClient;
            loggedIn = source.loggedIn;
        }

        public MediaWikiObjects(HttpClient client, bool isClientLoggedIn)
        {
            httpClient = client;
            loggedIn = isClientLoggedIn;
        }
        
        public MediaWikiObjects(HttpClient client, string username, string password)
        {
            httpClient = client;
            LoginMediaWiki(username, password);
        }

        public abstract class PageQuery : MediaWikiObjects
        {
            private string pageID;
            private string pageTitle;
                        
            public abstract string BuildQuery();
            public abstract void ParseJObject(JObject inputJObject);
            
            public PageQuery() {}

            public PageQuery(MediaWikiObjects source) : base(source) {}

        
            public class AllRevisions : PageQuery
            {
                public List<Revision> revisionList = new();

                public AllRevisions(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }
                
                public AllRevisions(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public List<Revision> GetAllRevisions()
                {
                    PostRequest();
                    
                    return revisionList;
                }

                public void PrintAllRevisions()
                {
                    foreach (var rev in revisionList)
                    {
                        Console.WriteLine("RevID: {0}, timestamp: {1}, content: {2}", rev.revisionID, rev.timestamp, rev.content);
                    }
                    
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

                    string query = queryBody;
                    
                    return query;
                }

                public override void ParseJObject(JObject inputJObject)
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
                        Revision tmp = new();
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
                
                public PageContent(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }

                public PageContent(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }
                
                public string GetContent()
                {
                    PostRequest();
                    
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

                    string query = queryBody;
                    
                    return query;
                }
                
                public override void ParseJObject(JObject inputJObject)
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
            
             
            public void PostRequest()
            {
                if (!loggedIn)
                {
                    throw new NotLoggedInException("PostRequest()");
                }
                
                string query = BuildQuery();
                    
                JObject httpResponse = getHttpResponse(query);
                    
                ParseJObject(httpResponse);
            }
        }

        public abstract class PageAction : MediaWikiObjects
        {
            public Action action = new();
            
            public abstract Action BuildAction();
            
            public PageAction() {}

            public PageAction(MediaWikiObjects source) : base(source) {}
            
            public class UploadNewRevision : PageAction
            {
                private string pageID;
                private string pageTitle;
                private JObject JOTokens;
                
                private Revision revision = new();

                public UploadNewRevision(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }

                public UploadNewRevision(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public void UploadContent(string content)
                {
                    revision.content = content;

                    JOTokens = GetTokens();

                    action = BuildAction();
                    
                    Console.WriteLine("Starting upload: posting to server.");

                    postHttpToServer(action);
                }
                
                public override Action BuildAction()
                {
                    JOTokens = GetTokens();
                    string editToken = ExtractToken(JOTokens, "csrftoken");
                    
                    action.action = "?action=edit";
                    action.AddValuePair("title", pageTitle);
                    action.AddValuePair("token", editToken);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");
                    action.AddValuePair("text", revision.content);

                    return action;
                }
            }

            public class UndoRevisions : PageAction
            {
                private string pageID;
                private string pageTitle;
                private JObject JOTokens;
                public string undoBeginID;
                public string undoEndID;
                
                private Revision revision = new();

                public UndoRevisions(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }

                public UndoRevisions(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public void UndoLastRevisionByID(string ID)
                {
                    UndoRevisionsByID(ID, ID);
                }

                public void UndoRevisionsByID(string startID,string endID)
                {
                    JOTokens = GetTokens();

                    action = BuildAction();
                    
                    Console.WriteLine("Starting upload: posting to server.");

                    postHttpToServer(action);
                }

                public override Action BuildAction()
                {
                    JOTokens = GetTokens();
                    string editToken = ExtractToken(JOTokens, "csrftoken");
                    
                    action.action = "?action=edit";
                    action.AddValuePair("title", pageTitle);
                    action.AddValuePair("token", editToken);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");

                    //action.AddValuePair("undo", undoBeginID);
                    //action.AddValuePair("undo", undoEndID);
                    action.AddValuePair("undo", "12");
                    action.AddValuePair("undo", "9");

                    return action;
                }
            }
            
            public JObject GetTokens()
            {
                string query = "?action=query";
                query += "&meta=tokens";
                query += "&format=json";

                JObject output = getHttpResponse(query);
                
                return output;
            }

            public string ExtractToken(JObject JOTokens, string tokenName)
            {
                string token = (string) JOTokens["query"]?["tokens"]?[tokenName];

                Console.WriteLine("Extracted token '{0}': {1}", tokenName, token);

                return token;
            }
        }
        
        public JObject getHttpResponse(string query)
        {

            if (!loggedIn)
            {
                throw new NotLoggedInException("getHttpResponse");
            }
            
            Console.WriteLine("Requesting from server:");
            Console.WriteLine(query);
            HttpResponseMessage response = httpClient.GetAsync(URL + query).Result;
            
            string responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("getHttpResponse: " + responseBody);
            
            response.EnsureSuccessStatusCode();
            
            JObject responseJson = JObject.Parse(responseBody);
            

            return responseJson;
        }

        
        public void postHttpToServer(Action action)
        {
            HttpResponseMessage httpResponseMessage = httpClient.PostAsync(URL + action.action, new FormUrlEncodedContent(action.values)).Result;
            string httpResponseMessageString = httpResponseMessage.Content.ReadAsStringAsync().Result;
            Console.WriteLine("postHttpToServer: " + httpResponseMessageString);
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
            loggedIn = true;
        }

        public class NotLoggedInException : Exception
        {
            public NotLoggedInException(string thrownFrom) : base(thrownFrom) {}
        }
        
        
    }
}