namespace SecureWiki.Model
{
    public class BaseReference
    {
        public string? reference;
    }
    
    public class SymmetricReference : BaseReference
    {
        public byte[]? symmKey;
        public Type type;
        
    }

    public class AccessFileReference : BaseReference
    {
        public DataFile? DataFile;
    }

    public class InboxReference : BaseReference
    {
        public byte[]? privateKey;
        public byte[]? publicKey;
        public AccessLevel AccessLevel;
    }
    
    public enum Type
    {
        AccessFile,
        Keyring
    }

    public enum AccessLevel
    {
        Read,
        ReadWrite
    }
}