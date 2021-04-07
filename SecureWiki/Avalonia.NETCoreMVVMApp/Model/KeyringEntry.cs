using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DynamicData;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class KeyringEntry : IReactiveObject
    {
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
            get => (_isCheckedWrite ?? false);
            set
            {
                _isCheckedWrite = value;
                OnPropertyChanged(nameof(isCheckedWrite));
                OnCheckedWriteChanged(EventArgs.Empty);
            }
        }
      
        public bool isCheckedWriteEnabled => isChecked ?? false;

        private string _name;
        [JsonProperty]
        public string name
        {
            get => _name;
            set
            {
                _name = value; 
                RaisePropertyChanged(nameof(name));
            }
        }

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

        public KeyringEntry(string name = "unnamed")
        {
            _name = name;
            
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

        public void AddRangeKeyring(List<KeyringEntry> keyringEntries)
        {
            keyrings.AddRange(keyringEntries);
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
        
        public void AddRangeDataFile(List<DataFileEntry> dataFileEntries)
        {
            dataFiles.AddRange(dataFileEntries);
            RaisePropertyChanged(nameof(dataFileEntries));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void RemoveDataFile(DataFileEntry dataFile)
        {
            dataFiles.Remove(dataFile);
            RaisePropertyChanged(nameof(dataFiles));
            RaisePropertyChanged(nameof(combinedList));
        }

        public void ClearKeyrings()
        {
            keyrings.Clear();
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }

        public void ClearDataFiles() {
            dataFiles.Clear();
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
            PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            EventHandler handler = CheckedChanged;
            handler(this, e);
        }
        
        protected virtual void OnCheckedWriteChanged(EventArgs e)
        {
            EventHandler handler = CheckedWriteChanged;
            handler(this, e);
        }

        public event EventHandler CheckedChanged = null!;
        public event EventHandler CheckedWriteChanged = null!;

        // Update parent isChecked based own and any other siblings' values
        protected void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            parent?.UpdateIsCheckedBasedOnChildren();
        }
        
        // Update children isChecked based own value
        protected void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        {
            // Disable events updating ancestors while setting values  
            foreach (KeyringEntry child in keyrings)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }
        
        // Update children isCheckedWrite based own value
        protected void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        {
            foreach (KeyringEntry child in keyrings)
            {
                child.isCheckedWrite = isCheckedWrite;
            }
            
            foreach (DataFileEntry child in dataFiles)
            {
                child.isCheckedWrite = isCheckedWrite;
            }
        }

        public virtual void UpdateIsCheckedBasedOnChildren()
        {
            // Prevent feedback loop
            this.CheckedChanged -= this.CheckedChangedUpdateChildren;
            
            bool anyChecked = false;
            bool atLeastTwoChecked = false;
            bool anyUnchecked = false;
            bool ancestorChecked = false;
            
            foreach (KeyringEntry child in keyrings)
            {
                if (child.isChecked == true)
                {
                    if (anyChecked)
                    {
                        atLeastTwoChecked = true;
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
                if (child.isChecked == true)
                {
                    if (anyChecked)
                    {
                        atLeastTwoChecked = true;
                    }
                    anyChecked = true;
                }
                else
                {
                    anyUnchecked = true;
                }
            }

            var localParent = parent;
            List<KeyringEntry> ancestorList = new();

            // Find chain of unchecked ancestors and set isChecked to true
            while (localParent != null)
            {
                if (localParent.isChecked == true)
                {
                    ancestorChecked = true;

                    // Disable events updating ancestors or children while setting values  
                    foreach (KeyringEntry item in ancestorList)
                    {
                        item.CheckedChanged -= item.CheckedChangedUpdateChildren;
                        item.CheckedChanged -= item.CheckedChangedUpdateParent;
                        item.isChecked = true;
                        item.CheckedChanged += item.CheckedChangedUpdateChildren;
                        item.CheckedChanged += item.CheckedChangedUpdateParent;
                    }

                    break;
                }

                ancestorList.Add(localParent);
                localParent = localParent.parent;
            }

            // Console.WriteLine("AnyUnchecked='{0}'", anyUnchecked);
            // Console.WriteLine("AnyChecked='{0}'", anyChecked);
            // Console.WriteLine("atLeastTwoChecked='{0}'", atLeastTwoChecked);
            // Console.WriteLine("ancestorChecked='{0}'", ancestorChecked);
            //
            // Change here to interact with IsThreeState properly
            // if (anyChecked && anyUnchecked)
            // {
            //     IsChecked = false;
            // }
            if (anyUnchecked == false || atLeastTwoChecked || (ancestorChecked && anyChecked))
            {
                isChecked = true;
            }
            else if (anyChecked == false)
            {
                isChecked = false;
            }
            
            // Restore event handler
            this.CheckedChanged += this.CheckedChangedUpdateChildren;
        }


        public void CopyFromOtherKeyring(KeyringEntry ke)
        {
            keyrings.Clear();
            dataFiles.Clear();
            
            name = ke.name;
            
            MergeAllEntriesFromOtherKeyring(ke);
        }
        
        public void MergeAllEntriesFromOtherKeyring(KeyringEntry ke)
        {
            // Add all KeyringEntries to own and merge recursively if name conflicts are found
            foreach (KeyringEntry item in ke.keyrings)
            {
                bool nameAlreadyInUse = false;
                foreach (KeyringEntry ownKe in keyrings)
                {
                    if (item.name.Equals(ownKe.name))
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
            
            foreach (DataFileEntry otherDF in ke.dataFiles)
            {
                // Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Checking", 
                //     item.filename, isChecked, parent?.name ?? "null");

                bool nameAlreadyInUse = false;
                bool fileAlreadyExists = false;
                foreach (DataFileEntry ownDF in dataFiles)
                {
                    
                    if (otherDF.filename.Equals(ownDF.filename))
                    {
                        // Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Filename match", 
                        //     item.filename, isChecked, parent?.name ?? "null");
                        if (ownDF.IsEqual(otherDF))
                        {
                            // Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Exact copy already exists", 
                            //     item.filename, isChecked, parent?.name ?? "null");
                            fileAlreadyExists = true;
                        }
                        // If they point to the exact same page
                        else if (ownDF.HasSameStaticProperties(otherDF))
                        {
                            ownDF.MergeWithOtherDataFileEntry(otherDF);
                            fileAlreadyExists = true;
                        }
                        else
                        {
                            // Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Name is used by existing file", 
                            //     item.filename, isChecked, parent?.name ?? "null");
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
                    
                    // Check new names until either an identical copy or no match is found 
                    while (newNameInUse)
                    {
                        string newName = otherDF.filename + "(" + cnt + ")";
                        
                        // Find any DataFileEntry with the same filename
                        newNameInUse = dataFiles.Any(x => x.filename.Equals(newName));
                        if (newNameInUse)
                        {
                            DataFileEntry df = dataFiles.First(x => x.filename.Equals(newName));

                            // If they point to the exact same page
                            if (df.HasSameStaticProperties(otherDF))
                            {
                                df.MergeWithOtherDataFileEntry(otherDF);
                                fileAlreadyExists = true;
                                break;
                            }

                            cnt++;
                        }
                        else
                        {
                            otherDF.filename = newName;
                            break;
                        }
                    }
                }

                if (fileAlreadyExists == false)
                {
                    // Console.WriteLine("DataFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Adding file", 
                    //     item.filename, isChecked, parent?.name ?? "null");
                    AddDataFile(otherDF);
                }
            }
        }

        // Recursively add checked children to another keyring 
        public void AddToOtherKeyringRecursivelyBasedOnIsChecked(KeyringEntry outputKeyring)
        {
            foreach (KeyringEntry ke in keyrings)
            {
                KeyringEntry keCopy = new(ke.name);
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
            }
            
            foreach (DataFileEntry dataFileEntry in dataFiles)
            {
                if (dataFileEntry.isChecked == true)
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

            // Remove private keys from DataFileEntries if they do not have write access checked
            foreach (DataFileEntry dataFileEntry in dataFiles)
            {
                dataFileEntry.PrepareForExport();
                
                if (dataFileEntry.isCheckedWrite != true)
                {
                    dataFileEntry.keyList.ForEach(e => e.PrivateKey = null);
                    // dataFileEntry.privateKey = null;
                }
            }
        }

        public bool HasDataFileEntryDescendant()
        {

            if (dataFiles.Count > 0)
            {
                return true;
            }

            return keyrings.Any(ke => ke.HasDataFileEntryDescendant());
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

        public void SortAllRecursively()
        {
            SortKeyrings();
            SortDataFiles();

            foreach (var item in keyrings)
            {
                item.SortAllRecursively();
            }
        }

        public void SortKeyrings()
        {
            var sortedList = keyrings.OrderBy(entry => entry.name).ToList();
            ClearKeyrings();
            AddRangeKeyring(sortedList);
        }
        
        public void SortDataFiles()
        {
            var sortedList = dataFiles.OrderBy(entry => entry.filename).ToList();
            ClearDataFiles();
            AddRangeDataFile(sortedList);
        }

        public List<DataFileEntry> GetAllDescendantDataFileEntries()
        {
            var outputList = new List<DataFileEntry>();

            outputList.AddRange(dataFiles);

            foreach (var keyring in keyrings)
            {
                outputList.AddRange(keyring.GetAllDescendantDataFileEntries());
            }

            return outputList;
        }
        

        public void PrintInfoRecursively()
        {
            Console.WriteLine("KeyRing: Name='{0}', Checked='{1}', Parent.Name='{2}'", 
                name, isChecked, parent?.name ?? "null");
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