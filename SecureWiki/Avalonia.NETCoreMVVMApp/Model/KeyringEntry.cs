using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class KeyringEntry : IReactiveObject
    {
        private string name;
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
        
        [JsonProperty]
        public string Name
        {
            get { return name;}
            set
            {
                name = value; 
                RaisePropertyChanged(nameof(Name));
            }
        }

        [JsonProperty]
        public ObservableCollection<KeyringEntry> keyrings { get; set; } = new();
        [JsonProperty]
        public ObservableCollection<DataFileEntry> dataFiles { get; set; } = new();

        public ObservableCollection<object> combinedList
        {
            get
            {
                var output = new ObservableCollection<object>();

                foreach (KeyringEntry entry in keyrings)
                {
                    output.Add(entry);
                }
                foreach (DataFileEntry entry in dataFiles)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }

        public void AddKeyring(KeyringEntry keyringEntry)
        {
            keyrings.Add(keyringEntry);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void AddDataFile(DataFileEntry dataFile)
        {
            dataFiles.Add(dataFile);
            RaisePropertyChanged(nameof(dataFiles));
            RaisePropertyChanged(nameof(combinedList));
        }

        public event PropertyChangingEventHandler? PropertyChanging;
        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
            throw new System.NotImplementedException();
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

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Console.WriteLine("OnPropertyChanged in Keyring, property: " + propertyName);
        }

        public void CopyFromOtherKeyring(KeyringEntry ke)
        {
            keyrings.Clear();
            dataFiles.Clear();
            
            Name = ke.Name;
            
            foreach (KeyringEntry item in ke.keyrings)
            {
                AddKeyring(item);
            }
            
            foreach (DataFileEntry item in ke.dataFiles)
            {
                AddDataFile(item);
            }
        }

        public void PrintInfoRecursively()
        {
            Console.WriteLine("KeyRing: Name='{0}', Checked='{1}'", Name, Checked);
            foreach (DataFileEntry item in dataFiles)
            {
                item.PrintInfo();
            }

            foreach (KeyringEntry item in keyrings)
            {
                item.PrintInfoRecursively();
            }
        }
    }
}