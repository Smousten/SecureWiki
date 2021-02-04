using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using LinqToWiki.Generated;
using LinqToWiki.Download;
using RestSharp;
using Newtonsoft.Json.Linq;

namespace SecureWiki
{
    public class WikiHandler
    {
        private readonly string username;
        private readonly string password;
        private const string URL = "http://localhost/mediawiki/api.php";
        // private readonly Wiki wiki;
        //static readonly HttpClient client = new HttpClient();

        public WikiHandler(string username, string password)
        {
            this.username = username;
            this.password = password;
            
            //wiki = new Wiki("LinqToWiki.Samples", "http://localhost", "/mediawiki/api.php");
            //login();
            loginRequest();
        }


        private string getLoginToken()
        {
            string getData = "?action=query";
            getData += "&meta=tokens";
            getData += "&type=login";
            getData += "&format=json";
            HttpWebRequest httpWReq =
                (HttpWebRequest)WebRequest.Create(URL + getData);
            
            HttpWebResponse response = (HttpWebResponse)httpWReq.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            String responseString = reader.ReadToEnd();
            
            Console.WriteLine(responseString);
            
            JObject responseJson = JObject.Parse(responseString);

            return (string) responseJson["query"]["tokens"]["logintoken"];
        }

        public void loginRequest()
        {
            var loginToken = getLoginToken();
            string postData = "?action=login";
            postData += "&lgname=" + username;
            postData += "&lgpassword=" + password;
            postData += "&lgtoken=" + loginToken;
            postData += "&format=json";
            byte[] dataStream = Encoding.UTF8.GetBytes(postData);
            HttpWebRequest httpWReq =
                (HttpWebRequest)WebRequest.Create(URL + postData);

            httpWReq.Method = "POST";
            httpWReq.ContentType = "application/x-www-form-urlencoded";
            httpWReq.ContentLength = dataStream.Length;

            Stream newStream=httpWReq.GetRequestStream();
            // Send the data.
            newStream.Write(dataStream,0,dataStream.Length);
            newStream.Close();

            HttpWebResponse response = (HttpWebResponse)httpWReq.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            String responseString = reader.ReadToEnd();
            
            Console.WriteLine(responseString);
            
            JObject responseJson = JObject.Parse(responseString);
            
        }

        public void getAllPages()
        {
            
        }

        public void createNewPage(string filename)
        {
            /*Tokens(wiki);
            var token = wiki.tokens(new[] { tokenstype.edit }).edittoken;
            Console.WriteLine(token);
            var result = wiki.edit(
                title: filename, text: "",
                contentformat: editcontentformat.application_json, token: token);
            Console.WriteLine(result);*/
        }
    }
}