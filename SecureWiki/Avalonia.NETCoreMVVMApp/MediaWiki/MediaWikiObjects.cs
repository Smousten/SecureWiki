using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace SecureWiki.MediaWiki
{
    public class Revision
    {
        public string? revisionID { get; set; }
        public string? flags { get; set; }
        public string? timestamp { get; set; }
        public string? user { get; set; }
        public string? size { get; set; }
        public string? content { get; set; }
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
        // private string URL = "http://localhost/mediawiki/api.php";

        private string URL;
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

        public MediaWikiObjects(HttpClient client, string username, string password, string url)
        {
            // URL = "http://" + ip + "/mediawiki/api.php";
            URL = url;
            httpClient = client;
            LoginMediaWiki(username, password);
        }

        public abstract class PageQuery : MediaWikiObjects
        {
            private string pageID;
            public string pageTitle;

            public abstract string BuildQuery();
            public abstract void ParseJObject(JObject inputJObject);

            public PageQuery()
            {
            }

            public PageQuery(MediaWikiObjects source) : base(source)
            {
            }

            public class LatestRevision : PageQuery
            {
                public Revision revision = new();

                public LatestRevision(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }

                public LatestRevision(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public Revision GetLatestRevision()
                {
                    PostRequest();

                    return revision;
                }

                public override string BuildQuery()
                {
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + pageTitle;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=main";
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
                    Revision tmp = new();
                    foreach (var token in inputJObject.SelectTokens("query.pages[0].revisions[*]"))
                    {
                        tmp.revisionID = token.SelectToken("revid")?.ToString();
                        tmp.flags = token.SelectToken("flags")?.ToString();
                        tmp.timestamp = token.SelectToken("timestamp")?.ToString();
                        tmp.user = token.SelectToken("user")?.ToString();
                        tmp.size = token.SelectToken("size")?.ToString();

                    }

                    revision = tmp;

                    //Console.WriteLine("revCount: " + revCount);
                    //Console.WriteLine("number of entries in revisionList: " + revisionList.Count);
                    //Console.WriteLine("Size of first, last entry: {0}, {1}", revisionList[0].size, revisionList[revisionList.Count-1].size);
                    // Console.WriteLine("Loaded {0} entries into revisionList", revisionList.Count);
                }
            }

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
                        Console.WriteLine("RevID: {0}, timestamp: {1}, content: {2}", rev.revisionID, rev.timestamp,
                            rev.content);
                    }
                }

                public override string BuildQuery()
                {
                    // URL does not allow + character, instead encode as hexadecimal
                    var updatedPageTitle = pageTitle.Replace("+", "%2B");
                    
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + updatedPageTitle;
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
                        tmp.revisionID = token.SelectToken("revid")?.ToString();
                        tmp.flags = token.SelectToken("flags")?.ToString();
                        tmp.timestamp = token.SelectToken("timestamp")?.ToString();
                        tmp.user = token.SelectToken("user")?.ToString();
                        tmp.size = token.SelectToken("size")?.ToString();

                        revisionList.Add(tmp);
                    }

                    //Console.WriteLine("revCount: " + revCount);
                    //Console.WriteLine("number of entries in revisionList: " + revisionList.Count);
                    //Console.WriteLine("Size of first, last entry: {0}, {1}", revisionList[0].size, revisionList[revisionList.Count-1].size);
                    // Console.WriteLine("Loaded {0} entries into revisionList", revisionList.Count);
                }
            }

            public class PageContent : PageQuery
            {
                public Revision revision = new();
                private string revID = "-1";
                
                public PageContent(MediaWikiObjects source, string pageTitle, string revisionID) : base(source)
                {
                    this.pageTitle = pageTitle;
                    revID = revisionID;
                }

                // public PageContent(MediaWikiObjects source, string pageTitle) : base(source)
                // {
                //     this.pageTitle = pageTitle;
                // }
                
                public PageContent(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public string GetContent()
                {
                    PostRequest();

                    return revision.content ?? string.Empty;
                }

                public bool PageAlreadyExists()
                {
                    if (!loggedIn)
                    {
                        throw new NotLoggedInException("PostRequest()");
                    }
                    // URL does not allow + character, instead encode as hexadecimal
                    var updatedPageTitle = pageTitle.Replace("+", "%2B");

                    string query = "?action=query";
                    query += "&titles=" + updatedPageTitle;
                    query += "&formatversion=2";
                    query += "&format=json";

                    JObject httpResponse = GetHttpResponse(query);
                    var token = httpResponse.SelectToken("query.pages[0]");
                    return token?.SelectToken("missing") == null;
                }

                public override string BuildQuery()
                {
                    // URL does not allow + character, instead encode as hexadecimal
                    var updatedPageTitle = pageTitle.Replace("+", "%2B");
                    
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + updatedPageTitle;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=*";
                    queryBody += "&rvlimit=1";
                    queryBody += "&rvprop=ids|flags|timestamp|user|size|content";
                    queryBody += "&formatversion=2";
                    queryBody += "&format=json";
                    if (!revID.Equals("-1"))
                    {
                        queryBody += "&rvstartid=" + revID;
                        queryBody += "&rvendid=" + revID;
                    }

                    string query = queryBody;

                    return query;
                }

                public override void ParseJObject(JObject inputJObject)
                {
                    // // Print input JObject
                    //
                    // foreach (var pair in inputJObject)
                    // {
                    //     Console.WriteLine("{0}: {1}", pair.Key, pair.Value);
                    // }


                    // Read the relevant fields of each revision entry into a Revision object
                    // and add it to the list of revisions
                    JToken? token = inputJObject.SelectToken("query.pages[0].revisions[0]");

                    // Console.WriteLine("token.ToString():");
                    if (token != null)
                    {
                        // Console.WriteLine(token.ToString());

                        Revision rev = new();
                        rev.revisionID = token.SelectToken("revid")?.ToString();
                        rev.flags = token.SelectToken("flags")?.ToString();
                        rev.timestamp = token.SelectToken("timestamp")?.ToString();
                        rev.user = token.SelectToken("user")?.ToString();
                        rev.size = token.SelectToken("size")?.ToString();
                        rev.content = token.SelectToken("slots.main.content")?.ToString();

                        revision = rev;
                        if (revision.content != null)
                            Console.WriteLine("Length of revision content, first 20 chars: {0}, {1}",
                                revision.content.Length, revision.content.Substring(0, 20));
                    }

                    //Console.WriteLine("revCount: " + revCount);
                    //Console.WriteLine("number of entries in revisionList: " + revisionList.Count);
                    //Console.WriteLine("Size of first, last entry: {0}, {1}", revisionList[0].size, revisionList[revisionList.Count-1].size);
                    // Console.WriteLine("Length of revision content, first 20 chars: {0}, {1}",
                    //     revision.content.Length, revision.content.Substring(0, 20));
                }
            }


            public void PostRequest()
            {
                if (!loggedIn)
                {
                    throw new NotLoggedInException("PostRequest()");
                }

                string query = BuildQuery();

                JObject httpResponse = GetHttpResponse(query);

                ParseJObject(httpResponse);
            }
        }

        public abstract class PageAction : MediaWikiObjects
        {
            public Action action = new();

            public abstract Action BuildAction();

            public PageAction()
            {
            }

            public PageAction(MediaWikiObjects source) : base(source)
            {
            }

            public class UploadNewRevision : PageAction
            {
                private string? pageID;
                private string pageTitle;
                private JObject? JOTokens;

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

                    PostHttpToServer(action);
                }

                public override Action BuildAction()
                {
                    JOTokens = GetTokens();
                    string? editToken = ExtractToken(JOTokens, "csrftoken");

                    action.action = "?action=edit";
                    action.AddValuePair("title", pageTitle);
                    action.AddValuePair("token", editToken ?? string.Empty);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");
                    action.AddValuePair("text", revision.content);

                    return action;
                }
            }

            public class UndoRevisions : PageAction
            {
                private string? pageID;
                private string pageTitle;
                private JObject? JOTokens;
                public string? undoBeginID;
                public string? undoEndID;

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

                public void UndoRevisionsByID(string startID, string endID)
                {
                    undoBeginID = startID;
                    undoEndID = endID;

                    action = BuildAction();

                    Console.WriteLine("Starting upload: posting to server.");

                    PostHttpToServer(action);
                }

                public override Action BuildAction()
                {
                    JOTokens = GetTokens();
                    string? editToken = ExtractToken(JOTokens, "csrftoken");

                    action.action = "?action=edit";
                    action.AddValuePair("title", pageTitle);
                    action.AddValuePair("token", editToken ?? string.Empty);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");

                    action.AddValuePair("undo", undoBeginID);
                    action.AddValuePair("undoafter", undoEndID);
                    //action.AddValuePair("undo", "14");
                    //action.AddValuePair("undo", "13");

                    return action;
                }
            }

            public class DeleteRevisions : PageAction
            {
                private string? pageID;
                private string pageTitle;
                private JObject? JOTokens;
                public string[]? deleteID;
                public string? IDString;

                private Revision revision = new();

                public DeleteRevisions(MediaWikiObjects source, string pageTitle) : base(source)
                {
                    this.pageTitle = pageTitle;
                }

                public DeleteRevisions(string pageTitle, HttpClient client)
                {
                    this.pageTitle = pageTitle;
                    httpClient = client;
                }

                public void DeleteRevisionsByIDArray(string[] IDArray)
                {
                    IDString = BuildIDString(IDArray);

                    action = BuildAction();

                    Console.WriteLine("Starting DeleteRevisionsByIDArray: posting to server.");

                    PostHttpToServer(action);
                }

                public void DeleteRevisionsByIDString(string idstring)
                {
                    IDString = idstring;

                    action = BuildAction();

                    Console.WriteLine("Starting DeleteRevisionsByIDString: posting to server.");

                    PostHttpToServer(action);
                }

                public override Action BuildAction()
                {
                    JOTokens = GetTokens();
                    string? editToken = ExtractToken(JOTokens, "csrftoken");

                    action.action = "?action=revisiondelete";
                    action.AddValuePair("title", pageTitle);
                    action.AddValuePair("token", editToken ?? string.Empty);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");

                    action.AddValuePair("type", "revision");
                    action.AddValuePair("ids", IDString);
                    action.AddValuePair("hide", "comment|content|user"); // hide all
                    action.AddValuePair("reason", "needed doing");

                    return action;
                }

                private string BuildIDString(string[] inputArr)
                {
                    string output = "";

                    output = inputArr[0];

                    for (int i = 1; i < inputArr.Length; i++)
                    {
                        output += "|";
                        output += inputArr[i];
                    }

                    return output;
                }
            }

            public JObject GetTokens()
            {
                string query = "?action=query";
                query += "&meta=tokens";
                query += "&format=json";

                JObject output = GetHttpResponse(query);

                return output;
            }

            public string? ExtractToken(JObject JOTokens, string tokenName)
            {
                var token = JOTokens["query"]?["tokens"]?[tokenName]?.ToString();

                Console.WriteLine("Extracted token '{0}': {1}", tokenName, token);

                return token;
            }
        }

        public JObject GetHttpResponse(string query)
        {
            if (!loggedIn)
            {
                throw new NotLoggedInException("getHttpResponse");
            }

            // Console.WriteLine("Requesting from server:");
            // Console.WriteLine(query);
            HttpResponseMessage response = httpClient.GetAsync(URL + query).Result;

            string responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("getHttpResponse: " + responseBody);

            response.EnsureSuccessStatusCode();

            JObject responseJson = JObject.Parse(responseBody);


            return responseJson;
        }

        public void PostHttpToServer(Action action)
        {
            try
            {
                HttpResponseMessage httpResponseMessage =
                    httpClient.PostAsync(URL + action.action, new FormUrlEncodedContent(action.values)).Result;
                string httpResponseMessageString = httpResponseMessage.Content.ReadAsStringAsync().Result;
                // Console.WriteLine("postHttpToServer: " + httpResponseMessageString);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to post http request to server {0}", URL);
                Console.WriteLine(e.Message);
            }
        }

        public bool LoginMediaWiki(string username, string password)
        {
            // Build request
            try
            {
                string query = "?action=query";
                query += "&meta=tokens";
                query += "&type=login";
                query += "&format=json";
                HttpResponseMessage response = httpClient.GetAsync(URL + query).Result;
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine("LoginHttpClient:- resonseBody: " + responseBody);
                JObject responseJson = JObject.Parse(responseBody);

                var loginToken = responseJson["query"]?["tokens"]?["logintoken"]?.ToString();
                Console.WriteLine("LoginHttpClient:- LoginToken: " + loginToken);

                string action = "?action=clientlogin";
                if (loginToken == null) return false;
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

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to login at url: " + URL);
                Console.WriteLine(e.Message);
                return false;
            }
        }

        public class NotLoggedInException : Exception
        {
            public NotLoggedInException(string thrownFrom) : base(thrownFrom)
            {
            }
        }
    }
}