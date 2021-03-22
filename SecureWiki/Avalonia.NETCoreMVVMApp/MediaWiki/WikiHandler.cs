using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using SecureWiki.Model;
using SecureWiki.Views;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler
    {
        private readonly Manager _manager;
        public readonly MediaWikiObjects MWO;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string ip = "localhost")
        {
            MWO = new MediaWikiObjects(inputClient, username, password, ip);
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
            if (!File.Exists(srcDir)) return;
            var plainText = File.ReadAllBytes(srcDir);
            string pageTitle = dataFile.pagename;

            if (plainText.Length > 0)
            {
                var latestRevID = _manager.cacheManager.GetLatestRevisionID(pageTitle);
                var rev = GetLatestRevision(dataFile);

                if (rev.revisionID != null && !rev.revisionID.Equals(latestRevID))
                {
                    string warningString = "This is not the newest revision available, " +
                                           "sure you wanna do this, mate?" + 
                                           "\nUploaded: " + rev.timestamp + 
                                           "\nBy user: " + rev.user +
                                           "\nContent size: " + rev.size;
                    
                    var msgBoxOutput = _manager.ShowMessageBox("Warning!", warningString);

                    Console.WriteLine("msgboxoutput = " + msgBoxOutput);

                    if (msgBoxOutput == MessageBox.MessageBoxResult.Cancel)
                    {
                        Console.WriteLine("Upload cancelled");
                        return;
                    }
                }
                // Sign plaintext
                var keyList = dataFile.keyList.Last();

                var hash = _manager.SignData(keyList.privateKey, plainText);

                byte[] rv = new byte[plainText.Length + hash.Length];
                Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
                Buffer.BlockCopy(hash, 0, rv, plainText.Length, hash.Length);
                
                var encryptedBytes = _manager.Encrypt(
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

        private static bool IsValidRevid(DataFileEntry dataFile, string revid, int i)
        {
            return int.Parse(dataFile.keyList[i].revisionStart) <= int.Parse(revid) &&
                   (int.Parse(dataFile.keyList[i].revisionEnd) >= int.Parse(revid) ||
                    (dataFile.keyList[i].revisionEnd.Equals("-1")));
        }

        // Get latest valid revision of wiki page
        // Do not loop though all datafile keys because the list is ordered.
        // Therefore, if X is a newer revision than Y, then Y cannot appear in a later datafile key than X.
        private byte[]? GetLatestValidRevision(DataFileEntry datafile, List<Revision> revisions)
        {
            // Initialise iterator to index of last DataFileKey entry
            var iterator = datafile.keyList.Count - 1;
            for (var i = 0; i < revisions.Count; i++)
            {
                for (var j = iterator; j >= 0; j--)
                {
                    Console.WriteLine(
                        "Try getting lastestvalid revision with revisionID {0} and key startRevID {1} and endRevID {2}",
                        revisions[i].revisionID, datafile.keyList[j].revisionStart, datafile.keyList[j].revisionEnd);
                    
                    if (!IsValidRevid(datafile, revisions[i].revisionID, j))
                    {
                        iterator = j;
                        continue;
                    }

                    DataFileKey key = datafile.keyList[j];

                    MediaWikiObjects.PageQuery.PageContent getPageContent =
                        new(MWO, datafile.pagename, revisions[i].revisionID);
                    var pageContentBytes = Convert.FromBase64String(getPageContent.GetContent());
                    var decryptedTextBytes = _manager.Decrypt(pageContentBytes,
                        key.symmKey, key.iv);

                    var textBytes = decryptedTextBytes.Take(decryptedTextBytes.Length - 256).ToArray();
                    var hashBytes = decryptedTextBytes.Skip(decryptedTextBytes.Length - 256).ToArray();

                    if (_manager.VerifyData(key.publicKey, textBytes, hashBytes))
                    {
                        return textBytes;
                    }

                    break;
                }
            }

            return null;
        }

        public byte[]? ReadFile(DataFileEntry datafile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(MWO, datafile.pagename);
            latestRevision.GetLatestRevision();

            return ReadFile(datafile, latestRevision.revision.revisionID ?? "-1");
        }

        public byte[]? ReadFile(DataFileEntry dataFile, string revid)
        {
            // Check if revision already exists in cache and return output if so
            var cacheResult = _manager.AttemptReadFileFromCache(dataFile.pagename, revid);
            if (cacheResult != null)
            {
                
                return Convert.FromBase64String(cacheResult);
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

            if (keyList == null)
            {
                var revisions = _manager.GetAllRevisions(dataFile.pagename).revisionList;
                return GetLatestValidRevision(dataFile, revisions);
            }

            if (pageContent.Equals(""))
            {
                return null;
            }

            try
            {
                var pageContentBytes = Convert.FromBase64String(pageContent);
                var decryptedTextBytes = _manager.Decrypt(pageContentBytes,
                    keyList.symmKey, keyList.iv);

                var textBytes = decryptedTextBytes.Take(decryptedTextBytes.Length - 256).ToArray();
                var hashBytes = decryptedTextBytes.Skip(decryptedTextBytes.Length - 256).ToArray();

                if (!_manager.VerifyData(keyList.publicKey, textBytes, hashBytes))
                {
                    Console.WriteLine("Verifying failed...");
                    var revisions = _manager.GetAllRevisions(dataFile.pagename).revisionList;
                    return GetLatestValidRevision(dataFile, revisions);
                }

                if (!(textBytes.Length > 0))
                {
                    return null;
                }

                getPageContent.revision.content = Convert.ToBase64String(textBytes);
                _manager.AddEntryToCache(dataFile.pagename, getPageContent.revision);

                return textBytes;
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}