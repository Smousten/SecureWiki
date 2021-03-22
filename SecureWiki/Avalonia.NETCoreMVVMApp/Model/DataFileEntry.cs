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
        
        // Tuple of (Start revision id, public key, private key, end revision end
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
                // Console.WriteLine("DataFile '{0}' set to '{1}'", filename, value);
                OnPropertyChanged(nameof(isChecked));
                OnPropertyChanged(nameof(isCheckedWriteEnabled));
                OnCheckedChanged(EventArgs.Empty);
                // Console.WriteLine("DataFile '{0}' finished setting");
            }
        }
        
        private bool? _isCheckedWrite = false;
        public bool? isCheckedWrite
        {
            get
            {
                // if (privateKey == null)
                if (keyList.TrueForAll(e => e.privateKey == null))
                {
                    return false;
                }
                return (_isCheckedWrite ?? false);
            }
            set
            {
                _isCheckedWrite = value;
                // Console.WriteLine("Datafile '{0}': IsCheckedWrite set to '{1}'", filename, value);
                OnPropertyChanged(nameof(isCheckedWrite));
                // Console.WriteLine("Datafile '{0}': IsCheckedWrite finished setting", filename);
            }
        }
        
        public bool isCheckedWriteEnabled
        {
            get
            {
                // if (privateKey == null)
                if (keyList.TrueForAll(e => e.privateKey == null))
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
                // Console.WriteLine("_newestRevisionSelected set to '{0}'", value);
                _newestRevisionSelected = value;
                OnPropertyChanged(nameof(newestRevisionSelected));
            }
        }
        
        public DataFileEntry(string serverLink, string filename = "unnamed")
        {
            this.filename = filename;
            this.serverLink = serverLink;
            pageName = RandomString.GenerateRandomAlphanumericString();
            keyList = new List<DataFileKey> {new()};
            
            // IsChecked = false;
            CheckedChanged -= CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateParent;
            // Console.WriteLine("DataFile '{0}' initialised", filename);
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
            // Console.WriteLine("OnPropertyChanged in DatFileEntry, property: " + propertyName);
        }
        
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            EventHandler handler = CheckedChanged;
            handler(this, e);
        }

        public event EventHandler CheckedChanged = null!;

        public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            // Console.WriteLine("CheckedChangedUpdateParent entered in datafile.filename='{0}'", filename);
            parent?.UpdateIsCheckedBasedOnChildren();
        }

        public bool IsEqual(DataFileEntry reference)
        {
            return CompareProperties(reference, null);
        }

        public bool HasSameStaticProperties(DataFileEntry reference)
        {
            // Construct ignore list and populate with non-static properties
            List<PropertyInfo> ignoreList = new();

            var filenameProperty = typeof(DataFileEntry).GetProperty(nameof(filename));

            if (filenameProperty != null)
            {
                ignoreList.Add(filenameProperty);
            }
            // ignoreList.Add(typeof(DataFileEntry).GetProperty(nameof(revisionNr)));
            
            return CompareProperties(reference, ignoreList);
        }

        private bool CompareProperties(DataFileEntry reference, List<PropertyInfo>? ignoreList)
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

        public void PrintInfo()
        {
            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}'", 
                filename, isChecked, parent?.name ?? "null");
        }
    }
}