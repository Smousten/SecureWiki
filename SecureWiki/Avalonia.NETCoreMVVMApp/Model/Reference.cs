using Avalonia.Input;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Reference
    {
        [JsonProperty] public string targetPageName;
        [JsonProperty] public string serverLink;

        public Reference(string targetPageName, string serverLink)
        {
            this.targetPageName = targetPageName;
            this.serverLink = serverLink;
        }

        public Reference()
        {
            
        }
    }
    
    [JsonObject(MemberSerialization.OptIn)]
    public class SymmetricReference : Reference
    {

        [JsonProperty] public PageType type; 
        [JsonProperty] public byte[] symmKey;
        [JsonProperty] public string accessFileTargetPageName;
        // [JsonProperty(Order = 99)] 
        public AccessFile? targetAccessFile;

        public Keyring? keyringParent;

        public SymmetricReference(string targetPageName, string serverLink, PageType type, 
            string accessFileTargetPageName, AccessFile targetAccessFile) : base(targetPageName, serverLink)
        {
            this.symmKey = Crypto.GenerateSymmKey();
            this.type = type;
            this.accessFileTargetPageName = accessFileTargetPageName;
            this.targetAccessFile = targetAccessFile;
        }
        
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AccessFileReference : Reference
    {
        [JsonProperty] public PageType type;
        public AccessFile? AccessFileParent;
        [JsonProperty(Order = 99)] public Keyring? KeyringTarget;

        public AccessFileReference(string targetPageName, string serverLink, PageType type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }

        public AccessFileReference()
        {
            
        }
        
        public AccessFileReference(AccessFile accessFileParent, PageType type, Keyring? keyringTarget = null) : base(accessFileParent.pageName, accessFileParent.serverLink)
        {
            this.AccessFileParent = accessFileParent;
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }
        
        public AccessFileReference(string targetPageName, string serverLink, AccessFile accessFileParent, PageType type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.AccessFileParent = accessFileParent;
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class InboxReference : Reference
    {
        public enum AccessLevel
        {
            Write,
            ReadWrite
        }
        
        [JsonProperty] public byte[] publicKey;
        [JsonProperty] public byte[]? privateKey;
        [JsonProperty(Order = -2)] public AccessLevel accessLevel;

        public InboxReference(string targetPageName, string serverLink, byte[] publicKey) : base(targetPageName,
            serverLink)
        {
            this.publicKey = publicKey;
            accessLevel = AccessLevel.Write;
        }
        
        public InboxReference(string targetPageName, string serverLink) : base(targetPageName,
            serverLink)
        {
            (privateKey, publicKey) = Crypto.GenerateRSAParams();
            accessLevel = AccessLevel.ReadWrite;
        }
    }
    
    public enum PageType
    {
        GenericFile,
        Keyring
    }
}