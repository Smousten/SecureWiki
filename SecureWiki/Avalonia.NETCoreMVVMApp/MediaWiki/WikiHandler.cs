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
        private readonly string _url;

        private readonly HttpClient _client;
        private readonly Manager _manager;
        public readonly MediaWikiObjects MWO;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string ip = "localhost")
        {
            _url = "http://" + ip + "/mediawiki/api.php";
            MWO = new MediaWikiObjects(inputClient, username, password, ip);
            _client = inputClient;
            _manager = manager;
        }

        public Revision GetLatestRevision(DataFileEntry dataFile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(MWO, dataFile.pagename);
            latestRevision.GetLatestRevision();
            return latestRevision.revision;
        }

        public void UploadNewVersion(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = File.ReadAllText(srcDir);
            string pageTitle = dataFile.pagename;
            // Console.WriteLine("Upload plain text: " + plainText);

            if (!plainText.Equals(""))
            {
                var latestRevID = _manager.cacheManager.GetLatestRevisionID(pageTitle);
                var rev = GetLatestRevision(dataFile);

                if (rev.revisionID != null && !rev.revisionID.Equals(latestRevID))
                {
                    // TODO: MessageBox

                    Console.WriteLine("This is not the newest revision available, " +
                                      "sure you wanna do this, mate?");
                }

                // Sign plaintext
                var keyList = dataFile.keyList.Last();
                var hash = _manager.SignData(keyList.privateKey!, plainText);
                var hashString = Convert.ToBase64String(hash);
                var encryptedBytes = _manager.EncryptAesStringToBytes(
                    plainText + hashString, keyList.symmKey, keyList.iv);

                var encryptedText = Convert.ToBase64String(encryptedBytes);

                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO,
                    dataFile.pagename);
                uploadNewRevision.UploadContent(encryptedText);

                // Get revision ID of the revision that was just uploaded &
                // set revision start id in latest datafile key if it has not been initialised
                rev = GetLatestRevision(dataFile);
                if (keyList.revisionStart.Equals("-1") && rev.revisionID != null)
                {
                    dataFile.keyList.Last().revisionStart = rev.revisionID;
                }
            }
        }

        public void UploadNewVersionBytes(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            if (!File.Exists(srcDir)) return;
            var plainText = File.ReadAllBytes(srcDir);
            string pageTitle = dataFile.pagename;

            // Console.WriteLine("Upload plain text: " + plainText);

            if (plainText.Length > 0)
            {
                var latestRevID = _manager.cacheManager.GetLatestRevisionID(pageTitle);
                var rev = GetLatestRevision(dataFile);

                if (rev.revisionID != null && !rev.revisionID.Equals(latestRevID))
                {
                    // TODO: MessageBox

                    Console.WriteLine("This is not the newest revision available, " +
                                      "sure you wanna do this, mate?");
                }

                // Sign plaintext
                var keyList = dataFile.keyList.Last();

                var hash = _manager.SignBytes(keyList.privateKey, plainText);

                Console.WriteLine("Uploading bytes of new file version");
                Console.WriteLine("Size of plaintext in bytes: " + plainText.Length);
                Console.WriteLine("Size of hash in bytes: " + hash.Length);

                byte[] rv = new byte[plainText.Length + hash.Length];
                Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
                Buffer.BlockCopy(hash, 0, rv, plainText.Length, hash.Length);

                Console.WriteLine("Upload text bytes to file: " + Encoding.ASCII.GetString(plainText));
                Console.WriteLine("Upload hash bytes to file: " + Encoding.ASCII.GetString(hash));

                var encryptedBytes = _manager.EncryptAesBytesToBytes(
                    rv, keyList.symmKey, keyList.iv);

                var encryptedText = Convert.ToBase64String(encryptedBytes);

                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(MWO,
                    dataFile.pagename);
                uploadNewRevision.UploadContent(encryptedText);

                // Get revision ID of the revision that was just uploaded
                rev = GetLatestRevision(dataFile);
                if (keyList.revisionStart.Equals("-1") && rev.revisionID != null)
                {
                    dataFile.keyList.Last().revisionStart = rev.revisionID;
                }

                Console.WriteLine("Uploading new version of {0}, current startRevID: {1}, current endRevID: {2}",
                    dataFile.filename, dataFile.keyList.Last().revisionStart, dataFile.keyList.Last().revisionEnd);
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

            DataFileKey? keyList = null;
            for (var i = 0; i < dataFile.keyList.Count; i++)
            {
                if (IsValidRevid(dataFile, revid, i))
                {
                    keyList = dataFile.keyList[i];
                    break;
                }
            }

            if (keyList == null) return "Tried to access revision without access";
            Console.WriteLine("Reading file: {0}, current startRevID: {1}, current endRevID: {2}", dataFile.filename,
                keyList.revisionStart, keyList.revisionEnd);

            if (pageContent.Equals(""))
            {
                return "File does not exist on server";
            }

            var pageContentBytes = Convert.FromBase64String(pageContent);

            var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes,
                keyList.symmKey, keyList.iv);
            var textString = decryptedText.Substring(0, decryptedText.Length - 344);
            var hashString = decryptedText.Substring(decryptedText.Length - 344);
            var hashBytes = Convert.FromBase64String(hashString);

            if (!_manager.VerifyData(keyList.publicKey, textString, hashBytes))
            {
                Console.WriteLine("Verifying failed...");
                var revisions = _manager.GetAllRevisions(dataFile.pagename).revisionList;
                return GetLatestValidRevision(dataFile, revisions);
                // return "Access denied...";
            }

            if (textString.Equals(""))
            {
                return "This text is stored securely.";
            }

            getPageContent.revision.content = textString;

            _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);

            return textString;
        }

        private static bool IsValidRevid(DataFileEntry dataFile, string revid, int i)
        {
            return int.Parse(dataFile.keyList[i].revisionStart) <= int.Parse(revid) &&
                   (int.Parse(dataFile.keyList[i].revisionEnd) >= int.Parse(revid) ||
                    (dataFile.keyList[i].revisionEnd.Equals("-1")));
        }

        // Get latest valid revision of wiki page
        // Do not loop though all datafile keys because the list is ordered.
        // Therefore, if X is a newer revision than Y, then Y cannot appear in a later datafile key than X.
        private string GetLatestValidRevision(DataFileEntry datafile, List<Revision> revisions)
        {
            // Initialise iterator to index of last DataFileKey entry
            var iterator = datafile.keyList.Count - 1;

            // 205, 204, 203, 203, 158, 146, 143
            //
            // 144,146;
            // 146,158;
            // 158,203
            // 203,-1;


            for (var i = 0; i < revisions.Count; i++)
            {
                for (var j = iterator; j >= 0; j--)
                {
                    Console.WriteLine(
                        "Try getting lastestvalid revision with revisionID {0} and key startRevID {1} and endRevID {2}",
                        revisions[i].revisionID, datafile.keyList[j].revisionStart, datafile.keyList[j].revisionStart);


                    if (!IsValidRevid(datafile, revisions[i].revisionID, j))
                    {
                        iterator = j;
                        continue;
                    }

                    DataFileKey key = datafile.keyList[j];

                    MediaWikiObjects.PageQuery.PageContent getPageContent =
                        new(MWO, datafile.pagename, revisions[i].revisionID);
                    var pageContent = getPageContent.GetContent();
                    var pageContentBytes = Convert.FromBase64String(pageContent);

                    var decryptedText = _manager.DecryptAesBytesToString(pageContentBytes,
                        key.symmKey, key.iv);
                    var textString = decryptedText.Substring(0, decryptedText.Length - 344);
                    var hashString = decryptedText.Substring(decryptedText.Length - 344);
                    var hashBytes = Convert.FromBase64String(hashString);
                    if (_manager.VerifyData(key.publicKey, textString, hashBytes))
                    {
                        return textString;
                    }

                    break;
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

        public byte[]? ReadFileBytes(DataFileEntry dataFile, string revid)
        {
            // TODO: Add bytes to cache instead of string
            // Check if revision already exists in cache and return output if so
            // var cacheResult = _manager.AttemptReadFileFromCache(dataFile.pagename, revid);
            // if (cacheResult != null)
            // {
            //     Console.WriteLine("WikiHandler:- ReadFile: Returning extracted content " +
            //                       "from cache: pageTitle='{0}', revid='{1}'", dataFile.pagename, revid);
            //     return cacheResult;
            // }

            MediaWikiObjects.PageQuery.PageContent getPageContent = new(MWO, dataFile.pagename, revid);
            var pageContent = getPageContent.GetContent();

            DataFileKey? keyList = null;
            for (var i = 0; i < dataFile.keyList.Count; i++)
            {
                if (IsValidRevid(dataFile, revid, i))
                {
                    keyList = dataFile.keyList[i];
                    break;
                }
            }

            if (keyList == null) return null;

            if (pageContent.Equals(""))
            {
                return null;
            }

            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedTextBytes = _manager.DecryptAesBytesToBytes(pageContentBytes,
                keyList.symmKey, keyList.iv);

            var textBytes = decryptedTextBytes.Take(decryptedTextBytes.Length - 256).ToArray();
            var hashBytes = decryptedTextBytes.Skip(decryptedTextBytes.Length - 256).ToArray();

            Console.WriteLine("Read text bytes from file: " + Encoding.ASCII.GetString(textBytes));
            Console.WriteLine("Read hash bytes from file: " + Encoding.ASCII.GetString(hashBytes));

            if (!_manager.VerifyBytes(keyList.publicKey, textBytes, hashBytes))
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

            // getPageContent.revision.content = Convert.ToBase64String(textBytes);
            // _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);

            return textBytes;
        }
    }
}