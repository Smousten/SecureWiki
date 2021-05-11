using SecureWiki.Cryptography;

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

        public SymmetricReference(string pageName, string serverLink, Type type, string accessFileTarget) : base(pageName, serverLink)
        {
            this.symmKey = Crypto.GenerateSymmKey();
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
        public AccessFile AccessFile;

        public AccessFileReference(string pageName, string serverLink, AccessFile accessFile, Type type) : base(pageName, serverLink)
        {
            this.AccessFile = accessFile;
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

        public InboxReference(string pageName, string serverLink, AccessLevel accessLevel) : base(pageName,
            serverLink)
        {
            this.accessLevel = accessLevel;
            
            if (accessLevel == AccessLevel.Write)
            {
                (privateKey, publicKey) = Crypto.GenerateRSAParams();
            }
            else
            {
                (_, publicKey) = Crypto.GenerateRSAParams();
            }
        }
    }
    



}