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
    public class DataFileEntry : IReactiveObject
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

        // Tuple of (Start revision id, public key, private key, end revision)
        [JsonProperty] 
        public List<DataFileKey> keyList { get; set; }

        private KeyringEntry? _parent;
        public KeyringEntry? parent
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
                // if (privateKey == null)
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
                // if (privateKey == null)
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
        
        public DataFileEntry(string serverLink, string filename = "unnamed")
        {
            this.filename = filename;
            this.serverLink = serverLink;

            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            ownerPrivateKey = newPrivateKey;
            ownerPublicKey = newPublicKey;

            pageName = RandomString.GenerateRandomAlphanumericString();
            keyList = new List<DataFileKey> {new()};
            
            keyList.Last().SignKey(ownerPrivateKey);

            CheckedChanged -= CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateParent;
        }

        public bool VerifyKeys()
        {
            foreach (var key in keyList)
            {
                if (ownerPublicKey == null || 
                    (key.PrivateKey != null && !Crypto.VerifyData(ownerPublicKey, key.PrivateKey, key.SignedPrivateKey)) ||
                    !Crypto.VerifyData(ownerPublicKey, key.PublicKey, key.SignedPublicKey))
                {
                    Console.WriteLine("DataFileEntry filename='{0}', key pair with revstart='{1}', revend='{2}'", 
                        filename, key.RevisionStart, key.RevisionEnd);
                    return false;
                }
            }

            return true;
        }

        // 
        // public void SignKeys()
        // {
        //     Crypto crypto = new();
        //
        //
        //     foreach (var key in keyList)
        //     {
        //         key.signedPrivateKey = crypto.SignData(ownerPrivateKey, key.privateKey);
        //         key.signedPublicKey = crypto.SignData(ownerPrivateKey, key.publicKey);
        //     }
        // }

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
            
            // Rider incorrectly thinks handler can never be null
            // ReSharper disable once ConstantConditionalAccessQualifier
            handler?.Invoke(this, e);
        }

        public event EventHandler CheckedChanged = null!;

        public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            parent?.UpdateIsCheckedBasedOnChildren();
        }

        public bool IsEqual(DataFileEntry reference)
        {
            return CompareAllPropertiesExcept(reference, null);
        }

        public bool HasSameStaticProperties(DataFileEntry reference)
        {
            // // Construct ignore list and populate with non-static properties
            // List<PropertyInfo> ignoreList = new();
            //
            // var filenameProperty = typeof(DataFileEntry).GetProperty(nameof(filename));
            // var ownerPrivKeyProperty = typeof(DataFileEntry).GetProperty(nameof(ownerPrivateKey));
            //
            // if (filenameProperty != null)
            // {
            //     ignoreList.Add(filenameProperty);
            // }
            // if (ownerPrivKeyProperty != null)
            // {
            //     ignoreList.Add((ownerPrivKeyProperty));
            // }
            //
            // return CompareProperties(reference, ignoreList);
            
            List<PropertyInfo?> staticPropertyList = new();
            List<PropertyInfo> compareList = new();
            
            staticPropertyList.Add(typeof(DataFileEntry).GetProperty(nameof(pageName)));
            staticPropertyList.Add(typeof(DataFileEntry).GetProperty(nameof(serverLink)));
            staticPropertyList.Add(typeof(DataFileEntry).GetProperty(nameof(ownerPublicKey)));

            
            foreach (var item in staticPropertyList)
            {
                if (item != null)
                {
                    compareList.Add(item);
                }
            }

            return CompareProperties(reference, compareList);
        }

        public bool CompareAllPropertiesExcept(DataFileEntry reference, List<PropertyInfo>? ignoreList)
        {
            PropertyInfo[] properties = typeof(DataFileEntry).GetProperties();

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

        private bool CompareProperties(DataFileEntry reference, List<PropertyInfo> propertiesToBeCompared)
        {
            foreach (PropertyInfo prop in propertiesToBeCompared)
            {
                var ownValue = typeof(DataFileEntry).GetProperty(prop.Name)?.GetValue(this, null);
                var refValue = typeof(DataFileEntry).GetProperty(prop.Name)?.GetValue(reference, null);

                // Console.WriteLine("Testing property: '{0}'='{1}'", prop, ownValue);
                
                if (ownValue == null || refValue == null)
                {
                    // Console.WriteLine("AtLeast one is null");
                    if (ownValue != null || refValue != null)
                    {
                        return false;
                    }
                }
                else if (ownValue is string)
                {
                    // Console.WriteLine("is a string");

                    if (!(ownValue.Equals(refValue)))
                    {
                        // Console.WriteLine("string: '{0}'!='{1}'", ownValue, refValue);
                        return false;
                    }
                }
                else if (ownValue.GetType() == typeof(byte[]))
                {
                    // Console.WriteLine("is a byte[]");

                    var byteArrayOwn = ownValue as byte[];
                    var byteArrayRef = refValue as byte[];
                    if (!(byteArrayOwn!).SequenceEqual(byteArrayRef!))
                    {
                        // Console.WriteLine("ByteArray: '{0}'!='{1}'", byteArrayOwn, byteArrayRef);
                        return false;
                    }
                }
                else
                {
                    // Console.WriteLine("is neither");

                    // Console.WriteLine("'{0}'=='{1}'", ownValue, refValue);
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

        public void MergeWithOtherDataFileEntry(DataFileEntry df)
        {
            if (!filename.Equals(df.filename) ||
                !serverLink.Equals(df.serverLink) ||
                !pageName.Equals(df.pageName) ||
                (ownerPrivateKey != null && df.ownerPrivateKey != null && !ownerPrivateKey.SequenceEqual(df.ownerPrivateKey)) ||
                (ownerPublicKey != null && df.ownerPublicKey != null && !ownerPublicKey.SequenceEqual(df.ownerPublicKey))
                )
            {
                return;
            }

            ownerPrivateKey ??= df.ownerPrivateKey;
            ownerPublicKey ??= df.ownerPublicKey;

            List<DataFileKey> combinedKeyList = new();
            List<DataFileKey> resultingKeyList = new();
            
            combinedKeyList.AddRange(keyList);
            combinedKeyList.AddRange(df.keyList);
            
            combinedKeyList = combinedKeyList.OrderBy(entry => entry.PublicKey, new ByteArrayComparer()).ToList();

            int i = 0;
            while (i < combinedKeyList.Count)
            {
                int cnt = 1;

                while (i + cnt < combinedKeyList.Count && combinedKeyList[i].PublicKey.SequenceEqual(combinedKeyList[i + cnt].PublicKey))
                {
                    combinedKeyList[i].MergeWithOtherKey(combinedKeyList[i + cnt]);
                    cnt++;
                }
                
                resultingKeyList.Add(combinedKeyList[i]);
                i += cnt;
            }
            
            resultingKeyList = resultingKeyList.OrderBy(entry => entry.RevisionStart).ToList();
            keyList.Clear();
            keyList.AddRange(resultingKeyList);
        }

        public void PrintInfo()
        {
            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}'", 
                filename, isChecked, parent?.name ?? "null");
        }
    }
}