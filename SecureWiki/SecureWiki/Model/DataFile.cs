namespace SecureWiki.Model
{
    public class DataFile
    {
        public string fileName { get; set; } 
        public byte[] symmKey { get; set; }
        public byte[] iv { get; set; }
        public byte[] privateKey { get; set; }
        public byte[] publicKey { get; set; }
        public int revisionNr { get; set; }
        public string serverLink { get; set; }
        public string pageName { get; set; }
    }
}