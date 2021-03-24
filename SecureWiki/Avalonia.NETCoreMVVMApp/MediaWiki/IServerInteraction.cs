using System.Collections.Generic;
using SecureWiki.Model;

namespace SecureWiki.MediaWiki
{
    public interface IServerInteraction
    {
        // Returns latest revision stored on server of data file
        public Revision GetLatestRevision(DataFileEntry dataFile);

        // Returns latest revision stored on server of data file,
        // where the data file entry contains decryption key
        public byte[]? GetLatestValidRevision(DataFileEntry dataFile, List<Revision> revisions);

        // Upload byte[] stored in file to server
        public void Upload(DataFileEntry dataFile, string filepath);

        // Returns data stored on server of data file 
        public byte[]? Download(DataFileEntry dataFile);
    }
}