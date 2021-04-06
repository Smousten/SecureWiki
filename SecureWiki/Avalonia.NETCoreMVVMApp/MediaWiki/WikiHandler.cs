using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.Views;

namespace SecureWiki.MediaWiki
{
    public class WikiHandler : IServerInteraction
    {
        private readonly Manager _manager;
        private readonly MediaWikiObjects _mwo;

        public readonly bool LoggedIn;
        public string url;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string url = "http://localhost/mediawiki/api.php")
        {
            _mwo = new MediaWikiObjects(inputClient, username, password, url);
            _manager = manager;
            LoggedIn = _mwo.loggedIn;
            this.url = url;
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
            var signature = _manager.SignData(key.PrivateKey!, plainText);

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
            if (key.RevisionStart.Equals("-1") && rev.revisionID != null)
            {
                dataFile.keyList.Last().RevisionStart = rev.revisionID;
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

                if (_manager.VerifyData(key.PublicKey, textBytes, signBytes))
                {
                    _manager.WriteToLogger($"Signature of revision '{revid}' verified. This is the latest valid revision.", 
                        dataFile.filename, LoggerEntry.LogPriority.Normal);
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
                if (!_manager.VerifyData(key.PublicKey, textBytes, signBytes))
                {
                    Console.WriteLine("Verifying failed...");
                    _manager.WriteToLogger($"Verifying signature of revision '{revid}'failed. Attempting to get latest valid revision.", 
                        dataFile.filename, LoggerEntry.LogPriority.Warning);
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

        private string? EncryptTextAndSignature(byte[] plainText, byte[] signature, DataFileKey key)
        {
            byte[] rv = new byte[plainText.Length + signature.Length];
            Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
            Buffer.BlockCopy(signature, 0, rv, plainText.Length, signature.Length);

            var encryptedBytes = _manager.Encrypt(
                rv, key.SymmKey, key.IV);
            if (encryptedBytes == null)
            {
                Console.WriteLine("Failed encryption");
                return null;
            }

            var encryptedText = Convert.ToBase64String(encryptedBytes);
            return encryptedText;
        }

        private (byte[] textBytes, byte[] signBytes)? DecryptPageContent(string pageContent, DataFileKey key)
        {
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedBytes = _manager.Decrypt(pageContentBytes,
                key.SymmKey, key.IV);

            if (decryptedBytes == null)
            {
                Console.WriteLine("Decryption failed");
                return null;
            }

            var textBytes = decryptedBytes.Take(decryptedBytes.Length - 256).ToArray();
            var signBytes = decryptedBytes.Skip(decryptedBytes.Length - 256).ToArray();
            return (textBytes, signBytes);
        }

        public List<string>? GetInboxPagesContent()
        {
            List<string> outputList = new();
            
            var contactList = _manager.contactManager.GetOwnContactsByServerLink(url);

            if (contactList == null)
            {
                return null;
            }

            foreach (var contact in contactList)
            {
                var allRevs = GetAllRevisions(contact.PageTitle);

                int highestRev = 0;

                foreach (var rev in allRevs.revisionList)
                {
                    if (rev.revisionID == null)
                    {
                        continue;
                    }
                    
                    var revid = int.Parse(rev.revisionID);

                    if (revid > highestRev)
                    {
                        highestRev = revid;
                    }

                    // If content has previously been parsed
                    if (revid < contact.revidCounter) 
                    {
                        break;
                    }
                    
                    MediaWikiObjects.PageQuery.PageContent getPageContent = new(_mwo, contact.PageTitle, revid.ToString());
                    var pageContent = getPageContent.GetContent();

                    if (pageContent.Equals(""))
                    {
                        continue;
                    }
                        
                    outputList.Add(pageContent);
                }

                if (highestRev > contact.revidCounter)
                {
                    contact.revidCounter = highestRev;
                }
                
            }

            // Return outputList if it is not empty, null otherwise
            return outputList.Count > 0 ? outputList : null;
        }

        public void UploadToInboxPage(string pageTitle, string content, byte[] publicKey)
        {
            Crypto _crypto = new();
            var (symmKey, IV) = _crypto.GenerateAESParams();
            
            var symmKeyData = ByteArrayCombiner.Combine(symmKey, IV);
            var encryptedSymmKeyData = _crypto.RSAEncryptWithPublicKey(symmKeyData, publicKey);

            Console.WriteLine("encryptedSymmKeyData.length: " + encryptedSymmKeyData?.Length);
            Console.WriteLine("SymmKey.length: " + symmKey.Length);
            Console.WriteLine("IV.length: " + IV.Length);
            
            var contentBytes = Encoding.ASCII.GetBytes(content);
            var encryptedBytes = _manager.Encrypt(
                contentBytes, symmKey, IV);
            
            if (encryptedBytes == null || encryptedSymmKeyData == null)
            {
                Console.WriteLine("UploadToInboxPage: Failed encryption");
                return;
            }

            // Combine ciphertexts
            byte[] pageContentBytes = ByteArrayCombiner.Combine(encryptedBytes, encryptedSymmKeyData);

            // var pageContent = Encoding.ASCII.GetString(pageContentBytes);
            var pageContent = Convert.ToBase64String(pageContentBytes);

            // Upload encrypted content
            MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                pageTitle);
            uploadNewRevision.UploadContent(pageContent);
        }
    }
}