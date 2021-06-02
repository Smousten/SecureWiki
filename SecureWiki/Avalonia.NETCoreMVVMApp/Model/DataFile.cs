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
    public class DataFile : IReactiveObject
    {
        [JsonProperty]
        public string filename { get; set; }
        [JsonProperty]
        public string serverLink { get; set; }
        [JsonProperty]
        public string pageName { get; set; }
        [JsonProperty]
        public byte[]? ownerPrivateKey { get; set; }
        [JsonProperty]
        public byte[]? ownerPublicKey { get; set; }
        
        // List of the page titles of contacts that this file is automatically shared with
        [JsonProperty] 
        public List<(string, string?)> contactList { get; set; }

        // DataFileKey is a tuple of (private key, public key and information relevant to their use)
        [JsonProperty] 
        public List<DataFileKey> keyList { get; set; }

        private Keyring? _parent;
        public Keyring? parent
        {
            get => _parent;
            set
            {
                _parent = value;
                RaisePropertyChanged(nameof(parent));
            }
        }

        private bool? _isChecked = false;
        public bool? isChecked
        {
            get => (_isChecked ?? false);
            set
            {
                _isChecked = value;
                OnPropertyChanged(nameof(isChecked));
                OnPropertyChanged(nameof(isCheckedWriteEnabled));
                OnCheckedChanged(EventArgs.Empty);
            }
        }
        
        private bool? _isCheckedWrite = false;
        public bool? isCheckedWrite
        {
            get
            {
                if (keyList.TrueForAll(e => e.PrivateKey == null))
                {
                    return false;
                }
                return (_isCheckedWrite ?? false);
            }
            set
            {
                _isCheckedWrite = value;
                OnPropertyChanged(nameof(isCheckedWrite));
            }
        }
        
        public bool isCheckedWriteEnabled
        {
            get
            {
                if (keyList.TrueForAll(e => e.PrivateKey == null))
                {
                    return false;
                }
                return isChecked ?? false;
            }
        }

        private bool _newestRevisionSelected = true;
        public bool newestRevisionSelected        
        {
            get => _newestRevisionSelected;
            set
            {
                _newestRevisionSelected = value;
                OnPropertyChanged(nameof(newestRevisionSelected));
            }
        }

        private DataFile()
        {
            
        }
        
        public DataFile(string serverLink, string pageName, string filename = "unnamed")
        {
            this.filename = filename;
            this.serverLink = serverLink;
            this.pageName = pageName;

            // Generate a new asymmetric key pair for owner priv/pub key
            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            ownerPrivateKey = newPrivateKey;
            ownerPublicKey = newPublicKey;

            contactList = new List<(string, string?)>();
            
            // Create a new DataFileKey and sign it with the owner private key 
            keyList = new List<DataFileKey> {new(ownerPrivateKey)};

            // Set event handlers
            CheckedChanged -= CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateParent;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangingEventHandler? PropertyChanging;
        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
            throw new NotImplementedException();
        }

        public void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            throw new NotImplementedException();
        }
        
        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            EventHandler handler = CheckedChanged;
            
            // The Rider IDE incorrectly thinks handler can never be null
            // ReSharper disable once ConstantConditionalAccessQualifier
            handler?.Invoke(this, e);
        }

        public event EventHandler CheckedChanged = null!;

        public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            parent?.UpdateIsCheckedBasedOnChildren();
        }

        public bool IsEqual(DataFile reference)
        {
            return CompareAllPropertiesExcept(reference, null);
        }

        public bool HasSameStaticProperties(DataFile reference)
        {
            List<PropertyInfo?> staticPropertyList = new();
            List<PropertyInfo> compareList = new();
            
            // Add relevant static properties
            staticPropertyList.Add(typeof(DataFile).GetProperty(nameof(pageName)));
            staticPropertyList.Add(typeof(DataFile).GetProperty(nameof(serverLink)));
            staticPropertyList.Add(typeof(DataFile).GetProperty(nameof(ownerPublicKey)));

            // Check properties are not null
            foreach (var item in staticPropertyList)
            {
                if (item != null)
                {
                    compareList.Add(item);
                }
            }

            return CompareProperties(reference, compareList);
        }

        public bool CompareAllPropertiesExcept(DataFile reference, List<PropertyInfo>? ignoreList)
        {
            PropertyInfo[] properties = typeof(DataFile).GetProperties();

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

        private bool CompareProperties(DataFile reference, List<PropertyInfo> propertiesToBeCompared)
        {
            foreach (PropertyInfo prop in propertiesToBeCompared)
            {
                var ownValue = typeof(DataFile).GetProperty(prop.Name)?.GetValue(this, null);
                var refValue = typeof(DataFile).GetProperty(prop.Name)?.GetValue(reference, null);

                // Console.WriteLine("Testing property: '{0}'='{1}'", prop, ownValue);
                
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

        public DataFileKey? GetDataFileKeyByRevisionID(string revid)
        {
            int rev = int.Parse(revid);

            // Find first key where revid is in range
            foreach (DataFileKey dataFileKey in keyList)
            {
                int revStart = int.Parse(dataFileKey.RevisionStart);
                int revEnd = int.Parse(dataFileKey.RevisionEnd);

                if (revStart > rev)
                {
                    continue;
                }

                if (revEnd > rev || revEnd == -1)
                {
                    return dataFileKey;
                }
            }

            // If no valid DataFileKey is found
            return null;
        }

        public void MergeWithOtherDataFileEntry(DataFile df)
        {
            // Abort if any of the static information does not match
            if (!filename.Equals(df.filename) ||
                !serverLink.Equals(df.serverLink) ||
                !pageName.Equals(df.pageName) ||
                (ownerPrivateKey != null && df.ownerPrivateKey != null && !ownerPrivateKey.SequenceEqual(df.ownerPrivateKey)) ||
                (ownerPublicKey != null && df.ownerPublicKey != null && !ownerPublicKey.SequenceEqual(df.ownerPublicKey))
                )
            {
                return;
            }

            // Keep highest level of information
            ownerPrivateKey ??= df.ownerPrivateKey;
            ownerPublicKey ??= df.ownerPublicKey;

            List<DataFileKey> combinedKeyList = new();
            List<DataFileKey> resultingKeyList = new();
            
            // Create and sort combined list of keys
            combinedKeyList.AddRange(keyList);
            combinedKeyList.AddRange(df.keyList);
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

        public void PrepareForExport()
        {
            ownerPrivateKey = null;
            contactList.Clear();
        }

        public (string, string?)? GetContactInfo(string pageTitle, string serverlink)
        {
            (string, string?) output;
            
            // Check if contactList contains any entries with the given page title and server link
            // Serverlink is not saved to file if it matches that of the DataFileEntry
            if (serverlink.Equals(serverLink))
            {
                output = contactList.FirstOrDefault
                    (e => e.Item1.Equals(pageTitle) && e.Item2 == null);
            }
            else
            {
                output = contactList.FirstOrDefault
                    (e => e.Item1.Equals(pageTitle) && e.Item2 == serverlink);
            }
            
            if (output.Equals(default)) return null;
            
            output.Item2 ??= serverLink;
            
            return output;
        }

        public void AddContactInfo(string pageTitle, string serverlink)
        {
            (string, string?) existingContactInfo;
            
            // Check if contact with same pageTitle already is in contactList
            // Serverlink is not saved to file if it matches that of the DataFileEntry
            if (serverlink.Equals(serverLink))
            {
                existingContactInfo = contactList.FirstOrDefault
                    (e => e.Item1.Equals(pageTitle) && e.Item2 == null);
            }
            else
            {
                existingContactInfo = contactList.FirstOrDefault
                    (e => e.Item1.Equals(pageTitle) && e.Item2 == serverlink);
            }

            // If conflicting entry does not exist
            if (existingContactInfo.Equals(default))
            {
                contactList.Add(serverlink.Equals(serverLink) ? (pageTitle, null) : (pageTitle, serverlink));
                return;
            }

            // If exact contact already exists 
            if ((existingContactInfo.Item2 == null && serverlink.Equals(serverLink)) ||
                (existingContactInfo.Item2 != null && existingContactInfo.Item2.Equals(serverlink))) 
            {
                return;
            }
            // If there is a conflict
            else
            {
                contactList.Add(serverlink.Equals(serverLink) ? (pageTitle, null) : (pageTitle, serverlink));
            }
        }

        // Creates a new DataFileEntry and copies most of own properties over
        public DataFile Copy()
        {
            // var copy = new DataFileEntry();
            // copy.filename = filename;
            // copy.serverLink = serverLink;
            // copy.pageName = pageName;
            // copy.ownerPrivateKey = ownerPrivateKey;
            // copy.ownerPublicKey = ownerPublicKey;
            // copy.contactList = contactList;
            // copy.keyList = new List<DataFileKey>();

            var jsonData = JSONSerialization.SerializeObject(this);

            DataFile copy = (JSONSerialization.DeserializeObject(jsonData, typeof(DataFile)) as DataFile)!;

            copy.isChecked = isChecked;
            copy.isCheckedWrite = isCheckedWrite;
            
            return copy;
        }

        public void PrintInfo()
        {
            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}'", 
                filename, isChecked, parent?.name ?? "null");
        }
    }
}