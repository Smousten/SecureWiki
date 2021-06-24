using System.Collections.Generic;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWiki.MediaWiki
{
    public interface IServerInteraction
    {
        // Returns latest revision stored on server of data file
        public Revision GetLatestRevision(string pageName);

        // Returns latest revision stored on server of data file,
        // where the data file entry contains decryption key
        public byte[]? GetLatestValidRevision(AccessFile accessFile, List<Revision> revisions);

        // Returns all revisions stored on the server with the given pageTitle
        public MediaWikiObject.PageQuery.AllRevisions GetAllRevisions(string pageName);

        // Upload unencrypted content to server
        public void Upload(string pageName, string content);
        
        // Upload byte[] stored in file to server
        // public bool Upload(AccessFile accessFile, string filepath);
        public bool Upload(AccessFile accessFile, byte[] content);
        
        // Upload using a Symmetric Reference and its key
        public bool Upload(SymmetricReference symmetricReference, byte[] content);

        // Returns decrypted data stored on server of data file with given revID
        public byte[]? Download(AccessFile accessFile, string? revid = null);
        
        // Returns decrypted content of a file protected by a Symmetric Reference for a given revision id
        public byte[]? Download(SymmetricReference symmetricReference, string? revid = null);

        // Returns the id of the newest revision of a page on the server, or null if no revision is found
        public string? GetLatestRevisionID(string pageName);
        
        // Returns encrypted string stored on server of page with given title and revision ID
        public string GetPageContent(string pageName, string revID = "-1");
        
        // Check if page exists on server
        public bool PageAlreadyExists(string pageName, string revID);
        
        // Undo revisions with given page title from revisions with ID between startID to endID
        public void UndoRevisionsByID(string pageName, string startID, string endID);

        // Delete revisions with given page title and IDs
        public void DeleteRevisionsByID(string pageName, string IDs);

        // Encrypt and upload DataFileEntry to specified page
        public bool UploadToInboxPage(string pageName, string content, byte[] publicKey);

        // TODO
        public Dictionary<Contact, List<string>>? DownloadFromInboxPages();

        // Download and return all new Inbox Page entries as a list of strings
        public List<string> DownloadFromInboxPage(OwnContact contact);
        
        // TODO
        public bool UploadAccessFile(AccessFile accessFile);
        
        // TODO
        public bool UploadKeyring(AccessFile accessFile, Keyring keyring);
        
        // TODO
        public bool UploadMasterKeyring(byte[] key, string pageName, Keyring masterKeyring);
        
        // TODO
        public AccessFile? DownloadAccessFile(SymmetricReference symmetricReference, string revid = "-1");

        // TODO
        public Keyring? DownloadKeyring(SymmetricReference symmetricReference, string revid = "-1");

        // TODO
        public MasterKeyring? DownloadMasterKeyring(SymmetricReference symmetricReference, string revid = "-1");
        
        // TODO
        public void DownloadKeyringsRecursion(Keyring rootKeyring);
    }
}