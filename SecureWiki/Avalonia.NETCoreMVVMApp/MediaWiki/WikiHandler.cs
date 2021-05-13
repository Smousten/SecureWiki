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
        private readonly MediaWikiObject _mwo;

        public readonly bool LoggedIn;
        public string url;

        public WikiHandler(string username, string password, HttpClient inputClient, Manager manager,
            string url = "http://localhost/mediawiki/api.php")
        {
            _mwo = new MediaWikiObject(inputClient, username, password, url);
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

        public MediaWikiObject.PageQuery.AllRevisions GetAllRevisions(string pageTitle)
        {
            MediaWikiObject.PageQuery.AllRevisions allRevisions = new(_mwo, pageTitle);
            allRevisions.GetAllRevisions();
            return allRevisions;
        }

        public Revision GetLatestRevision(AccessFile accessFile)
        {
            MediaWikiObject.PageQuery.LatestRevision latestRevision = new(_mwo, accessFile.pageName);
            latestRevision.GetLatestRevision();
            return latestRevision.revision;
        }

        public string GetPageContent(string pageTitle, string revID = "-1")
        {
            MediaWikiObject.PageQuery.PageContent pc = new(_mwo, pageTitle, revID);
            string output = pc.GetContent();
            return output;
        }

        public bool PageAlreadyExists(string pageTitle, string revID)
        {
            MediaWikiObject.PageQuery.PageContent pc = new(_mwo, pageTitle, revID);
            return pc.PageAlreadyExists();
        }

        public void UndoRevisionsByID(string pageTitle, string startID, string endID)
        {
            MediaWikiObject.PageAction.UndoRevisions undoRevisions =
                new(_mwo, pageTitle);
            undoRevisions.UndoRevisionsByID(startID, endID);
        }

        public void DeleteRevisionsByID(string pageTitle, string IDs)
        {
            MediaWikiObject.PageAction.DeleteRevisions deleteRevisions =
                new(_mwo, pageTitle);
            deleteRevisions.DeleteRevisionsByIDString(IDs);
        }

        public bool Upload(AccessFile accessFile, byte[] content)
        {
            // var srcDir = GetRootDir(filepath);
            // var plainText = File.ReadAllBytes(srcDir);
            var plainText = content;
            string pageTitle = accessFile.pageName;

            if (plainText.Length <= 0) return false;

            var latestRevIDInCache = _manager.cacheManager.GetLatestRevisionID(pageTitle);
            var rev = GetLatestRevision(accessFile);

            if (rev.revisionID != null && !rev.revisionID.Equals(latestRevIDInCache))
            {
                string warningString = "Your changes are no longer based on the newest revision available, " +
                                       "push changes to server regardless?" +
                                       "\nUploaded: " + rev.timestamp +
                                       "\nBy user: " + rev.user +
                                       "\nContent size: " + rev.size;

                var msgBoxOutput = Manager.ShowMessageBox("Warning!", warningString);

                if (msgBoxOutput == MessageBox.Result.Cancel)
                {
                    Console.WriteLine("Upload cancelled");
                    return false;
                }
            }

            // Find latest key in data file key list
            var key = accessFile.keyList.Last();

            // Encrypt text using key from key list
            // var encryptedContent = Crypto.Encrypt(plainText, key.SymmKey, iv);
            var encryptedContent = Crypto.EncryptGCM(plainText, key.SymmKey);

            if (encryptedContent == null) return false;

            // Sign hash value of cipher text
            var signature = Crypto.SignData(key.PrivateKey!, encryptedContent);

            // Combine encrypted content and signature, then convert to string
            var uploadContentBytes = ByteArrayCombiner.Combine(encryptedContent, signature);
            var uploadContentText = Convert.ToBase64String(uploadContentBytes);

            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                accessFile.pageName);
            var httpResponse = uploadNewRevision.UploadContent(uploadContentText);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // Check if upload was successful
            if (httpResponse != null)
            {
                var response = new Response(httpResponse);

                // Get revision ID of the revision that was just uploaded and update DataFileEntry revision start
                // information for key if not set  
                if (key.RevisionStart.Equals("-1") && response.newrevidString != null)
                {
                    accessFile.keyList.Last().RevisionStart = response.newrevidString;
                }

                // If upload was a success
                if (response.Result == Response.ResultType.Success)
                {
                    return true;
                }
            }

            return false;
        }

        // Get latest valid revision of wiki page
        public byte[]? GetLatestValidRevision(AccessFile accessFile, List<Revision> revisions)
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
                var key = accessFile.GetAccessFileKeyByRevisionID(revid);
                if (key == null)
                {
                    continue;
                }

                // Get page content from server
                var pageContent = GetPageContent(accessFile.pageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Verify data using public key, ciphertext and signature
                if (Crypto.VerifyData(key.PublicKey, splitPageContent.Value.cipherBytes,
                    splitPageContent.Value.signBytes))
                {
                    var decryptedBytes = Crypto.DecryptGCM(splitPageContent.Value.cipherBytes, key.SymmKey);
                    if (decryptedBytes == null)
                    {
                        continue;
                    }

                    _manager.WriteToLogger(
                        $"Signature of revision '{revid}' verified. This is the latest valid revision.",
                        accessFile.filename);
                    return decryptedBytes;
                }
            }

            return null;
        }

        // Returns the id of the newest revision of a page on the server, or null if no revision is found
        public string? GetLatestRevisionID(string pageName)
        {
            MediaWikiObject.PageQuery.LatestRevision latestRevision = new(_mwo, pageName);
            latestRevision.GetLatestRevision();
            return latestRevision.revision.revisionID;
        }

        // Download latest valid revision starting from given revid.
        // If revid is null, newest revision on server is starting point
        public byte[]? Download(AccessFile accessFile, string? revid = null)
        {
            // If revid is not set, get id of newest revision, or set to default
            revid ??= GetLatestRevisionID(accessFile.pageName) ?? "-1";

            // If specific revision is requested, find key in access file key list with correct start revision ID and end revision ID.
            // Otherwise get latest key
            var key = revid.Equals("-1")
                ? accessFile.keyList.LastOrDefault()
                : accessFile.keyList.Where((t, i)
                    => accessFile.IsValidRevisionID(revid, i)).FirstOrDefault();

            // If no such key is found, return latest valid revision
            if (key == null)
            {
                _manager.WriteToLogger(
                    $"Could not find AccessFileKey matching requested revision id '{revid}'. Attempting to get latest valid revision.",
                    accessFile.filename, LoggerEntry.LogPriority.Warning);
                var revisions = GetAllRevisions(accessFile.pageName).revisionList;
                return GetLatestValidRevision(accessFile, revisions);
            }

            // Get page content from server
            var pageContent = GetPageContent(accessFile.pageName, revid);
            if (pageContent.Equals(""))
            {
                var revisions = GetAllRevisions(accessFile.pageName).GetAllRevisionBefore(revid);
                return GetLatestValidRevision(accessFile, revisions);
            }

            try
            {
                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Verify data using public key, ciphertext and signature
                if (!Crypto.VerifyData(key.PublicKey, splitPageContent.Value.cipherBytes,
                    splitPageContent.Value.signBytes))
                {
                    _manager.WriteToLogger(
                        $"Verifying signature of revision '{revid}'failed. " +
                        $"Attempting to get latest valid revision older than requested revision.",
                        accessFile.filename, LoggerEntry.LogPriority.Warning);
                    var revisions = GetAllRevisions(accessFile.pageName).GetAllRevisionBefore(revid);
                    return GetLatestValidRevision(accessFile, revisions);
                }
                
                var decryptedBytes = Crypto.DecryptGCM(splitPageContent.Value.cipherBytes, key.SymmKey);
                if (decryptedBytes == null)
                {
                    var revisions = GetAllRevisions(accessFile.pageName).GetAllRevisionBefore(revid);
                    return GetLatestValidRevision(accessFile, revisions);
                }

                return !(decryptedBytes.Length > 0) ? null : decryptedBytes;
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private (byte[] cipherBytes, byte[] signBytes)? SplitPageContent(string pageContent)
        {
            var pageContentBytes = Convert.FromBase64String(pageContent);
            var cipherBytes = pageContentBytes.Take(pageContentBytes.Length - 256).ToArray();
            var signBytes = pageContentBytes.Skip(pageContentBytes.Length - 256).ToArray();
            return (cipherBytes, signBytes);
        }

        public bool UploadAccessFile(SymmetricReference symmetricReference, AccessFile accessFile)
        {
            var accessFileString = JSONSerialization.SerializeObject(accessFile);
            var accessFileBytes = Encoding.ASCII.GetBytes(accessFileString);

            var cipherTextBytes = Crypto.EncryptGCM(accessFileBytes, symmetricReference.symmKey);

            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                symmetricReference.targetPageName);

            if (cipherTextBytes != null)
            {
                var cipherText = Convert.ToBase64String(cipherTextBytes);
                var httpResponse = uploadNewRevision.UploadContent(cipherText);
                _mwo.editToken ??= uploadNewRevision.editToken;

                // Check if upload was successful
                if (httpResponse != null)
                {
                    var response = new Response(httpResponse);
                
                    // If upload was a success
                    if (response.Result == Response.ResultType.Success)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool UploadMasterKeyring(byte[] key, string pageName, Keyring masterKeyring)
        {
            var keyringString = JSONSerialization.SerializeObject(masterKeyring);
            var keyringBytes = Encoding.ASCII.GetBytes(keyringString);
            
            // Encrypt text using key from key list
            var encryptedContent = Crypto.EncryptGCM(keyringBytes, key);
            if (encryptedContent == null) return false;

            var encryptedContentString = Convert.ToBase64String(encryptedContent);
            
            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                pageName);
            var httpResponse = uploadNewRevision.UploadContent(encryptedContentString);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // Check if upload was successful
            if (httpResponse != null)
            {
                var response = new Response(httpResponse);
                
                // If upload was a success
                if (response.Result == Response.ResultType.Success)
                {
                    return true;
                }
            }

            return false;   
        }
        

        public bool UploadKeyring(AccessFile accessFile, Keyring keyring)
        {
            var keyringString = JSONSerialization.SerializeObject(keyring);
            var keyringBytes = Encoding.ASCII.GetBytes(keyringString);

            // Find latest key in access file key list
            var key = accessFile.keyList.Last();

            // Encrypt text using key from key list
            var encryptedContent = Crypto.EncryptGCM(keyringBytes, key.SymmKey);
            if (encryptedContent == null) return false;

            // Sign hash value of cipher text
            var signature = Crypto.SignData(key.PrivateKey!, encryptedContent);

            // Combine encrypted content and signature, then convert to string
            var uploadContentBytes = ByteArrayCombiner.Combine(encryptedContent, signature);
            var uploadContentText = Convert.ToBase64String(uploadContentBytes);

            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                accessFile.pageName);
            var httpResponse = uploadNewRevision.UploadContent(uploadContentText);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // Check if upload was successful
            if (httpResponse != null)
            {
                var response = new Response(httpResponse);

                // Get revision ID of the revision that was just uploaded and update AccessFileEntry revision start
                // information for key if not set  
                if (key.RevisionStart.Equals("-1") && response.newrevidString != null)
                {
                    accessFile.keyList.Last().RevisionStart = response.newrevidString;
                }

                // If upload was a success
                if (response.Result == Response.ResultType.Success)
                {
                    return true;
                }
            }

            return false;
        }
        
        public AccessFile? DownloadAccessFile(SymmetricReference symmetricReference)
        {
            // Download access file
            var accessFileContent = GetPageContent(symmetricReference.targetPageName);

            var accessFileBytes = Convert.FromBase64String(accessFileContent);
            var decryptedAccessFile = Crypto.DecryptGCM(accessFileBytes, symmetricReference.symmKey);
            if (decryptedAccessFile == null) return null;

            var decryptedAccessFileString = Encoding.ASCII.GetString(decryptedAccessFile);
            var accessFile = JSONSerialization.DeserializeObject(
                decryptedAccessFileString, typeof(AccessFile)) as AccessFile;
            return accessFile;
        }

        public Keyring? DownloadKeyring(SymmetricReference symmetricReference)
        {
            // Download access file
            var accessFile = DownloadAccessFile(symmetricReference);

            // Download master keyring - pageTitle stored in access File links to rootkeyring
            if (accessFile != null)
            {
                var keyringBytes = Download(accessFile);
                if (keyringBytes != null)
                {
                    var keyringString = Encoding.ASCII.GetString(keyringBytes);
                    var keyring = JSONSerialization.DeserializeObject(
                        keyringString, typeof(Keyring)) as Keyring;
                    return keyring;
                }
            }

            return null;
        }

        public void DownloadKeyringsRecursion(AccessFile accessFile, RootKeyring rootKeyring)
        {
            var keyringBytes = Download(accessFile);
            if (keyringBytes != null)
            {
                var keyringString = Encoding.ASCII.GetString(keyringBytes);
                var keyring = JSONSerialization.DeserializeObject(
                    keyringString, typeof(Keyring)) as Keyring;

                if (keyring != null)
                {
                    rootKeyring.keyrings.Add(keyring);
                    foreach (var symmRef in keyring.SymmetricReferences)
                    {
                        var targetAccessFile = DownloadAccessFile(symmRef);
                        if (symmRef.type == SymmetricReference.Type.GenericFile)
                        {
                            if (targetAccessFile != null) rootKeyring.AddAccessFile(targetAccessFile);
                        }
                        else
                        {
                            if (targetAccessFile != null) DownloadKeyringsRecursion(targetAccessFile, rootKeyring);
                        }
                    }
                }
            }
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
                    // var decryptedContent = Crypto.DecryptGCM(encryptedContentBytes, symmKey);
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
            Console.WriteLine("Fetching updates from inbox page belonging to contact '{0}'.", contact.Nickname);
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
                    break;
                }

                // Get page content from server
                var pageContent = GetPageContent(contact.PageTitle, revid.ToString());

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
            // var encryptedBytes = Crypto.EncryptGCM(
            //     contentBytes, symmKey);

            if (encryptedBytes == null || encryptedSymmKeyData == null)
            {
                Console.WriteLine("UploadToInboxPage: Failed encryption");
                return false;
            }

            // Combine ciphertexts
            byte[] pageContentBytes = ByteArrayCombiner.Combine(encryptedSymmKeyData, encryptedBytes);
            var pageContent = Convert.ToBase64String(pageContentBytes);

            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
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