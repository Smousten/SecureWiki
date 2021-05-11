namespace SecureWiki.Model
{
    public abstract class Reference
    {
        public string pageName;
        public string serverLink;

        public Reference(string pageName, string serverLink)
        {
            this.pageName = pageName;
            this.serverLink = serverLink;
        }
    }
    
    public class SymmetricReference : Reference
    {
        public enum Type
        {
            GenericFile,
            Keyring
        }
        
        public Type type;
        public byte[] symmKey;
        public string accessFileTarget;

        public SymmetricReference(string pageName, string serverLink, byte[] symmKey, Type type, string accessFileTarget) : base(pageName, serverLink)
        {
            this.symmKey = symmKey;
            this.type = type;
            this.accessFileTarget = accessFileTarget;
        }
        
    }

    public class AccessFileReference : Reference
    {
        public enum Type
        {
            GenericFile,
            Keyring
        }

        public Type type;
        public DataFile dataFile;

        public AccessFileReference(string pageName, string serverLink, DataFile dataFile, Type type) : base(pageName, serverLink)
        {
            this.dataFile = dataFile;
            this.type = type;
        }
        
    }

    public class InboxReference : Reference
    {
        public enum AccessLevel
        {
            Write,
            ReadWrite
        }
        
        public byte[] publicKey;
        public byte[]? privateKey;
        public AccessLevel accessLevel;

        public InboxReference(string pageName, string serverLink, AccessLevel accessLevel, byte[] publicKey, byte[]? privateKey = null) : base(pageName,
            serverLink)
        {
            this.accessLevel = accessLevel;
            this.publicKey = publicKey;
            this.privateKey = privateKey;
        }
    }
    



}