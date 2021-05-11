using System.Collections.Generic;
using SecureWiki.Model;

namespace SecureWiki.MediaWiki
{
    public interface IServerInteraction
    {
        // Returns latest revision stored on server of data file
        public Revision GetLatestRevision(AccessFile accessFile);

        // Returns latest revision stored on server of data file,
        // where the data file entry contains decryption key
        public byte[]? GetLatestValidRevision(AccessFile accessFile, List<Revision> revisions);

        // Returns all revisions stored on the server with the given pageTitle
        public MediaWikiObject.PageQuery.AllRevisions GetAllRevisions(string pageTitle);
        
        // Upload byte[] stored in file to server
        public bool Upload(AccessFile accessFile, string filepath);

        // Returns decrypted data stored on server of data file with given revID
        public byte[]? Download(AccessFile accessFile, string? revID = null);

        // Returns the id of the newest revision of a page on the server, or null if no revision is found
        public string? GetLatestRevisionID(string pageName);
        
        // Returns encrypted string stored on server of page with given title and revision ID
        public string GetPageContent(string pageTitle, string revID);
        
        // Check if page exists on server
        public bool PageAlreadyExists(string pageTitle, string revID);
        
        // Undo revisions with given page title from revisions with ID between startID to endID
        public void UndoRevisionsByID(string pageTitle, string startID, string endID);

        // Delete revisions with given page title and IDs
        public void DeleteRevisionsByID(string pageTitle, string IDs);

        // Encrypt and upload DataFileEntry to specified page
        public bool UploadToInboxPage(string pageTitle, string content, byte[] publicKey);

        // TODO
        public List<List<string>>? DownloadFromInboxPages();
        
        // TODO
        public void UploadAccessFile(SymmetricReference symmetricReference, AccessFile accessFile);
        
        // TODO
        public bool UploadKeyring(AccessFile accessFile, Keyring keyring);
        
        // TODO
        public AccessFile? DownloadAccessFile(SymmetricReference symmetricReference);

        // TODO
        public Keyring? DownloadKeyring(SymmetricReference symmetricReference);

        // TODO
        public void DownloadKeyringsRecursion(AccessFile accessFile, RootKeyring rootKeyring);
    }
}