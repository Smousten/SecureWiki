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
        
        public readonly bool LoggedIn;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string url = "http://localhost/mediawiki/api.php")
        {
            _mwo = new MediaWikiObjects(inputClient, username, password, url);
            _manager = manager;
            LoggedIn = _mwo.loggedIn;
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

        public MediaWikiObjects.PageQuery.AllRevisions GetAllRevisions(string pageTitle)
        {
            MediaWikiObjects.PageQuery.AllRevisions allRevisions = new(_mwo, pageTitle);
            allRevisions.GetAllRevisions();
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

            if (plainText.Length <= 0) return;

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

            // Find latest key in data file key list
            var key = dataFile.keyList.Last();

            // Sign hash value of plain text
            var signature = _manager.SignData(key.privateKey!, plainText);

            // Encrypt text and signature using key from key list
            var encryptedContent = EncryptTextAndSignature(plainText, signature, key);
            if (encryptedContent == null) return;

            // Upload encrypted content
            MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                dataFile.pageName);
            uploadNewRevision.UploadContent(encryptedContent);

            // Get revision ID of the revision that was just uploaded and update DataFileEntry revision start
            // information for key if not set  
            rev = GetLatestRevision(dataFile);
            if (key.revisionStart.Equals("-1") && rev.revisionID != null)
            {
                dataFile.keyList.Last().revisionStart = rev.revisionID;
            }
            
            // If uploaded revision ID is greater than latest revision end. 
            // Only happens if user manually deletes entries from key list 
            // if (!key.revisionEnd.Equals("-1") 
            //     && rev.revisionID != null 
            //     && int.Parse(key.revisionEnd) < int.Parse(rev.revisionID))
            // {
            //     dataFile.keyList.Last().revisionStart = rev.revisionID;
            //     dataFile.keyList.Last().revisionEnd = "-1";
            // }
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

                var decryptedBytes = DecryptPageContent(pageContent, key);
                
                // If decryption fails, continue
                if (decryptedBytes == null)
                {
                    continue;
                }

                var textBytes = decryptedBytes.Value.textBytes;
                var signBytes = decryptedBytes.Value.signBytes;

                if (_manager.VerifyData(key.publicKey, textBytes, signBytes))
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

            DataFileKey? key = null;
            // Find key in data file key list with correct start revision ID and end revision ID
            for (var i = 0; i < dataFile.keyList.Count; i++)
            {
                if (dataFile.IsValidRevisionID(revid, i))
                {
                    key = dataFile.keyList[i];
                    break;
                }
            }

            // If key == null, then return null?? 
            if (key == null)
            {
                var revisions = GetAllRevisions(dataFile.pageName).revisionList;
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
                var decryptedBytes = DecryptPageContent(pageContent, key);
                if (decryptedBytes == null)
                {
                    var revisions = GetAllRevisions(dataFile.pageName).revisionList;
                    return GetLatestValidRevision(dataFile, revisions);
                }

                var textBytes = decryptedBytes.Value.textBytes;
                var signBytes = decryptedBytes.Value.signBytes;
                if (!_manager.VerifyData(key.publicKey, textBytes, signBytes))
                {
                    Console.WriteLine("Verifying failed...");
                    var revisions = GetAllRevisions(dataFile.pageName).revisionList;
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

        private string? EncryptTextAndSignature(byte[] plainText, byte[] signature, DataFileKey keyList)
        {
            byte[] rv = new byte[plainText.Length + signature.Length];
            Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
            Buffer.BlockCopy(signature, 0, rv, plainText.Length, signature.Length);

            var encryptedBytes = _manager.Encrypt(
                rv, keyList.symmKey, keyList.iv);
            if (encryptedBytes == null)
            {
                Console.WriteLine("Failed encryption");
                return null;
            }

            var encryptedText = Convert.ToBase64String(encryptedBytes);
            return encryptedText;
        }

        private (byte[] textBytes, byte[] signBytes)? DecryptPageContent(string pageContent, DataFileKey keyList)
        {
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedBytes = _manager.Decrypt(pageContentBytes,
                keyList.symmKey, keyList.iv);

            if (decryptedBytes == null)
            {
                Console.WriteLine("Decryption failed");
                return null;
            }

            var textBytes = decryptedBytes.Take(decryptedBytes.Length - 256).ToArray();
            var signBytes = decryptedBytes.Skip(decryptedBytes.Length - 256).ToArray();
            return (textBytes, signBytes);
        }
    }
}