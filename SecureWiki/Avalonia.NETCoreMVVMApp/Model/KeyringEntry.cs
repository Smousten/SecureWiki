using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Avalonia.Remote.Protocol.Input;
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
                // Console.WriteLine("Keyring '{0}' set to '{1}'", Name, value);
                OnPropertyChanged(nameof(IsChecked));
                OnPropertyChanged(nameof(IsCheckedWriteEnabled));
                OnCheckedChanged(EventArgs.Empty);
                // Console.WriteLine("Keyring '{0}' finished setting", Name);
            }
        }
        
        private bool? _isCheckedWrite = false;
        public bool? IsCheckedWrite
        {
            get
            {
                return (_isCheckedWrite ?? false);
            }
            set
            {
                _isCheckedWrite = value;
                Console.WriteLine("Keyring '{0}': IsCheckedWrite set to '{1}'", Name, value);
                OnPropertyChanged(nameof(IsCheckedWrite));
                OnCheckedWriteChanged(EventArgs.Empty);
                Console.WriteLine("Keyring '{0}': IsCheckedWrite finished setting", Name);
            }
        }
      
        public bool IsCheckedWriteEnabled
        {
            get
            {
                return IsChecked ?? false;
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
            CheckedWriteChanged += CheckedWriteChangedUpdateChildren;
        }
        
        public void AddKeyring(KeyringEntry keyringEntry)
        {
            keyrings.Add(keyringEntry);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void RemoveKeyring(KeyringEntry keyringEntry)
        {
            keyrings.Remove(keyringEntry);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void AddDataFile(DataFileEntry dataFile)
        {
            dataFiles.Add(dataFile);
            RaisePropertyChanged(nameof(dataFiles));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void RemoveDataFile(DataFileEntry dataFile)
        {
            dataFiles.Remove(dataFile);
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
        
        protected virtual void OnCheckedWriteChanged(EventArgs e)
        {
            EventHandler handler = CheckedWriteChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler CheckedChanged;
        public event EventHandler CheckedWriteChanged;

        protected void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            // Console.WriteLine("CheckedChangedUpdateParent in keyring.Name='{0}' : entered", Name);
            Parent?.UpdateIsCheckedBasedOnChildren();
        }
        
        protected void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        {
            // Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : entered", Name);
            foreach (KeyringEntry child in keyrings)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                // Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Updating child='{1}'", Name, child.Name);
                child.IsChecked = IsChecked;
                // Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Restoring child='{1}'", Name, child.Name);
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                // child.CheckedChanged += CheckedChangedUpdateParent;
                // Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Updating child='{1}'", Name, child.filename);
                child.IsChecked = IsChecked;
                // Console.WriteLine("CheckedChangedUpdateChildren in keyring.Name='{0}' : Restoring child='{1}'", Name, child.filename);
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }
        
        protected void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        {
            foreach (KeyringEntry child in keyrings)
            {
                child.IsCheckedWrite = IsCheckedWrite;
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                child.IsCheckedWrite = IsCheckedWrite;
            }
        }

        public virtual void UpdateIsCheckedBasedOnChildren()
        {
            Console.WriteLine("UpdateIsCheckedBasedOnChildren in keyring.Name='{0}'", Name);
            // Prevent feedback loop
            this.CheckedChanged -= this.CheckedChangedUpdateChildren;
            
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

            // Console.WriteLine("AnyUnchecked='{0}'", anyUnchecked);
            // Console.WriteLine("AnyChecked='{0}'", anyChecked);
            // Console.WriteLine("atleastTwoChecked='{0}'", atleastTwoChecked);
            // Console.WriteLine("ancestorChecked='{0}'", ancestorChecked);
            //
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
            this.CheckedChanged += this.CheckedChangedUpdateChildren;
        }


        public void CopyFromOtherKeyring(KeyringEntry ke)
        {
            keyrings.Clear();
            dataFiles.Clear();
            
            Name = ke.Name;
            
            MergeAllEntriesFromOtherKeyring(ke);
        }
        
        public void MergeAllEntriesFromOtherKeyring(KeyringEntry ke)
        {
            foreach (KeyringEntry item in ke.keyrings)
            {
                bool nameAlreadyInUse = false;
                foreach (KeyringEntry ownKe in keyrings)
                {
                    if (item.Name.Equals(ownKe.Name))
                    {
                        nameAlreadyInUse = true;
                        
                        ownKe.MergeAllEntriesFromOtherKeyring(item);
                        
                        break;
                    }
                }

                if (nameAlreadyInUse == false)
                {
                    AddKeyring(item);                    
                }
            }
            
            foreach (DataFileEntry item in ke.dataFiles)
            {
                bool nameAlreadyInUse = false;
                bool fileAlreadyExists = false;
                foreach (DataFileEntry ownDF in dataFiles)
                {
                    
                    if (item.filename.Equals(ownDF.filename))
                    {
                        if (item.IsEqual(ownDF))
                        {
                            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Exact copy already exists", 
                                item.filename, IsChecked, Parent?.Name ?? "null");
                            fileAlreadyExists = true;
                        }
                        else
                        {
                            Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Name is used by existing file", 
                                item.filename, IsChecked, Parent?.Name ?? "null");
                            nameAlreadyInUse = true;
                        }
                        
                        break;
                    }
                }
                
                // Rename new datafile if name is already in use
                if (nameAlreadyInUse)
                {
                    int cnt = 1;
                    bool newNameInUse = true;
                    while (newNameInUse)
                    {
                        string newName = item.filename + "(" + cnt + ")";
                        
                        newNameInUse = dataFiles.Any(x => x.filename.Equals(newName));
                        if (newNameInUse)
                        {
                            DataFileEntry df = dataFiles.First(x => x.filename.Equals(newName));

                            if (df.HasSameStaticProperties(item))
                            {
                                fileAlreadyExists = true;
                                break;
                            }

                            cnt++;
                        }
                        else
                        {
                            item.filename = newName;
                            break;
                        }
                    }
                }

                if (fileAlreadyExists == false)
                {
                    Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Adding file", 
                        item.filename, IsChecked, Parent?.Name ?? "null");
                    AddDataFile(item);
                }
            }
        }

        public void AddToOtherKeyringRecursivelyBasedOnIsChecked(KeyringEntry outputKeyring)
        {
            foreach (KeyringEntry ke in keyrings)
            {
                KeyringEntry keCopy = new();
                keCopy.Name = ke.Name;
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
            }
            
            foreach (DataFileEntry dataFileEntry in dataFiles)
            {
                if (dataFileEntry.IsChecked == true)
                {
                    outputKeyring.AddDataFile(dataFileEntry);                    
                }
            }
        }

        public void PrepareForExportRecursively()
        {
            foreach (KeyringEntry ke in keyrings)
            {
                ke.PrepareForExportRecursively();
            }
            
            foreach (DataFileEntry dataFileEntry in dataFiles)
            {
                if (dataFileEntry.IsCheckedWrite != true)
                {
                    dataFileEntry.privateKey = null;
                }
            }
        }

        public bool HasDataFileEntryDescendant()
        {

            if (dataFiles.Count > 0)
            {
                return true;
            }
            
            foreach (KeyringEntry ke in keyrings)
            {
                if (ke.HasDataFileEntryDescendant() == true)
                {
                    return true;
                }
            }

            return false;
        }

        public void RemoveEmptyDescendantsRecursively()
        {
            List<KeyringEntry> removeList = new();
            foreach (KeyringEntry ke in keyrings)
            {
                if (ke.HasDataFileEntryDescendant() == false)
                {
                    removeList.Add(ke);
                }
                else
                {
                    ke.RemoveEmptyDescendantsRecursively();
                }
            }
            foreach (KeyringEntry ke in removeList)
            {
                RemoveKeyring(ke);
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