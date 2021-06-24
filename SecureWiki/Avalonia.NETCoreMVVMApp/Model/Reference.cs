using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Input;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using SecureWiki.Cryptography;
using SecureWiki.Utilities;

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
        public MDFile? MDFile;

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

        public bool HasSameStaticProperties(AccessFileReference other)
        {
            List<PropertyInfo?> staticPropertyList = new();
            List<PropertyInfo> compareList = new();
            
            // Add relevant static properties
            staticPropertyList.Add(this.GetType().GetProperty(nameof(targetPageName)));
            staticPropertyList.Add(this.GetType().GetProperty(nameof(serverLink)));
            staticPropertyList.Add(this.GetType().GetProperty(nameof(type)));
            
            // Check properties are not null
            foreach (var item in staticPropertyList)
            {
                if (item != null)
                {
                    compareList.Add(item);
                }
            }

            foreach (PropertyInfo prop in compareList)
            {
                var ownValue = this.GetType().GetProperty(prop.Name)?.GetValue(this, null);
                var refValue = this.GetType().GetProperty(prop.Name)?.GetValue(other, null);
                
                if (ownValue == null || refValue == null)
                {
                    if (ownValue != null || refValue != null)
                    {
                        return false;
                    }
                }
                else if (ownValue is string)
                {
                    if (!(ownValue.Equals(refValue)))
                    {
                        return false;
                    }
                }
                else if (ownValue.GetType() == typeof(byte[]))
                {
                    var byteArrayOwn = ownValue as byte[];
                    var byteArrayRef = refValue as byte[];
                    if (!(byteArrayOwn!).SequenceEqual(byteArrayRef!))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class InboxReference : Reference
    {
        public enum AccessLevel
        {
            Read,
            ReadWrite
        }
        
        [JsonProperty(Order = 98)] public byte[] publicKey;
        [JsonProperty(Order = 99)] public byte[]? privateKey;
        [JsonProperty(Order = -2)] public AccessLevel? accessLevel;
        
        public InboxReference(string targetPageName, string serverLink, byte[] publicKey,
            AccessLevel? accessLevel = null) : base(targetPageName, serverLink)
        {
            this.accessLevel = accessLevel;
            this.publicKey = publicKey;
        }
        
        public InboxReference(string targetPageName, string serverLink, AccessLevel? accessLevel = null) : base(targetPageName, serverLink)
        {
            this.accessLevel = accessLevel;
            (privateKey, publicKey) = Crypto.GenerateRSAParams();
        }
        
        public InboxReference() {}

        public InboxReference Copy(AccessLevel accessLevel)
        {
            var jsonData = JSONSerialization.SerializeObject(this);

            var copy = (JSONSerialization.DeserializeObject(jsonData, typeof(InboxReference)) as InboxReference)!;

            copy.privateKey = null;
            
            if (accessLevel == AccessLevel.Read)
            {
                copy.accessLevel = AccessLevel.Read;
            }
            
            return copy;
        }
        
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
        
        public bool HasSameStaticProperties(InboxReference other)
        {
            List<PropertyInfo?> staticPropertyList = new();
            List<PropertyInfo> compareList = new();
            
            // Add relevant static properties
            staticPropertyList.Add(this.GetType().GetProperty(nameof(targetPageName)));
            staticPropertyList.Add(this.GetType().GetProperty(nameof(serverLink)));
            staticPropertyList.Add(this.GetType().GetProperty(nameof(publicKey)));
            
            // Check properties are not null
            foreach (var item in staticPropertyList)
            {
                if (item != null)
                {
                    compareList.Add(item);
                }
            }

            foreach (PropertyInfo prop in compareList)
            {
                var ownValue = this.GetType().GetProperty(prop.Name)?.GetValue(this, null);
                var refValue = this.GetType().GetProperty(prop.Name)?.GetValue(other, null);
                
                if (ownValue == null || refValue == null)
                {
                    if (ownValue != null || refValue != null)
                    {
                        return false;
                    }
                }
                else if (ownValue is string)
                {
                    if (!(ownValue.Equals(refValue)))
                    {
                        return false;
                    }
                }
                else if (ownValue.GetType() == typeof(byte[]))
                {
                    var byteArrayOwn = ownValue as byte[];
                    var byteArrayRef = refValue as byte[];
                    if (!(byteArrayOwn!).SequenceEqual(byteArrayRef!))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
    
    public enum PageType
    {
        GenericFile,
        Keyring
    }
}