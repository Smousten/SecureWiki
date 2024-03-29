using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;
using SecureWiki.Cryptography;
using SecureWiki.Utilities;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AccessFile //: IReactiveObject
    {
        [JsonProperty(Order = 10)]
        public string filename { get; set; }
        [JsonProperty(Order = 10)]
        public byte[]? ownerPrivateKey { get; set; }
        [JsonProperty(Order = 9)]
        public byte[]? ownerPublicKey { get; set; }
        
        // List of inbox references which 'subscribe' to this access file
        [JsonProperty] public List<InboxReference> inboxReferences { get; set; }

        // AccessFileKey is a tuple of (private key, public key, semi-permanent symmetric key
        // and information relevant to their use)
        [JsonProperty] 
        public List<AccessFileKey> keyList { get; set; }

        [JsonProperty] 
        public AccessFileReference AccessFileReference;

        public SymmetricReference? SymmetricReferenceToSelf;
        public Keyring? Parent;
        
        public bool HasBeenChanged;
        public bool HasTargetBeenChanged;

        private bool _newestRevisionSelected = true;
        public bool newestRevisionSelected        
        {
            get => _newestRevisionSelected;
            set
            {
                _newestRevisionSelected = value;
                // OnPropertyChanged(nameof(newestRevisionSelected));
            }
        }

        [JsonConstructor]
        private AccessFile()
        {
            
        }
        
        public AccessFile(string serverLink, string pageName, PageType pageType, Keyring? keyringTarget = null)
        {
            AccessFileReference = new AccessFileReference(pageName, serverLink, this, 
                pageType, keyringTarget);
            this.filename = "unnamed";
            HasBeenChanged = true;

            // Generate a new asymmetric key pair for owner priv/pub key
            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            ownerPrivateKey = newPrivateKey;
            ownerPublicKey = newPublicKey;
            
            inboxReferences = new List<InboxReference>();
            
            // Create a new AccessFileKey and sign it with the owner private key 
            keyList = new List<AccessFileKey> {new(ownerPrivateKey)};
        }

        public bool IsValid()
        {
            var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            properties.Add(typeof(AccessFile).GetProperty(nameof(ownerPublicKey)));
            properties.Add(typeof(AccessFile).GetProperty(nameof(keyList)));
            
            // Fields to be checked
            fields.Add(typeof(AccessFile).GetField(nameof(AccessFileReference)));
            fields.Add(typeof(AccessFile).GetField(nameof(SymmetricReferenceToSelf)));
            
            foreach (var prop in properties)
            {
                var val = prop?.GetValue(this);
                if (val == null)
                {
                    return false;
                }

                if (val is string {Length: < 2})
                {
                    return false;
                }
                
                if (val is byte[] {Length: < 2})
                {
                    return false;
                }
            }
            
            foreach (var field in fields)
            {
                if (field?.GetValue(this) == null)
                {
                    return false;
                }
            }

            // Check if reference is also valid
            return AccessFileReference.IsValid();
        }

        public bool VerifyKeys()
        {
            foreach (var key in keyList)
            {
                if (ownerPublicKey == null || 
                    (key.PrivateKey != null && 
                     !Crypto.VerifyData(ownerPublicKey, key.PrivateKey, key.SignedWriteKey)) ||
                    !Crypto.VerifyData(ownerPublicKey, 
                        ByteArrayCombiner.Combine(key.SymmKey, key.PublicKey), key.SignedReadKeys))
                {
                    return false;
                }
            }

            return true;
        }

        // public event PropertyChangedEventHandler? PropertyChanged;
        // public event PropertyChangingEventHandler? PropertyChanging;
        // public void RaisePropertyChanging(PropertyChangingEventArgs args)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public void RaisePropertyChanged(PropertyChangedEventArgs args)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public void RaisePropertyChanged(string propertyName)
        // {
        //     PropertyChangedEventHandler? handler = PropertyChanged;
        //     handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
        //
        // [NotifyPropertyChangedInvocator]
        // protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        // {
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
        //
        // protected virtual void OnCheckedChanged(EventArgs e)
        // {
        //     EventHandler handler = CheckedChanged;
        //     
        //     // The Rider IDE incorrectly thinks handler can never be null
        //     // ReSharper disable once ConstantConditionalAccessQualifier
        //     handler?.Invoke(this, e);
        // }
        //
        // public event EventHandler CheckedChanged = null!;
        //
        // public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        // {
        //     Parent?.UpdateIsCheckedBasedOnChildren();
        // }

        public bool IsEqual(AccessFile reference)
        {
            return CompareAllPropertiesExcept(reference, null);
        }

        public bool HasSameStaticProperties(AccessFile reference)
        {
            List<PropertyInfo?> staticPropertyList = new();
            List<PropertyInfo> compareList = new();
            
            // Add relevant static properties
            staticPropertyList.Add(typeof(AccessFile).GetProperty(nameof(ownerPublicKey)));

            // Check properties are not null
            foreach (var item in staticPropertyList)
            {
                if (item != null)
                {
                    compareList.Add(item);
                }
            }

            // Check both own and AccessFileReference properties
            return CompareProperties(reference, compareList)
                   && AccessFileReference.HasSameStaticProperties(reference.AccessFileReference);
        }

        public bool CompareAllPropertiesExcept(AccessFile reference, List<PropertyInfo>? ignoreList)
        {
            PropertyInfo[] properties = typeof(AccessFile).GetProperties();

            List<PropertyInfo> propertiesToBeCompared = new();

            foreach (PropertyInfo prop in properties)
            {
                if (ignoreList?.Contains(prop) == true)
                {
                    continue;
                }

                propertiesToBeCompared.Add(prop);
            }

            return CompareProperties(reference, propertiesToBeCompared);
        }

        private bool CompareProperties(AccessFile reference, List<PropertyInfo> propertiesToBeCompared)
        {
            foreach (PropertyInfo prop in propertiesToBeCompared)
            {
                var ownValue = typeof(AccessFile).GetProperty(prop.Name)?.GetValue(this, null);
                var refValue = typeof(AccessFile).GetProperty(prop.Name)?.GetValue(reference, null);
                
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

        // Check if revision ID is in range specified for that key pair
        public bool IsValidRevisionID(string revid, int i)
        {
            // If KeyList index doesn't exist
            if (i >= keyList.Count)
            {
                return false;
            }
    
            // Parse revisions IDs to int 
            int rev = int.Parse(revid);
            int revStart = int.Parse(keyList[i].RevisionStart);
            int revEnd = int.Parse(keyList[i].RevisionEnd);
            
            bool revEndNotSet = keyList[i].RevisionEnd.Equals("-1");
            
            return revStart <= rev && (revEnd >= rev || revEndNotSet);
        }

        public AccessFileKey? GetAccessFileKeyByRevisionID(string revid)
        {
            int rev = int.Parse(revid);

            // Find first key where revid is in range
            foreach (AccessFileKey accessFileKey in keyList)
            {
                int revStart = int.Parse(accessFileKey.RevisionStart);
                int revEnd = int.Parse(accessFileKey.RevisionEnd);

                if (revStart > rev)
                {
                    continue;
                }

                if (revEnd > rev || revEnd == -1)
                {
                    return accessFileKey;
                }
            }

            // If no valid AccessFileKey is found
            return null;
        }

        public void MergeWithOtherAccessFileEntry(AccessFile af)
        {
            // Abort if any of the static information does not match
            if (!filename.Equals(af.filename) ||
                !AccessFileReference.serverLink.Equals(af.AccessFileReference.serverLink) ||
                !AccessFileReference.targetPageName.Equals(af.AccessFileReference.targetPageName) ||
                (ownerPrivateKey != null && af.ownerPrivateKey != null && !ownerPrivateKey.SequenceEqual(af.ownerPrivateKey)) ||
                (ownerPublicKey != null && af.ownerPublicKey != null && !ownerPublicKey.SequenceEqual(af.ownerPublicKey))
                )
            {
                return;
            }

            // Keep highest level of information
            ownerPrivateKey ??= af.ownerPrivateKey;
            ownerPublicKey ??= af.ownerPublicKey;

            List<AccessFileKey> combinedKeyList = new();
            List<AccessFileKey> resultingKeyList = new();
            
            // Create and sort combined list of keys
            combinedKeyList.AddRange(keyList);
            combinedKeyList.AddRange(af.keyList);
            combinedKeyList = combinedKeyList.OrderBy(
                entry => entry.PublicKey, new ByteArrayComparer()).ToList();

            // Merge keyLists and remove duplicates 
            int i = 0;
            while (i < combinedKeyList.Count)
            {
                int cnt = 1;

                while (i + cnt < combinedKeyList.Count && 
                       combinedKeyList[i].PublicKey.SequenceEqual(combinedKeyList[i + cnt].PublicKey))
                {
                    combinedKeyList[i].MergeWithOtherKey(combinedKeyList[i + cnt]);
                    cnt++;
                }
                
                resultingKeyList.Add(combinedKeyList[i]);
                i += cnt;
            }
            
            // Sort resulting list and update own keyList
            resultingKeyList = resultingKeyList.OrderBy(entry => entry.RevisionStart).ToList();
            keyList.Clear();
            keyList.AddRange(resultingKeyList);
        }

        public void PrepareForExport(bool writeEnabled)
        {
            ownerPrivateKey = null;
            if (!writeEnabled)
            {
                foreach (var key in keyList)
                {
                    key.PrivateKey = null;
                    key.SignedWriteKey = null;
                }
            }
            
            inboxReferences.Clear();
        }

        // public void PrepareForExport()
        // {
        //     ownerPrivateKey = null;
        //     contactList.Clear();
        // }
        //
        // public (string, string?)? GetContactInfo(string pageTitle, string serverlink)
        // {
        //     (string, string?) output;
        //     
        //     // Check if contactList contains any entries with the given page title and server link
        //     // Serverlink is not saved to file if it matches that of the AccessFileEntry
        //     if (serverlink.Equals(serverLink))
        //     {
        //         output = contactList.FirstOrDefault
        //             (e => e.Item1.Equals(pageTitle) && e.Item2 == null);
        //     }
        //     else
        //     {
        //         output = contactList.FirstOrDefault
        //             (e => e.Item1.Equals(pageTitle) && e.Item2 == serverlink);
        //     }
        //     
        //     if (output.Equals(default)) return null;
        //     
        //     output.Item2 ??= serverLink;
        //     
        //     return output;
        // }
        //
        // public void AddContactInfo(string pageTitle, string serverlink)
        // {
        //     (string, string?) existingContactInfo;
        //     
        //     // Check if contact with same pageTitle already is in contactList
        //     // Serverlink is not saved to file if it matches that of the AccessFileEntry
        //     if (serverlink.Equals(serverLink))
        //     {
        //         existingContactInfo = contactList.FirstOrDefault
        //             (e => e.Item1.Equals(pageTitle) && e.Item2 == null);
        //     }
        //     else
        //     {
        //         existingContactInfo = contactList.FirstOrDefault
        //             (e => e.Item1.Equals(pageTitle) && e.Item2 == serverlink);
        //     }
        //
        //     // If conflicting entry does not exist
        //     if (existingContactInfo.Equals(default))
        //     {
        //         contactList.Add(serverlink.Equals(serverLink) ? (pageTitle, null) : (pageTitle, serverlink));
        //         return;
        //     }
        //
        //     // If exact contact already exists 
        //     if ((existingContactInfo.Item2 == null && serverlink.Equals(serverLink)) ||
        //         (existingContactInfo.Item2 != null && existingContactInfo.Item2.Equals(serverlink))) 
        //     {
        //         return;
        //     }
        //     // If there is a conflict
        //     else
        //     {
        //         contactList.Add(serverlink.Equals(serverLink) ? (pageTitle, null) : (pageTitle, serverlink));
        //     }
        // }

        // Creates a new AccessFileEntry and copies most of own properties over
        public AccessFile Copy()
        {
            // var copy = new AccessFileEntry();
            // copy.filename = filename;
            // copy.serverLink = serverLink;
            // copy.pageName = pageName;
            // copy.ownerPrivateKey = ownerPrivateKey;
            // copy.ownerPublicKey = ownerPublicKey;
            // copy.contactList = contactList;
            // copy.keyList = new List<AccessFileKey>();

            var jsonData = JSONSerialization.SerializeObject(this);

            AccessFile copy = (JSONSerialization.DeserializeObject(jsonData, typeof(AccessFile)) as AccessFile)!;

            // copy.isChecked = isChecked;
            // copy.isCheckedWrite = isCheckedWrite;
            
            return copy;
        }

        public void PrintInfo()
        {
            // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}'", 
            //     filename, isChecked, parent?.name ?? "null");
            Console.WriteLine("AccessFile: filename='{0}', Parent.Name='{1}'", 
                filename, Parent?.name ?? "null");
        }
    }
}