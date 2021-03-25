using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using SecureWiki.Model;
using SecureWiki.Views;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler : IServerInteraction
    {
        private readonly Manager _manager;
        private readonly MediaWikiObjects _mwo;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string ip = "localhost")
        {
            _mwo = new MediaWikiObjects(inputClient, username, password, ip);
            _manager = manager;
        }

        public MediaWikiObjects.PageQuery.AllRevisions GetAllRevisions(string pageTitle)
        {
            MediaWikiObjects.PageQuery.AllRevisions allRevisions = new(_mwo, pageTitle);
            allRevisions.GetAllRevisions();
            allRevisions.PrintAllRevisions();
            return allRevisions;
        }

        public Revision GetLatestRevision(DataFileEntry dataFile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(_mwo, dataFile.pageName);
            latestRevision.GetLatestRevision();
            return latestRevision.revision;
        }

        public string GetPageContent(string pageTitle, string revID)
        {
            MediaWikiObjects.PageQuery.PageContent pc = new(_mwo, pageTitle, revID);
            string output = pc.GetContent();
            return output;
        }

        public void UndoRevisionsByID(string pageTitle, string startID, string endID)
        {
            MediaWikiObjects.PageAction.UndoRevisions undoRevisions =
                new(_mwo, pageTitle);
            undoRevisions.UndoRevisionsByID(startID, endID);
        }

        public void DeleteRevisionsByID(string pageTitle, string IDs)
        {
            MediaWikiObjects.PageAction.DeleteRevisions deleteRevisions =
                new(_mwo, pageTitle);
            deleteRevisions.DeleteRevisionsByIDString(IDs);
        }

        public void Upload(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = File.ReadAllBytes(srcDir);
            string pageTitle = dataFile.pageName;
            // Console.WriteLine("Upload plain text: " + plainText);

            if (plainText.Length > 0)
            {
                var latestRevID = _manager.cacheManager.GetLatestRevisionID(pageTitle);
                var rev = GetLatestRevision(dataFile);

                if (rev.revisionID != null && !rev.revisionID.Equals(latestRevID))
                {
                    // TODO: MessageBox content

                    string warningString = "This is not the newest revision available, " +
                                           "sure you wanna do this, mate?" +
                                           "\nUploaded: " + rev.timestamp +
                                           "\nBy user: " + rev.user +
                                           "\nContent size: " + rev.size;

                    var msgBoxOutput = _manager.ShowMessageBox("Warning!", warningString);

                    if (msgBoxOutput == MessageBox.MessageBoxResult.Cancel)
                    {
                        Console.WriteLine("Upload cancelled");
                        return;
                    }
                }

                // Sign plaintext
                var keyList = dataFile.keyList.Last();

                var hash = _manager.SignData(keyList.privateKey!, plainText);

                byte[] rv = new byte[plainText.Length + hash.Length];
                Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
                Buffer.BlockCopy(hash, 0, rv, plainText.Length, hash.Length);

                var encryptedBytes = _manager.Encrypt(
                    rv, keyList.symmKey, keyList.iv);
                var encryptedText = Convert.ToBase64String(encryptedBytes);

                // Upload encrypted content
                MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                    dataFile.pageName);
                uploadNewRevision.UploadContent(encryptedText);

                // Get revision ID of the revision that was just uploaded and update DataFileEntry revision start
                // information for key if not set  
                rev = GetLatestRevision(dataFile);
                if (keyList.revisionStart.Equals("-1") && rev.revisionID != null)
                {
                    dataFile.keyList.Last().revisionStart = rev.revisionID;
                }
            }
        }

        // Return absolute path to fuse root directory
        private static string GetRootDir(string relativeFilepath)
        {
            var filepath = "fuse/directories/rootdir/" + relativeFilepath;
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var srcDir = Path.Combine(projectDir, filepath);
            return srcDir;
        }

        // Get latest valid revision of wiki page
        public byte[]? GetLatestValidRevision(DataFileEntry dataFile, List<Revision> revisions)
        {
            for (var i = 0; i < revisions.Count; i++)
            {
                // If revision ID is not set, continue
                var revid = revisions[i].revisionID;
                if (revid == null)
                {
                    continue;
                }

                // If no valid key is found, continue
                var key = dataFile.GetDataFileKeyByRevisionID(revid);
                if (key == null)
                {
                    continue;
                }

                MediaWikiObjects.PageQuery.PageContent getPageContent =
                    new(_mwo, dataFile.pageName, revid);
                var pageContent = getPageContent.GetContent();

                var (textBytes, hashBytes) = DecryptPageContent(pageContent, key);

                if (_manager.VerifyData(key.publicKey, textBytes, hashBytes))
                {
                    return textBytes;
                }
            }

            return null;
        }

        public byte[]? Download(DataFileEntry datafile)
        {
            MediaWikiObjects.PageQuery.LatestRevision latestRevision = new(_mwo, datafile.pageName);
            latestRevision.GetLatestRevision();

            return Download(datafile, latestRevision.revision.revisionID ?? "-1");
        }

        public byte[]? Download(DataFileEntry dataFile, string revid)
        {
            // Check if revision already exists in cache and return output if so
            var cacheResult = _manager.AttemptReadFileFromCache(dataFile.pageName, revid);
            if (cacheResult != null)
            {
                return Convert.FromBase64String(cacheResult);
            }

            DataFileKey? keyList = null;
            for (var i = 0; i < dataFile.keyList.Count; i++)
            {
                if (dataFile.IsValidRevisionID(revid, i))
                {
                    keyList = dataFile.keyList[i];
                    break;
                }
            }

            if (keyList == null)
            {
                var revisions = _manager.GetAllRevisions(dataFile.pageName).revisionList;
                return GetLatestValidRevision(dataFile, revisions);
            }

            MediaWikiObjects.PageQuery.PageContent getPageContent = new(_mwo, dataFile.pageName, revid);
            var pageContent = getPageContent.GetContent();

            if (pageContent.Equals(""))
            {
                return null;
            }

            try
            {
                var (textBytes, hashBytes) = DecryptPageContent(pageContent, keyList);

                if (!_manager.VerifyData(keyList.publicKey, textBytes, hashBytes))
                {
                    Console.WriteLine("Verifying failed...");
                    var revisions = _manager.GetAllRevisions(dataFile.pageName).revisionList;
                    return GetLatestValidRevision(dataFile, revisions);
                }

                if (!(textBytes.Length > 0))
                {
                    return null;
                }

                getPageContent.revision.content = Convert.ToBase64String(textBytes);
                _manager.AddEntryToCache(dataFile.pageName, getPageContent.revision);

                return textBytes;
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private (byte[] textBytes, byte[] hashBytes) DecryptPageContent(string pageContent, DataFileKey keyList)
        {
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedTextBytes = _manager.Decrypt(pageContentBytes,
                keyList.symmKey, keyList.iv);

            var textBytes = decryptedTextBytes.Take(decryptedTextBytes.Length - 256).ToArray();
            var hashBytes = decryptedTextBytes.Skip(decryptedTextBytes.Length - 256).ToArray();
            return (textBytes, hashBytes);
        }
    }
}