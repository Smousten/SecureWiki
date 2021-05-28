using System;
using System.Collections.Generic;
using System.Linq;
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
        public string? action;
        public List<KeyValuePair<string, string>> values = new();

        public void AddValuePair(string key, string value)
        {
            int cnt = values.Count;
            values.Insert(cnt, new KeyValuePair<string, string>(key, value));
        }
    }

    public class Response
    {
        public string actionString;
        public string resultString;
        public string pageidString;
        public string titleString;
        // public string contentmodelString;
        public string oldrevidString;
        public string newrevidString;
        public string newtimestampString;

        public ActionType Action;
        public ResultType Result;

        public enum ActionType
        {
            Edit,
            Unknown
        }
        
        public enum ResultType
        {
            Success,
            Failure
        }

        public Response(string jsondata)
        {
            ParseJSON(jsondata);

            switch (resultString)
            {
                case "Success":
                    Result = ResultType.Success;
                    break;
                default:
                    Result = ResultType.Failure;
                    break;
            }

            switch (actionString)
            {
                case "edit":
                    Action = ActionType.Edit;
                    break;
                default:
                    Action = ActionType.Unknown;
                    break;
            }
        }
        
        public bool ParseJSON(string jsondata)
        {
            JObject jObject = JObject.Parse(jsondata);

            actionString = jObject.Properties().Select(x => x.Name).FirstOrDefault();
            var jAction = jObject[actionString];

            if (jAction == null) return false;

            resultString = (string) jAction["result"];
            pageidString = (string) jAction["pageid"];
            titleString = (string) jAction["title"];
            oldrevidString = (string) jAction["oldrevid"];
            newrevidString = (string) jAction["newrevid"];
            newtimestampString = (string) jAction["newtimestamp"];

            // return true only if all of these variables have been set
            return (resultString ?? pageidString ?? titleString ?? oldrevidString ?? newrevidString ?? newtimestampString) != null;
        }
    }

    public class MediaWikiObject
    {
        private string URL;
        private string MWuserID;
        private string MWuserPassword;

        public JObject JOTokens;
        public string? editToken;


        private HttpClient httpClient;
        public bool loggedIn = false;

        public MediaWikiObject()
        {
        }

        public MediaWikiObject(MediaWikiObject source)
        {
            URL = source.URL;
            MWuserID = source.MWuserID;
            MWuserPassword = source.MWuserPassword;
            httpClient = source.httpClient;
            loggedIn = source.loggedIn;
        }

        public MediaWikiObject(HttpClient client, bool isClientLoggedIn)
        {
            httpClient = client;
            loggedIn = isClientLoggedIn;
        }

        public MediaWikiObject(HttpClient client, string username, string password)
        {
            httpClient = client;
            LoginMediaWiki(username, password);
        }

        public MediaWikiObject(HttpClient client, string username, string password, string url)
        {
            URL = url;
            httpClient = client;
            LoginMediaWiki(username, password);
        }

        public abstract class PageQuery : MediaWikiObject
        {
            private string pageID;
            public string pageName;

            public abstract string BuildQuery();
            public abstract void ParseJObject(JObject inputJObject);

            public PageQuery()
            {
            }

            public PageQuery(MediaWikiObject source) : base(source)
            {
            }

            public class LatestRevision : PageQuery
            {
                public Revision revision = new();

                public LatestRevision(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public LatestRevision(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
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
                    queryBody += "&titles=" + pageName;
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
                }
            }
            
            public class TwoLatestRevisions : PageQuery
            {
                public List<Revision> revisionList = new();

                public TwoLatestRevisions(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public TwoLatestRevisions(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
                    httpClient = client;
                }

                public List<Revision> GetLatestRevisions()
                {
                    PostRequest();

                    return revisionList;
                }

                public override string BuildQuery()
                {
                    // URL does not allow + character, instead encode as hexadecimal
                    var updatedPageName = pageName.Replace("+", "%2B");
                    
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + updatedPageName;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=*";
                    queryBody += "&rvlimit=2";
                    queryBody += "&rvprop=ids|flags|timestamp|user|size";
                    queryBody += "&formatversion=2";
                    queryBody += "&format=json";

                    string query = queryBody;

                    return query;
                }

                public override void ParseJObject(JObject inputJObject)
                {
                    // Read the relevant fields of each revision entry into a Revision object
                    // and add it to the list of revisions
                    foreach (var token in inputJObject.SelectTokens("query.pages[0].revisions[*]"))
                    {
                        Revision tmp = new()
                        {
                            revisionID = token.SelectToken("revid")?.ToString(),
                            flags = token.SelectToken("flags")?.ToString(),
                            timestamp = token.SelectToken("timestamp")?.ToString(),
                            user = token.SelectToken("user")?.ToString(),
                            size = token.SelectToken("size")?.ToString()
                        };

                        revisionList.Add(tmp);
                    }
                }
            }

            public class AllRevisions : PageQuery
            {
                public List<Revision> revisionList = new();

                public AllRevisions(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public AllRevisions(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
                    httpClient = client;
                }

                public List<Revision> GetAllRevisions()
                {
                    PostRequest();

                    return revisionList;
                }

                public List<Revision> GetAllRevisionBefore(string revid)
                {
                    List<Revision> output = new();
                    List<Revision> reverseList = revisionList.ToList();
                    reverseList.Reverse();

                    // Get all revisions older than revid
                    foreach (var item in reverseList)
                    {
                        if (item.revisionID == null || item.revisionID.Equals(revid)) break;
                        output.Add(item);
                    }

                    // Put output list back in correct order
                    output.Reverse();
                    
                    return output;
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
                    var updatedPageName = pageName.Replace("+", "%2B");
                    
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + updatedPageName;
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
                        Revision tmp = new()
                        {
                            revisionID = token.SelectToken("revid")?.ToString(),
                            flags = token.SelectToken("flags")?.ToString(),
                            timestamp = token.SelectToken("timestamp")?.ToString(),
                            user = token.SelectToken("user")?.ToString(),
                            size = token.SelectToken("size")?.ToString()
                        };

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
                
                public PageContent(MediaWikiObject source, string pageName, string revisionID = "-1") : base(source)
                {
                    this.pageName = pageName;
                    revID = revisionID;
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
                    // URL does not allow '+' character, instead encode as hexadecimal
                    var updatedPageName = pageName.Replace("+", "%2B");

                    string query = "?action=query";
                    query += "&titles=" + updatedPageName;
                    query += "&formatversion=2";
                    query += "&format=json";

                    JObject httpResponse = GetHttpResponse(query);
                    var token = httpResponse.SelectToken("query.pages[0]");
                    return token?.SelectToken("missing") == null;
                }

                public override string BuildQuery()
                {
                    // URL does not allow '+' character, instead encode as hexadecimal
                    var updatedPageName = pageName.Replace("+", "%2B");
                    
                    string queryBody = "?action=query";
                    queryBody += "&titles=" + updatedPageName;
                    queryBody += "&prop=revisions";
                    queryBody += "&rvslots=*";
                    queryBody += "&rvlimit=1";
                    queryBody += "&rvprop=ids|flags|timestamp|user|size|content";
                    queryBody += "&formatversion=2";
                    queryBody += "&format=json";
                    if (!revID.Equals("-1")) // If specific revision has been requested
                    {
                        queryBody += "&rvstartid=" + revID;
                        queryBody += "&rvendid=" + revID;
                    }
                    
                    return queryBody;
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
                        // if (revision.content != null)
                        //     Console.WriteLine("Length of revision content, first 20 chars: {0}, {1}",
                        //         revision.content.Length, revision.content.Substring(0, 20));
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

        public abstract class PageAction : MediaWikiObject
        {
            public Action action = new();

            public abstract Action BuildAction();

            public PageAction()
            {
            }

            public PageAction(MediaWikiObject source) : base(source)
            {
                editToken = source.editToken;
            }

            public class UploadNewRevision : PageAction
            {
                private string? pageID;
                private string pageName;
                private JObject? JOTokens;

                private Revision revision = new();

                public UploadNewRevision(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public UploadNewRevision(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
                    httpClient = client;
                }

                public string? UploadContent(string content)
                {
                    revision.content = content;

                    action = BuildAction();

                    Console.WriteLine($"Starting upload: posting to page '{pageName}' at server '{URL}'.");

                    var result = PostHttpToServer(action);

                    // If post fails, try again with a fresh edit token
                    if (result == null)
                    {
                        editToken = null;
                        action = BuildAction();
                        
                        result = PostHttpToServer(action);
                    }

                    Console.WriteLine("Upload finished, response length: " + (result?.Length ?? 0));
                    // Console.WriteLine(result);
                    return result;
                }

                public override Action BuildAction()
                {
                    if (editToken == null)
                    {
                        JOTokens = GetTokens();
                        editToken = ExtractToken(JOTokens, "csrftoken");
                    }

                    action.action = "?action=edit";
                    action.AddValuePair("title", pageName);
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
                private string pageName;
                private JObject? JOTokens;
                public string? undoBeginID;
                public string? undoEndID;

                private Revision revision = new();

                public UndoRevisions(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public UndoRevisions(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
                    httpClient = client;
                }

                public void UndoLastRevisionByID(string ID)
                {
                    UndoRevisionsByID(ID, ID);
                }

                public string? UndoRevisionsByID(string startID, string endID)
                {
                    undoBeginID = startID;
                    undoEndID = endID;

                    action = BuildAction();

                    Console.WriteLine("Starting upload: posting to server.");

                    var result = PostHttpToServer(action);

                    // If post fails, try again with a fresh edit token
                    if (result == null)
                    {
                        editToken = null;
                        action = BuildAction();
                        
                        result = PostHttpToServer(action);
                    }

                    return result;
                }

                public override Action BuildAction()
                {
                    if (editToken == null)
                    {
                        JOTokens = GetTokens();
                        editToken = ExtractToken(JOTokens, "csrftoken");
                    }

                    action.action = "?action=edit";
                    action.AddValuePair("title", pageName);
                    action.AddValuePair("token", editToken ?? string.Empty);
                    action.AddValuePair("format", "json");
                    action.AddValuePair("formatversion", "2");

                    action.AddValuePair("undo", undoBeginID);
                    action.AddValuePair("undoafter", undoEndID);

                    return action;
                }
            }

            public class DeleteRevisions : PageAction
            {
                private string? pageID;
                private string pageName;
                private JObject? JOTokens;
                public string[]? deleteID;
                public string? IDString;

                private Revision revision = new();

                public DeleteRevisions(MediaWikiObject source, string pageName) : base(source)
                {
                    this.pageName = pageName;
                }

                public DeleteRevisions(string pageName, HttpClient client)
                {
                    this.pageName = pageName;
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
                    if (editToken == null)
                    {
                        JOTokens = GetTokens();
                        editToken = ExtractToken(JOTokens, "csrftoken");
                    }

                    action.action = "?action=revisiondelete";
                    action.AddValuePair("title", pageName);
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

            public string? ExtractToken(JObject jOTokens, string tokenName)
            {
                var token = jOTokens["query"]?["tokens"]?[tokenName]?.ToString();

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
            // Console.WriteLine("getHttpResponse: " + responseBody);

            response.EnsureSuccessStatusCode();

            JObject responseJson = JObject.Parse(responseBody);


            return responseJson;
        }

        private string? PostHttpToServer(Action action)
        {
            try
            {
                HttpResponseMessage httpResponseMessage =
                    httpClient.PostAsync(URL + action.action, new FormUrlEncodedContent(action.values!)).Result;
                string httpResponseMessageString = httpResponseMessage.Content.ReadAsStringAsync().Result;
                
                return httpResponseMessageString;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to post http request to server {0}", URL);
                Console.WriteLine(e.Message);
            }

            return null;
        }

        private bool LoginMediaWiki(string username, string password)
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
                // Console.WriteLine("LoginHttpClient:- responseBody: " + responseBody);
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
                // Console.WriteLine("LoginHttpClient:- responseBodyClientLogin: " + responseBodyClientLogin);
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