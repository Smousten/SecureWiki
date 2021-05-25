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

        public MediaWikiObject.PageQuery.AllRevisions GetAllRevisions(string pageName)
        {
            MediaWikiObject.PageQuery.AllRevisions allRevisions = new(_mwo, pageName);
            allRevisions.GetAllRevisions();
            return allRevisions;
        }

        public Revision GetLatestRevision(string pageName)
        {
            MediaWikiObject.PageQuery.LatestRevision latestRevision = new(_mwo, pageName);
            latestRevision.GetLatestRevision();
            return latestRevision.revision;
        }
        
        public bool HasAtleastTwoRevisions(string pageName)
        {
            MediaWikiObject.PageQuery.TwoLatestRevisions latestRevisions = new(_mwo, pageName);
            latestRevisions.GetLatestRevisions();
            return latestRevisions.revisionList.Count > 1;
        }

        public string GetPageContent(string pageName, string revID = "-1")
        {
            MediaWikiObject.PageQuery.PageContent pc = new(_mwo, pageName, revID);
            string output = pc.GetContent();
            return output;
        }

        public bool PageAlreadyExists(string pageName, string revID)
        {
            MediaWikiObject.PageQuery.PageContent pc = new(_mwo, pageName, revID);
            return pc.PageAlreadyExists();
        }

        public void UndoRevisionsByID(string pageName, string startID, string endID)
        {
            MediaWikiObject.PageAction.UndoRevisions undoRevisions =
                new(_mwo, pageName);
            undoRevisions.UndoRevisionsByID(startID, endID);
        }

        public void DeleteRevisionsByID(string pageName, string IDs)
        {
            MediaWikiObject.PageAction.DeleteRevisions deleteRevisions =
                new(_mwo, pageName);
            deleteRevisions.DeleteRevisionsByIDString(IDs);
        }

        public void Upload(string pageName, string content)
        {
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                pageName);
            uploadNewRevision.UploadContent(content);
        }
        
        public bool Upload(AccessFile accessFile, byte[] content)
        {
            var plainText = content;
            string pageName = accessFile.AccessFileReference.targetPageName;
            if (plainText.Length <= 0) return false;

            if (!ConfirmUpload(pageName)) return false;

            // Find latest key in data file key list
            var key = accessFile.keyList.Last();

            // Encrypt text using key from key list
            // var encryptedContent = Crypto.Encrypt(plainText, key.SymmKey, iv);
            var encryptedContent = Crypto.Encrypt(plainText, key.SymmKey);

            if (encryptedContent == null) return false;

            // Sign hash value of cipher text
            var signature = Crypto.SignData(key.PrivateKey!, encryptedContent);

            // Combine encrypted content and signature, then convert to string
            var uploadContentBytes = ByteArrayCombiner.Combine(encryptedContent, signature);
            var uploadContentText = Convert.ToBase64String(uploadContentBytes);

            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                accessFile.AccessFileReference.targetPageName);
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

        public bool Upload(SymmetricReference symmetricReference, byte[] content)
        {
            string pageName = symmetricReference.targetPageName;
            if (content.Length <= 0) return false;

            if (!ConfirmUpload(pageName)) return false;
            var cipherTextBytes = Crypto.Encrypt(content, symmetricReference.symmKey);

            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                symmetricReference.targetPageName);

            if (cipherTextBytes == null) return false;
            
            var cipherText = Convert.ToBase64String(cipherTextBytes);
            var httpResponse = uploadNewRevision.UploadContent(cipherText);
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
        
        private bool ConfirmUpload(string pageName)
        {
            var latestRevIDInCache = _manager.cacheManager.GetLatestRevisionID(pageName);
            var rev = GetLatestRevision(pageName);
            var atleastTwoRevisions = HasAtleastTwoRevisions(pageName);

            if (atleastTwoRevisions && rev.revisionID != null && !rev.revisionID.Equals(latestRevIDInCache))
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

            return true;
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
                var pageContent = GetPageContent(accessFile.AccessFileReference.targetPageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Verify data using public key, ciphertext and signature
                if (Crypto.VerifyData(key.PublicKey, splitPageContent.Value.cipherBytes,
                    splitPageContent.Value.signBytes))
                {
                    var decryptedBytes = Crypto.Decrypt(splitPageContent.Value.cipherBytes, key.SymmKey);
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
        
        public byte[]? GetLatestValidRevision(SymmetricReference symmetricReference, List<Revision> revisions)
        {
            for (var i = 0; i < revisions.Count; i++)
            {
                // If revision ID is not set, continue
                var revid = revisions[i].revisionID;
                if (revid == null)
                {
                    continue;
                }

                var pageName = symmetricReference.targetPageName;
                
                // Get page content from server
                var pageContent = GetPageContent(symmetricReference.targetPageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                var accessFileBytes = Convert.FromBase64String(pageContent);
                var decryptedAccessFile = Crypto.Decrypt(accessFileBytes, symmetricReference.symmKey);
                
                if (decryptedAccessFile != null && decryptedAccessFile.Length >= 2)
                {
                    return decryptedAccessFile;
                }
            }

            return null;
        }
        
        public Keyring? GetLatestValidKeyring(AccessFile accessFile, List<Revision> revisions)
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
                var pageContent = GetPageContent(accessFile.AccessFileReference.targetPageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Verify data using public key, ciphertext and signature
                if (Crypto.VerifyData(key.PublicKey, splitPageContent.Value.cipherBytes,
                    splitPageContent.Value.signBytes))
                {
                    var decryptedBytes = Crypto.Decrypt(splitPageContent.Value.cipherBytes, key.SymmKey);
                    if (decryptedBytes == null)
                    {
                        continue;
                    }

                    _manager.WriteToLogger(
                        $"Signature of revision '{revid}' verified. This is the latest valid revision.",
                        accessFile.filename);
                    
                    var keyringString = Encoding.ASCII.GetString(decryptedBytes);
                    if (JSONSerialization.DeserializeObject(
                        keyringString, typeof(Keyring)) is not Keyring keyring
                        || keyring.InboxReferenceToSelf == null) continue;

                    keyring.accessFileReferenceToSelf = accessFile.AccessFileReference;
                    accessFile.AccessFileReference.KeyringTarget = keyring;
                    return keyring;
                }
            }

            return null;
        }
        
        public MasterKeyring? GetLatestValidMasterKeyring(AccessFile accessFile, List<Revision> revisions)
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
                var pageContent = GetPageContent(accessFile.AccessFileReference.targetPageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Verify data using public key, ciphertext and signature
                if (Crypto.VerifyData(key.PublicKey, splitPageContent.Value.cipherBytes,
                    splitPageContent.Value.signBytes))
                {
                    var decryptedBytes = Crypto.Decrypt(splitPageContent.Value.cipherBytes, key.SymmKey);
                    if (decryptedBytes == null)
                    {
                        continue;
                    }

                    _manager.WriteToLogger(
                        $"Signature of revision '{revid}' verified. This is the latest valid revision.",
                        accessFile.filename);
                    
                    var keyringString = Encoding.ASCII.GetString(decryptedBytes);
                    if (JSONSerialization.DeserializeObject(
                        keyringString, typeof(MasterKeyring)) is not MasterKeyring keyring) continue;

                    keyring.accessFileReferenceToSelf = accessFile.AccessFileReference;
                    accessFile.AccessFileReference.KeyringTarget = keyring;
                    if (!keyring.IsValid()) continue;
                    
                    return keyring;
                }
            }

            return null;
        }
        
        public AccessFile? GetLatestValidAccessFile(SymmetricReference symmetricReference, List<Revision> revisions)
        {
            for (var i = 0; i < revisions.Count; i++)
            {
                // If revision ID is not set, continue
                var revid = revisions[i].revisionID;
                if (revid == null)
                {
                    continue;
                }

                Console.WriteLine("GetLatestValidAccessFile:- revid=" + revid);
                
                // Get page content from server
                var pageContent = GetPageContent(symmetricReference.targetPageName, revid);

                // Split downloaded page content into cipher text and signature
                var splitPageContent = SplitPageContent(pageContent);
                if (splitPageContent == null) return null;

                // Decrypt content, continue if not successful
                var accessFileBytes = Convert.FromBase64String(pageContent);
                var decryptedAccessFile = Crypto.Decrypt(accessFileBytes, symmetricReference.symmKey);
                if (decryptedAccessFile == null || decryptedAccessFile.Length < 2) continue;
                var decryptedAccessFileString = Encoding.ASCII.GetString(decryptedAccessFile);
                
                // Check if plaintext can be parsed as Access File
                if (!(JSONSerialization.DeserializeObject(
                    decryptedAccessFileString, typeof(AccessFile)) is AccessFile accessFile)
                    || accessFile.AccessFileReference == null)
                {
                    continue;
                }

                // Connect references and return Access File
                accessFile.AccessFileReference.AccessFileParent = accessFile;                
                accessFile.SymmetricReferenceToSelf = symmetricReference;
            
                // Check it has been parsed correctly
                if (!accessFile.IsValid())
                {
                    continue;
                }
                
                return accessFile;
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
            revid ??= GetLatestRevisionID(accessFile.AccessFileReference.targetPageName) ?? "-1";

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
                var revisions = GetAllRevisions(accessFile.AccessFileReference.targetPageName).revisionList;
                return GetLatestValidRevision(accessFile, revisions);
            }

            // Get page content from server
            var pageContent = GetPageContent(accessFile.AccessFileReference.targetPageName, revid);
            if (pageContent.Equals(""))
            {
                var revisions = GetAllRevisions(accessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
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
                    var revisions = GetAllRevisions(accessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                    return GetLatestValidRevision(accessFile, revisions);
                }
                
                var decryptedBytes = Crypto.Decrypt(splitPageContent.Value.cipherBytes, key.SymmKey);
                if (decryptedBytes == null)
                {
                    var revisions = GetAllRevisions(accessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
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

        public byte[]? Download(SymmetricReference symmetricReference, string? revid = null)
        {
            var pageName = symmetricReference.targetPageName;

            // If revid is not set, get id of newest revision, or set to default
            revid ??= GetLatestRevisionID(pageName) ?? "-1";

            // Get page content from server
            var pageContent = GetPageContent(pageName, revid);
            if (pageContent.Equals(""))
            {
                var revisions = GetAllRevisions(pageName).GetAllRevisionBefore(revid);
                return GetLatestValidRevision(symmetricReference, revisions);
            }

            try
            {
                var accessFileBytes = Convert.FromBase64String(pageContent);
                var decryptedAccessFile = Crypto.Decrypt(accessFileBytes, symmetricReference.symmKey);
                
                if (decryptedAccessFile != null && decryptedAccessFile.Length >= 2)
                {
                    return decryptedAccessFile;
                }
                
                Console.WriteLine("decryptedAccessFile is null");
                var revisions = GetAllRevisions(pageName).GetAllRevisionBefore(revid);
                return GetLatestValidRevision(symmetricReference, revisions);

            }
            catch (FormatException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private (byte[] cipherBytes, byte[] signBytes)? SplitPageContent(string pageContent)
        {
            try
            {
                var pageContentBytes = Convert.FromBase64String(pageContent);
                var cipherBytes = pageContentBytes.Take(pageContentBytes.Length - 256).ToArray();
                var signBytes = pageContentBytes.Skip(pageContentBytes.Length - 256).ToArray();
                return (cipherBytes, signBytes);
            }
            catch (FormatException e)
            {
                Console.WriteLine("SplitPageContent:- FormatException");
                return null;
            }
        }

        public bool UploadAccessFile(AccessFile accessFile)
        {
            var symmetricReference = accessFile.SymmetricReferenceToSelf;
            Console.WriteLine("UploadAccessFile entered, target pagename=" + symmetricReference.targetPageName);

            var accessFileString = JSONSerialization.SerializeObject(accessFile);
            var accessFileBytes = Encoding.ASCII.GetBytes(accessFileString);

            var cipherTextBytes = Crypto.Encrypt(accessFileBytes, symmetricReference.symmKey);

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
                        accessFile.HasBeenChanged = false;
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
            var encryptedContent = Crypto.Encrypt(keyringBytes, key);
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
                    masterKeyring.HasBeenChanged = false;
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
            var encryptedContent = Crypto.Encrypt(keyringBytes, key.SymmKey);
            if (encryptedContent == null) return false;

            // Sign hash value of cipher text
            var signature = Crypto.SignData(key.PrivateKey!, encryptedContent);

            // Combine encrypted content and signature, then convert to string
            var uploadContentBytes = ByteArrayCombiner.Combine(encryptedContent, signature);
            var uploadContentText = Convert.ToBase64String(uploadContentBytes);

            // Upload encrypted content
            MediaWikiObject.PageAction.UploadNewRevision uploadNewRevision = new(_mwo,
                accessFile.AccessFileReference.targetPageName);
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
                    keyring.HasBeenChanged = false;
                    return true;
                }
            }

            return false;
        }
        
        public AccessFile? DownloadAccessFile(SymmetricReference symmetricReference, string revid = "-1")
        {
            // Download access file
            var accessFileContent = GetPageContent(symmetricReference.targetPageName);
            
            if (accessFileContent.Length < 2)
            {
                Console.WriteLine("DownloadAccessFile:- accessFileContent is null");
                return null;
            }

            var accessFileBytes = Convert.FromBase64String(accessFileContent);
            var decryptedAccessFile = Crypto.Decrypt(accessFileBytes, symmetricReference.symmKey);
            if (decryptedAccessFile == null)
            {
                Console.WriteLine("decryptedAccessFile is null");
                var revisions = GetAllRevisions(symmetricReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidAccessFile(symmetricReference, revisions);
            }

            var decryptedAccessFileString = Encoding.ASCII.GetString(decryptedAccessFile);

            if (!(JSONSerialization.DeserializeObject(
                decryptedAccessFileString, typeof(AccessFile)) is AccessFile accessFile)
                || accessFile.AccessFileReference == null)
            {
                Console.WriteLine("Parsed AccessFile or its AccessFileReference is null, " +
                                  "symmetricReference.targetPageName='{0}'", symmetricReference.targetPageName);
                var revisions = GetAllRevisions(symmetricReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidAccessFile(symmetricReference, revisions);
            }

            accessFile.AccessFileReference.AccessFileParent = accessFile;                
            accessFile.SymmetricReferenceToSelf = symmetricReference;
            
            if (!accessFile.IsValid()) 
            {
                Console.WriteLine("Parsed AccessFile or its AccessFileReference is not valid, " +
                                  "symmetricReference.targetPageName='{0}'", symmetricReference.targetPageName);
                var revisions = GetAllRevisions(symmetricReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidAccessFile(symmetricReference, revisions);
            }
            
            return accessFile;
        }

        public Keyring? DownloadKeyring(SymmetricReference symmetricReference, string revid = "-1")
        {
            if (symmetricReference.targetAccessFile == null)
            {
                // Download access file
                var accessFile = DownloadAccessFile(symmetricReference);
                symmetricReference.targetAccessFile = accessFile;
            }

            // Download master keyring - pageName stored in access File links to rootkeyring
            if (symmetricReference.targetAccessFile == null) return null;
            
            var keyringBytes = Download(symmetricReference.targetAccessFile);
            if (keyringBytes == null)
            {
                var revisions = GetAllRevisions(symmetricReference.targetAccessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidKeyring(symmetricReference.targetAccessFile, revisions);
            }

            var keyringString = Encoding.ASCII.GetString(keyringBytes);
            if (JSONSerialization.DeserializeObject(
                keyringString, typeof(Keyring)) is not Keyring keyring
                || keyring.InboxReferenceToSelf == null)
            {
                Console.WriteLine("Parsed Keyring or its InboxReferenceToSelf is null, " +
                                  "symmetricReference.accessFileTargetPageName='{0}'", symmetricReference.accessFileTargetPageName);
                Console.WriteLine(keyringString);
                var revisions = GetAllRevisions(symmetricReference.targetAccessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidKeyring(symmetricReference.targetAccessFile, revisions);
            }

            keyring.accessFileReferenceToSelf = symmetricReference.targetAccessFile.AccessFileReference;
            symmetricReference.targetAccessFile.AccessFileReference.KeyringTarget = keyring;
            keyring.InboxReferenceToSelf.KeyringTarget = keyring;

            if (!keyring.IsValid())
            {
                Console.WriteLine("Parsed Keyring, its InboxReferenceToSelf, or its Symmetric references are not valid, " +
                                  "symmetricReference.accessFileTargetPageName='{0}'", symmetricReference.accessFileTargetPageName);
                var revisions = GetAllRevisions(symmetricReference.targetAccessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidKeyring(symmetricReference.targetAccessFile, revisions);
            }
            
            return keyring;
        }

        public MasterKeyring? DownloadMasterKeyring(SymmetricReference symmetricReference, string revid = "-1")
        {
            if (symmetricReference.targetAccessFile == null)
            {
                // Download access file
                var accessFile = DownloadAccessFile(symmetricReference);
                symmetricReference.targetAccessFile = accessFile;
            }

            // Download master keyring - pageName stored in access File links to rootkeyring
            if (symmetricReference.targetAccessFile == null) return null;
            
            var keyringBytes = Download(symmetricReference.targetAccessFile);
            if (keyringBytes == null)
            {
                var revisions = GetAllRevisions(symmetricReference.targetAccessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidMasterKeyring(symmetricReference.targetAccessFile, revisions);
            }
            
            var keyringString = Encoding.ASCII.GetString(keyringBytes);

            if (JSONSerialization.DeserializeObject(
                keyringString, typeof(MasterKeyring)) is not MasterKeyring keyring)
            {
                var revisions = GetAllRevisions(symmetricReference.targetAccessFile.AccessFileReference.targetPageName).GetAllRevisionBefore(revid);
                return GetLatestValidMasterKeyring(symmetricReference.targetAccessFile, revisions);
            }

            keyring.accessFileReferenceToSelf = symmetricReference.targetAccessFile.AccessFileReference;

            return keyring;
        }

        public void DownloadKeyringsRecursion(Keyring rootKeyring)
        {
            foreach (var symmRef in rootKeyring.SymmetricReferences)
            {
                if (symmRef.targetAccessFile == null)
                {
                    // Download access file
                    var accessFile = DownloadAccessFile(symmRef);

                    if (accessFile == null)
                    {
                        Console.WriteLine("accessFile is null, continuing");
                        continue;
                    }
                    symmRef.targetAccessFile = accessFile;
                }

                if (symmRef.type == PageType.GenericFile)
                {
                    rootKeyring.AddAccessFile(symmRef.targetAccessFile!);
                }
                else
                {
                    var kr = symmRef.targetAccessFile!.AccessFileReference!.KeyringTarget == null 
                        ? DownloadKeyring(symmRef) : symmRef.targetAccessFile.AccessFileReference.KeyringTarget;

                    if (kr != null)
                    {
                        rootKeyring.AddKeyring(kr);
                        DownloadKeyringsRecursion(kr);
                    }
                    else
                    {
                        Console.WriteLine("Download keyring failed");
                    }
                }
            }
        }

        public Dictionary<Contact, List<string>>? DownloadFromInboxPages()
        {
            // Dict of new content(s) for each contact
            var outputDict = new Dictionary<Contact, List<string>>();

            // Get list of OwnContacts associated with the current server 
            var contactList = _manager.MasterKeyring.GetOwnContactsByServerLink(url);

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
                    byte[] pageContentBytes;
                    try
                    {
                        pageContentBytes = Convert.FromBase64String(entry);
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                    // Split page content into header and ciphertext 
                    var encryptedSymmKey = pageContentBytes.Take(256).ToArray();
                    var encryptedContentBytes = pageContentBytes.Skip(256).ToArray();

                    // Get symmetric key
                    if (contact.InboxReference.privateKey != null)
                    {
                        var decryptedSymmKey =
                            Crypto.RSADecrypt(encryptedSymmKey, contact.InboxReference.privateKey);
                        var symmKey = decryptedSymmKey;

                        if (symmKey == null)
                        {
                            Console.WriteLine("symmKey is null");
                            break;
                        }

                        // Decrypt ciphertext
                        var decryptedContent = Crypto.Decrypt(encryptedContentBytes, symmKey);
                        if (decryptedContent == null)
                        {
                            Console.WriteLine("decryptedContent is null");
                            break;
                        }

                        // Convert plaintext to string
                        var decryptedContentString = Encoding.ASCII.GetString(decryptedContent);

                        contentList.Add(decryptedContentString);
                    }
                }


                if (contentList.Count > 0)
                {
                    outputDict.Add(contact, contentList);
                }
            }

            // Return outputList if it is not empty, null otherwise
            return outputDict.Count > 0 ? outputDict : null;
        }

        private List<string>? GetInboxPageContent(OwnContact contact)
        {
            Console.WriteLine("Fetching updates from inbox page belonging to contact '{0}'.", contact.Nickname);
            List<string> encryptedContentList = new();

            // Get list of all revisions on 
            var allRevs = GetAllRevisions(contact.InboxReference.targetPageName);

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
                if (revid <= contact.RevidCounter)
                {
                    break;
                }

                // Get page content from server
                var pageContent = GetPageContent(contact.InboxReference.targetPageName, revid.ToString());

                if (pageContent.Equals(""))
                {
                    continue;
                }

                encryptedContentList.Add(pageContent);
            }

            // Update contact revision counter so that these revisions don't have to be parsed again
            if (highestRev > contact.RevidCounter)
            {
                contact.RevidCounter = highestRev;
            }

            return encryptedContentList.Count > 0 ? encryptedContentList : null;
        }


        public bool UploadToInboxPage(string pageName, string content, byte[] publicKey)
        {
            Console.WriteLine("Uploading content to mediawiki: " + content);
            // Generate symmetric key
            var symmKey = Crypto.GenerateSymmKey();

            // Encrypt symmetric key with given public key
            var encryptedSymmKeyData = Crypto.RSAEncrypt(symmKey, publicKey);

            // Encrypt content with the symmetric key
            var contentBytes = Encoding.ASCII.GetBytes(content);
            var encryptedBytes = Crypto.Encrypt(
                contentBytes, symmKey);

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
                pageName);
            var httpResponse = uploadNewRevision.UploadContent(pageContent);
            _mwo.editToken ??= uploadNewRevision.editToken;

            // Check if upload was successful
            if (httpResponse != null)
            {
                Console.WriteLine(httpResponse);
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