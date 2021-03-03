using System;
using System.ComponentModel;
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

        private bool? _checked = false;
        public bool? Checked
        {
            get
            {
                return (_checked ?? false);
            }
            set
            {
                _checked = value;
                OnPropertyChanged(nameof(Checked));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangingEventHandler? PropertyChanging;
        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
            throw new System.NotImplementedException();
        }

        public void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            throw new System.NotImplementedException();
        }
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Console.WriteLine("OnPropertyChanged in DatFileEntry, property: " + propertyName);
        }

        public void PrintInfo()
        {
            Console.WriteLine("DataFile: filename='{0}', Checked='{1}'", filename, Checked);
        } 
    }
}