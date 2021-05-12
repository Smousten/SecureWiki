using Avalonia.Input;
using Newtonsoft.Json;
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
    
    [JsonObject(MemberSerialization.OptIn)]
    public class SymmetricReference : Reference
    {
        public enum Type
        {
            GenericFile,
            Keyring
        }
        
        [JsonProperty] public Type type; 
        [JsonProperty] public byte[] symmKey;
        [JsonProperty] public string accessFileTarget;

        public Keyring? keyringParent;

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
        public AccessFile AccessFileParent;

        public AccessFileReference(string pageName, string serverLink, AccessFile accessFileParent, Type type) : base(pageName, serverLink)
        {
            this.AccessFileParent = accessFileParent;
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

        public InboxReference(string pageName, string serverLink, byte[] publicKey) : base(pageName,
            serverLink)
        {
            this.publicKey = publicKey;
            accessLevel = AccessLevel.Write;
        }
        
        public InboxReference(string pageName, string serverLink) : base(pageName,
            serverLink)
        {
            (privateKey, publicKey) = Crypto.GenerateRSAParams();
            accessLevel = AccessLevel.ReadWrite;
        }
    }
    



}