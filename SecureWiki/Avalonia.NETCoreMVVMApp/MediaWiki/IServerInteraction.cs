using System.Collections.Generic;
using SecureWiki.Model;

namespace SecureWiki.MediaWiki
{
    public interface IServerInteraction
    {
        // Returns latest revision stored on server of data file
        public Revision GetLatestRevision(DataFile dataFile);

        // Returns latest revision stored on server of data file,
        // where the data file entry contains decryption key
        public byte[]? GetLatestValidRevision(DataFile dataFile, List<Revision> revisions);

        // Returns all revisions stored on the server with the given pageTitle
        public MediaWikiObjects.PageQuery.AllRevisions GetAllRevisions(string pageTitle);
        
        // Upload byte[] stored in file to server
        public bool Upload(DataFile dataFile, string filepath);

        // Returns decrypted data stored on server of data file 
        public byte[]? Download(DataFile dataFile);
        
        // Returns decrypted data stored on server of data file with given revID
        public byte[]? Download(DataFile dataFile, string revID);
        
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
    }
}