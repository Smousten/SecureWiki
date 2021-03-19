using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            // Console.WriteLine("Upload plain text: " + plainText);

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
        
        public void UploadNewVersionBytes(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            if (!File.Exists(srcDir)) return;
            var plainText = File.ReadAllBytes(srcDir);

                // Console.WriteLine("Upload plain text: " + plainText);

            if (plainText.Length > 0)
            {
                // Sign plaintext
                var hash = _manager.SignBytes(dataFile.privateKey, plainText);

                Console.WriteLine("Uploading bytes of new file version");
                Console.WriteLine("Size of plaintext in bytes: " + plainText.Length);
                Console.WriteLine("Size of hash in bytes: " + hash.Length);
                
                byte[] rv = new byte[plainText.Length + hash.Length];
                Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
                Buffer.BlockCopy(hash, 0, rv, plainText.Length, hash.Length);
                
                Console.WriteLine("Upload text bytes to file: " + Encoding.ASCII.GetString(plainText));
                Console.WriteLine("Upload hash bytes to file: " + Encoding.ASCII.GetString(hash));
                
                var encryptedBytes = _manager.EncryptAesBytesToBytes(
                    rv, dataFile.symmKey, dataFile.iv);

                var encryptedText = Convert.ToBase64String(encryptedBytes);

                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO, 
                    dataFile.pagename);
                uploadNewRevision.UploadContent(encryptedText);
            }
        }

        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/directories/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, filepath);
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
                var revisions = _manager.GetAllRevisions(dataFile.pagename).revisionList;
                return GetLatestValidRevision(dataFile, revid, revisions);
            }

            if (textString.Equals(""))
            {
                return "This text is stored securely.";
            }

            getPageContent.revision.content = textString;

            _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);
            
            return textString;
        }

        private string GetLatestValidRevision(DataFileEntry datafile, string revid, List<Revision> revisions)
        {
            for (var i = 0; i < revisions.Count; i++)
            {
                MediaWikiObjects.PageQuery.PageContent getPageContent = new(MWO, datafile.pagename, revid);
                var pageContent = getPageContent.GetContent();
                var pageContentBytes = Convert.FromBase64String(pageContent);
           
                var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes, 
                    datafile.symmKey, datafile.iv);
                var textString = decryptedText.Substring(0, decryptedText.Length - 344);
                var hashString = decryptedText.Substring(decryptedText.Length - 344);
                var hashBytes = Convert.FromBase64String(hashString);
                if (_manager.VerifyData(datafile.publicKey, textString, hashBytes))
                {
                    return textString;
                }
            }

            return "Server does not contain any valid revisions...";
        }

        public byte[]? ReadFileBytes(DataFileEntry datafile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(MWO, datafile.pagename);
            latestRevision.GetLatestRevision();
            
            return ReadFileBytes(datafile, latestRevision.revision.revisionID ?? "-1");
        }

        public byte[]? ReadFileBytes(DataFileEntry datafile, string revid)
        {
            // Check if revision already exists in cache and return output if so
            // var cacheResult = _manager.AttemptReadFileFromCache(dataFile.pagename, revid);
            // if (cacheResult != null)
            // {
            //     Console.WriteLine("WikiHandler:- ReadFile: Returning extracted content " +
            //                       "from cache: pageTitle='{0}', revid='{1}'", dataFile.pagename, revid);
            //     return cacheResult;
            // }

            MediaWikiObjects.PageQuery.PageContent getPageContent = new(MWO, datafile.pagename, revid);
            var pageContent = getPageContent.GetContent();

            if (pageContent.Equals(""))
            {
                return null;
            }
            
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedTextBytes = _manager.DecryptAesBytesToBytes(pageContentBytes, 
                datafile.symmKey, datafile.iv);

            var textBytes = decryptedTextBytes.Take(decryptedTextBytes.Length-256).ToArray();
            var hashBytes = decryptedTextBytes.Skip(decryptedTextBytes.Length-256).ToArray();

            Console.WriteLine("Read text bytes from file: " + Encoding.ASCII.GetString(textBytes));
            Console.WriteLine("Read hash bytes from file: " + Encoding.ASCII.GetString(hashBytes));

            if (!_manager.VerifyBytes(datafile.publicKey, textBytes, hashBytes))
            {
                Console.WriteLine("Verifying failed...");
                return null;
                // var revisions = _manager.GetAllRevisions(dataFile.pagename).revisionList;
                // return GetLatestValidRevision(dataFile, revid, revisions);
            }

            if (!(textBytes.Length > 0))
            {
                return null;
            }

            getPageContent.revision.content = Convert.ToBase64String(textBytes);

            // _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);
            
            return textBytes;
        }
    }
}