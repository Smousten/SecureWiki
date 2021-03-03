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
                OnPropertyChanged(nameof(IsChecked));
                OnCheckedChanged(EventArgs.Empty);
            }
        }
        
        private string name;
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

        public KeyringEntry()
        {
            // IsChecked = false;
            
            CheckedChanged += CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateChildren;
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
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Console.WriteLine("OnPropertyChanged in Keyring, property: " + propertyName);
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

        protected void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            Console.WriteLine("CheckedChangedUpdateParent in keyring.Name='{0}' : entered", Name);
            Parent?.UpdateIsCheckedBasedOnChildren();
        }
        
        protected void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        {
            Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : entered", Name);
            foreach (KeyringEntry child in keyrings)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Updating child='{1}'", Name, child.Name);
                child.IsChecked = IsChecked;
                Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Restoring child='{1}'", Name, child.Name);
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                // child.CheckedChanged += CheckedChangedUpdateParent;
                Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Updating child='{1}'", Name, child.filename);
                child.IsChecked = IsChecked;
                Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Restoring child='{1}'", Name, child.filename);
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }

        public void UpdateIsCheckedBasedOnChildren()
        {
            // Prevent feedback loop
            CheckedChanged -= CheckedChangedUpdateChildren;
            
            bool anyChecked = false;
            bool atleastTwoChecked = false;
            bool anyUnchecked = false;
            bool ancestorChecked = false;
            
            foreach (KeyringEntry child in keyrings)
            {
                if (child.IsChecked == true)
                {
                    if (anyChecked)
                    {
                        atleastTwoChecked = true;
                    }
                    anyChecked = true;
                }
                else
                {
                    anyUnchecked = true;
                    
                }
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                if (child.IsChecked == true)
                {
                    if (anyChecked)
                    {
                        atleastTwoChecked = true;
                    }
                    anyChecked = true;
                }
                else
                {
                    anyUnchecked = true;
                    
                }
            }

            var localParent = Parent;
            List<KeyringEntry> ancestorList = new();

            while (localParent != null)
            {
                if (localParent.IsChecked == true)
                {
                    ancestorChecked = true;

                    foreach (KeyringEntry item in ancestorList)
                    {
                        item.CheckedChanged -= item.CheckedChangedUpdateChildren;
                        item.CheckedChanged -= item.CheckedChangedUpdateParent;
                        item.IsChecked = true;
                        item.CheckedChanged += item.CheckedChangedUpdateChildren;
                        item.CheckedChanged += item.CheckedChangedUpdateParent;
                    }

                    break;
                }

                ancestorList.Add(localParent);
                localParent = localParent.Parent;
            }
            
            // Change here to interact with IsThreeState properly
            // if (anyChecked && anyUnchecked)
            // {
            //     // parentcb.IsChecked = false;
            // }
            if (anyUnchecked == false || atleastTwoChecked || (ancestorChecked && anyChecked))
            {
                IsChecked = true;
            }
            else if (anyChecked == false)
            {
                IsChecked = false;
            }
            
            // Restore event handler
            CheckedChanged += CheckedChangedUpdateChildren;
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
            Console.WriteLine("KeyRing: Name='{0}', Checked='{1}', Parent.Name='{2}'", 
                Name, IsChecked, Parent?.Name ?? "null");
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