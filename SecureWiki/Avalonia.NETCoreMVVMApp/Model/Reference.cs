using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Input;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Reference
    {
        [JsonProperty(Order = -11)] public string serverLink { get; set; }
        [JsonProperty(Order = -10)] public string targetPageName { get; set; }

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
        [JsonProperty(Order = -9)] public PageType type;
        [JsonProperty(Order = -8)] public string accessFileTargetPageName;
        [JsonProperty(Order = -7)] public byte[] symmKey;
        
        public AccessFile? targetAccessFile { get; set; }
        public Keyring? keyringParent;

        public SymmetricReference(string targetPageName, string serverLink, PageType type, 
            string accessFileTargetPageName, AccessFile targetAccessFile) : base(targetPageName, serverLink)
        {
            this.symmKey = Crypto.GenerateSymmKey();
            this.type = type;
            this.accessFileTargetPageName = accessFileTargetPageName;
            this.targetAccessFile = targetAccessFile;
        }
        
        public bool IsValid()
        {
            var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            properties.Add(this.GetType().GetProperty(nameof(targetPageName)));
            properties.Add(this.GetType().GetProperty(nameof(serverLink)));

            // Fields to be checked
            fields.Add(this.GetType().GetField(nameof(symmKey)));
            fields.Add(this.GetType().GetField(nameof(accessFileTargetPageName)));
            fields.Add(this.GetType().GetField(nameof(type)));
            
            return properties.All(prop => prop?.GetValue(this) != null) 
                   && fields.All(field => field?.GetValue(this) != null);
        }
        
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AccessFileReference : Reference
    {
        [JsonProperty(Order = -9)] public PageType type;
        public AccessFile? AccessFileParent;
        public Keyring? KeyringTarget;

        public AccessFileReference(string targetPageName, string serverLink, PageType type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }

        public AccessFileReference()
        {
            
        }
        
        public AccessFileReference(string targetPageName, string serverLink, AccessFile accessFileParent, PageType type, Keyring? keyringTarget = null) : base(targetPageName, serverLink)
        {
            this.AccessFileParent = accessFileParent;
            this.type = type;
            this.KeyringTarget = keyringTarget;
        }
        
        public bool IsValid()
        {
            var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            properties.Add(typeof(AccessFileReference).GetProperty(nameof(targetPageName)));
            properties.Add(typeof(AccessFileReference).GetProperty(nameof(serverLink)));
            
            // Fields to be checked
            fields.Add(typeof(AccessFileReference).GetField(nameof(AccessFileParent)));
            fields.Add(this.GetType().GetField(nameof(type)));
            
            return properties.All(prop => prop?.GetValue(this) != null) 
                   && fields.All(field => field?.GetValue(this) != null);
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
        
        [JsonProperty(Order = 98)] public byte[] publicKey;
        [JsonProperty(Order = 99)] public byte[]? privateKey;
        [JsonProperty(Order = -2)] public AccessLevel accessLevel;
        public Keyring KeyringTarget;

        public InboxReference(string targetPageName, string serverLink, byte[] publicKey, Keyring keyringTarget) : base(targetPageName,
            serverLink)
        {
            this.publicKey = publicKey;
            accessLevel = AccessLevel.Write;
            KeyringTarget = keyringTarget;
        }
        
        public InboxReference(string targetPageName, string serverLink, Keyring keyringTarget) : base(targetPageName,
            serverLink)
        {
            (privateKey, publicKey) = Crypto.GenerateRSAParams();
            accessLevel = AccessLevel.ReadWrite;
            KeyringTarget = keyringTarget;
        }
        
        public InboxReference() {}
        
        public bool IsValid()
        {
            var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            properties.Add(typeof(InboxReference).GetProperty(nameof(targetPageName)));
            properties.Add(typeof(InboxReference).GetProperty(nameof(serverLink)));
            
            // Fields to be checked
            fields.Add(typeof(InboxReference).GetField(nameof(publicKey)));
            fields.Add(typeof(InboxReference).GetField(nameof(accessLevel)));
            
            return properties.All(prop => prop?.GetValue(this) != null) 
                   && fields.All(field => field?.GetValue(this) != null);
        }
    }
    
    public enum PageType
    {
        GenericFile,
        Keyring
    }
}