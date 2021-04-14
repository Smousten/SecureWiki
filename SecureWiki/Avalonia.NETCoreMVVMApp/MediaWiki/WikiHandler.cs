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

        public bool PageAlreadyExists(string pageTitle, string revID)
        {
            MediaWikiObjects.PageQuery.PageContent pc = new(_mwo, pageTitle, revID);
            return pc.PageAlreadyExists();
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

        public bool Upload(DataFileEntry dataFile, string filepath)
        {
            var srcDir = GetRootDir(filepath);
            var plainText = File.ReadAllBytes(srcDir);
            string pageTitle = dataFile.pageName;

            if (plainText.Length <= 0) return false;

            var latestRevID = _manager.cacheManager.GetLatestRevisionID(pageTitle);
            var rev = GetLatestRevision(dataFile);

            if (rev.revisionID != null && !rev.revisionID.Equals(latestRevID))
            {
                // TODO: MessageBox content

                string warningString = "Your changes are no longer based on the newest revision available, " +
                                       "push changes to server regardless?" +
                                       "\nUploaded: " + rev.timestamp +
                                       "\nBy user: " + rev.user +
                                       "\nContent size: " + rev.size;

                var msgBoxOutput = _manager.ShowMessageBox("Warning!", warningString);

                if (msgBoxOutput == MessageBox.MessageBoxResult.Cancel)
                {
                    Console.WriteLine("Upload cancelled");
                    return false;
                }
            }

            // Find latest key in data file key list
            var key = dataFile.keyList.Last();

            // Sign hash value of plain text
            var signature = Crypto.SignData(key.PrivateKey!, plainText);

            // Encrypt text and signature using key from key list
            var encryptedContent = EncryptTextAndSignature(plainText, signature, key);
            if (encryptedContent == null) return false;

            // Upload encrypted content
            MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                dataFile.pageName);
            var httpResponse = uploadNewRevision.UploadContent(encryptedContent);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // If uploaded revision ID is greater than latest revision end. 
            // Only happens if user manually deletes entries from key list 
            // if (!key.revisionEnd.Equals("-1") 
            //     && rev.revisionID != null 
            //     && int.Parse(key.revisionEnd) < int.Parse(rev.revisionID))
            // {
            //     dataFile.keyList.Last().revisionStart = rev.revisionID;
            //     dataFile.keyList.Last().revisionEnd = "-1";
            // }

            // Check if upload was successful
            if (httpResponse != null)
            {
                var response = new Response(httpResponse);

                // Get revision ID of the revision that was just uploaded and update DataFileEntry revision start
                // information for key if not set  
                // rev = GetLatestRevision(dataFile);
                if (key.RevisionStart.Equals("-1") && response.newrevidString != null)
                {
                    dataFile.keyList.Last().RevisionStart = response.newrevidString;
                }

                if (response.Result == Response.ResultType.Success)
                {
                    return true;
                }
            }

            return false;
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

                if (Crypto.VerifyData(key.PublicKey, textBytes, signBytes))
                {
                    _manager.WriteToLogger(
                        $"Signature of revision '{revid}' verified. This is the latest valid revision.",
                        dataFile.filename, LoggerEntry.LogPriority.Normal);
                    return textBytes;
                }
            }

            return null;
        }

        // Download latest valid revision
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

            // If no such key is found, return latest valid revision
            if (key == null)
            {
                var revisions = GetAllRevisions(dataFile.pageName).revisionList;
                return GetLatestValidRevision(dataFile, revisions);
            }

            // Get page content from server
            MediaWikiObjects.PageQuery.PageContent getPageContent = new(_mwo, dataFile.pageName, revid);
            var pageContent = getPageContent.GetContent();

            if (pageContent.Equals(""))
            {
                return null;
            }

            // Decrypt and verify content, return latest valid revision if this fails
            try
            {
                var decryptedBytes = DecryptPageContent(pageContent, key);
                if (decryptedBytes == null)
                {
                    var revisions = GetAllRevisions(dataFile.pageName).revisionList;
                    return GetLatestValidRevision(dataFile, revisions);
                }

                // Split decrypted page content into plaintext and signature
                var textBytes = decryptedBytes.Value.textBytes;
                var signBytes = decryptedBytes.Value.signBytes;
                if (!Crypto.VerifyData(key.PublicKey, textBytes, signBytes))
                {
                    Console.WriteLine("Verifying failed...");
                    _manager.WriteToLogger(
                        $"Verifying signature of revision '{revid}'failed. Attempting to get latest valid revision.",
                        dataFile.filename, LoggerEntry.LogPriority.Warning);
                    var revisions = GetAllRevisions(dataFile.pageName).revisionList;
                    return GetLatestValidRevision(dataFile, revisions);
                }

                if (!(textBytes.Length > 0))
                {
                    return null;
                }

                // Add plaintext to cache
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
            // Combine plaintext and signature
            byte[] rv = new byte[plainText.Length + signature.Length];
            Buffer.BlockCopy(plainText, 0, rv, 0, plainText.Length);
            Buffer.BlockCopy(signature, 0, rv, plainText.Length, signature.Length);

            // Encrypt message
            var encryptedBytes = Crypto.Encrypt(
                rv, key.SymmKey, key.IV);
            if (encryptedBytes == null)
            {
                Console.WriteLine("Failed encryption");
                return null;
            }

            // Convert to string and return
            var encryptedText = Convert.ToBase64String(encryptedBytes);
            return encryptedText;
        }

        private (byte[] textBytes, byte[] signBytes)? DecryptPageContent(string pageContent, DataFileKey key)
        {
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var decryptedBytes = Crypto.Decrypt(pageContentBytes,
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

        public List<List<string>>? DownloadFromInboxPages()
        {
            // List of new content(s) for each contact
            var outputList = new List<List<string>>();

            // Get list of OwnContacts associated with the current server 
            var contactList = _manager.contactManager.GetOwnContactsByServerLink(url);

            if (contactList == null)
            {
                return null;
            }

            foreach (var contact in contactList)
            {
                // Get list of the contents of new revisions
                var encryptedContentList = GetInboxPageContent(contact);

                // If no new content was found
                if (encryptedContentList == null) continue;

                var contentList = new List<string>();

                // For each new revision
                foreach (var entry in encryptedContentList)
                {
                    // Convert page content to byte array so it can be processed
                    var pageContentBytes = Convert.FromBase64String(entry);

                    // Split page content into header and ciphertext 
                    var encryptedSymmKeyData = pageContentBytes.Take(256).ToArray();
                    var encryptedContentBytes = pageContentBytes.Skip(256).ToArray();

                    // Get IV and symmetric key
                    var decryptedSymmKeyData =
                        Crypto.RSADecryptWithPrivateKey(encryptedSymmKeyData, contact.PrivateKey);
                    var iv = decryptedSymmKeyData?.Take(16).ToArray();
                    var symmKey = decryptedSymmKeyData?.Skip(16).ToArray();

                    if (symmKey == null || iv == null)
                    {
                        Console.WriteLine("symmKey or iv null");
                        break;
                    }

                    // Decrypt ciphertext
                    var decryptedContent = Crypto.Decrypt(encryptedContentBytes, symmKey, iv);

                    if (decryptedContent == null)
                    {
                        Console.WriteLine("decryptedContent is null");
                        break;
                    }

                    // Convert plaintext to string
                    var decryptedContentString = Encoding.ASCII.GetString(decryptedContent);

                    contentList.Add(decryptedContentString);
                }


                if (contentList.Count > 0)
                {
                    outputList.Add(contentList);
                }
            }

            // Return outputList if it is not empty, null otherwise
            return outputList.Count > 0 ? outputList : null;
        }

        private List<string>? GetInboxPageContent(OwnContact contact)
        {
            List<string> encryptedContentList = new();

            // Get list of all revisions on 
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
                if (revid <= contact.revidCounter)
                {
                    Console.WriteLine("revid too low, breaking");
                    break;
                }

                // Get page content from server
                MediaWikiObjects.PageQuery.PageContent getPageContent = new(_mwo, contact.PageTitle, revid.ToString());
                var pageContent = getPageContent.GetContent();

                if (pageContent.Equals(""))
                {
                    continue;
                }

                encryptedContentList.Add(pageContent);
            }

            // Update contact revision counter so that these revisions don't have to be parsed again
            if (highestRev > contact.revidCounter)
            {
                contact.revidCounter = highestRev;
            }

            return encryptedContentList.Count > 0 ? encryptedContentList : null;
        }


        public bool UploadToInboxPage(string pageTitle, string content, byte[] publicKey)
        {
            Console.WriteLine("Uploading content to mediawiki: " + content);
            // Generate symmetric key
            var (symmKey, IV) = Crypto.GenerateAESParams();

            // Encrypt symmetric key information with given public key
            var symmKeyData = ByteArrayCombiner.Combine(IV, symmKey);
            var encryptedSymmKeyData = Crypto.RSAEncryptWithPublicKey(symmKeyData, publicKey);

            // Encrypt content with the symmetric key
            var contentBytes = Encoding.ASCII.GetBytes(content);
            var encryptedBytes = Crypto.Encrypt(
                contentBytes, symmKey, IV);

            if (encryptedBytes == null || encryptedSymmKeyData == null)
            {
                Console.WriteLine("UploadToInboxPage: Failed encryption");
                return false;
            }

            // Combine ciphertexts
            byte[] pageContentBytes = ByteArrayCombiner.Combine(encryptedSymmKeyData, encryptedBytes);
            var pageContent = Convert.ToBase64String(pageContentBytes);

            // Upload encrypted content
            MediaWikiObjects.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                pageTitle);
            var httpResponse = uploadNewRevision.UploadContent(pageContent);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // Check if upload was successful
            if (httpResponse != null)
            {
                var response = new Response(httpResponse);

                if (response.Result == Response.ResultType.Success)
                {
                    return true;
                }
            }

            return false;
        }
    }
}