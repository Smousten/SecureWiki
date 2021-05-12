using Avalonia.Input;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    public abstract class Reference
    {
        public string targetPageName;
        public string serverLink;

        public Reference(string targetPageName, string serverLink)
        {
            this.targetPageName = targetPageName;
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
        [JsonProperty] public string accessFileTargetPageName;
        [JsonProperty] public AccessFile targetAccessFile;

        public Keyring? keyringParent;

        public SymmetricReference(string targetPageName, string serverLink, Type type, 
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
        public enum Type
        {
            GenericFile,
            Keyring
        }

        [JsonProperty] public Type type;
        public AccessFile? AccessFileParent;
        [JsonProperty] public Keyring? KeyringTarget;

        public AccessFileReference(string targetPageName, string serverLink, Type type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }
        
        public AccessFileReference(string targetPageName, string serverLink, AccessFile accessFileParent, Type type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.AccessFileParent = accessFileParent;
            this.type = type;
            this.KeyringTarget = keyringTarget;
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
    



}