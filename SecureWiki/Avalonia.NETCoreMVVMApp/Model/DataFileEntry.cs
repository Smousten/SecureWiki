using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DataFileEntry : IReactiveObject
    {
        [JsonProperty]
        public string filename { get; set; }
        [JsonProperty]
        public byte[] symmKey { get; set; }
        [JsonProperty]
        public byte[] iv { get; set; }
        [JsonProperty]
        public byte[] privateKey { get; set; }
        [JsonProperty]
        public byte[] publicKey { get; set; }
        [JsonProperty]
        public string revisionNr { get; set; }
        [JsonProperty]
        public string serverLink { get; set; }
        [JsonProperty]
        public string pagename { get; set; }

        private KeyringEntry? _parent;
        public KeyringEntry? Parent
        {
            get => _parent;
            set
            {
                _parent = value; 
                RaisePropertyChanged(nameof(Parent));
            }
        }
        private bool? _isChecked = false;
        public bool? IsChecked
        {
            get
            {
                return (_isChecked ?? false);
            }
            set
            {
                _isChecked = value;
                // Console.WriteLine("DataFile '{0}' set to '{1}'", filename, value);
                OnPropertyChanged(nameof(IsChecked));
                OnCheckedChanged(EventArgs.Empty);
                // Console.WriteLine("DataFile '{0}' finished setting");
            }
        }
        
        public DataFileEntry()
        {
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
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Console.WriteLine("OnPropertyChanged in DatFileEntry, property: " + propertyName);
        }
        
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            EventHandler handler = CheckedChanged;
            if (handler != null)
            {
                handler(this, e);                
            }
        }

        public event EventHandler CheckedChanged;

        public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            Console.WriteLine("CheckedChangedUpdateParent entered in datafile.filename='{0}'", filename);
            Parent.UpdateIsCheckedBasedOnChildren();
        }

        public bool IsEqual(DataFileEntry reference)
        {
            PropertyInfo[] properties = typeof(DataFileEntry).GetProperties();
            foreach (PropertyInfo prop in properties)
            {
                var ownValue = typeof(DataFileEntry).GetProperty(prop.Name).GetValue(this, null);
                var refValue = typeof(DataFileEntry).GetProperty(prop.Name).GetValue(reference, null);

                // Console.WriteLine("Testing property: '{0}'='{1}'", prop, ownValue);
                
                if (ownValue.GetType() == typeof(string))
                {
                    if (!(ownValue.Equals(refValue)))
                    {
                        // Console.WriteLine("string: '{0}'!='{1}'", ownValue, refValue);
                        return false;
                    }
                }
                else if (ownValue.GetType() == typeof(byte[]))
                {
                    var byteArrayOwn = ownValue as byte[];
                    var byteArrayRef = refValue as byte[];
                    if (!((byteArrayOwn ?? throw new InvalidOperationException()).SequenceEqual(byteArrayRef ?? throw new InvalidOperationException())))
                    {
                        // Console.WriteLine("ByteArray: '{0}'!='{1}'", byteArrayOwn, byteArrayRef);
                        return false;
                    }
                }
                else
                {
                    // Console.WriteLine("'{0}'=='{1}'", ownValue, refValue);
                }
            }

            return true;
        }

        public void PrintInfo()
        {
            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}'", 
                filename, IsChecked, Parent?.Name ?? "null");
        }
    }
}