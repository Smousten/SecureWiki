using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SecureWiki.Model;

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

        public void UploadNewVersion(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = File.ReadAllText(srcDir);
            Console.WriteLine("Upload plain text: " + plainText);

            if (!plainText.Equals(""))
            {
                // Sign plaintext
                var hash = _manager.SignData(dataFile.privateKey, plainText);
                var hashString = Convert.ToBase64String(hash);
                var encryptedBytes = _manager.EncryptAesStringToBytes(
                    plainText + hashString, dataFile.symmKey, dataFile.iv);

                var encryptedText = Convert.ToBase64String(encryptedBytes);

                var encryptedPagetitleBytes = _manager.EncryptAesStringToBytes(
                    dataFile.filename, dataFile.symmKey, dataFile.iv);
                var encryptedPagetitleString = Convert.ToBase64String(encryptedPagetitleBytes);

                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, 
                    encryptedPagetitleString);
                uploadNewRevision.UploadContent(encryptedText);
            }
        }

        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/directories/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, @filepath);
            return srcDir;
        }

        public void PrintTest(string input)
        {
            Console.WriteLine("WikiHandler printing: " + input);
        }

        public string ReadFile(DataFileEntry dataFile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(MWO, dataFile.pagename);
            latestRevision.GetLatestRevision();
            
            return ReadFile(dataFile, latestRevision.revision.revisionID ?? "-1");
        }

        public string ReadFile(DataFileEntry dataFile, string revid)
        {
            // Check if revision already exists in cache and return output if so
            var cacheResult = _manager.AttemptReadFileFromCache(dataFile.pagename, revid);
            if (cacheResult != null)
            {
                Console.WriteLine("WikiHandler:- ReadFile: Returning extracted content " +
                                  "from cache: pageTitle='{0}', revid='{1}'", dataFile.pagename, revid);
                return cacheResult;
            }

            MediaWikiObjects.PageQuery.PageContent getPageContent = new(MWO, dataFile.pagename, revid);
            var pageContent = getPageContent.GetContent();


            if (pageContent.Equals(""))
            {
                return "File does not exist on server";
            }
            
            var pageContentBytes = Convert.FromBase64String(pageContent);
           
            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, 
                dataFile.symmKey, dataFile.iv);
            var textString = decryptedText.Substring(0, decryptedText.Length - 344);
            var hashString = decryptedText.Substring(decryptedText.Length - 344);
            var hashBytes = Convert.FromBase64String(hashString);

            if (!_manager.VerifyData(dataFile.publicKey, textString, hashBytes))
            {
                Console.WriteLine("Verifying failed...");
                return "Verifying failed...";
            }

            if (textString.Equals(""))
            {
                return "This text is stored securely.";
            }

            getPageContent.revision.content = textString;

            _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);
            
            return textString;
        }
    }
}