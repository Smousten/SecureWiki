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
    public class Keyring : IReactiveObject
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

        [JsonProperty] public string pageTitle;
        [JsonProperty] public List<SymmetricReference> SymmetricReferences = new();
        [JsonProperty] public InboxReference InboxReferenceToSelf;
        public AccessFileReference accessFileReferenceToSelf;

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
        
        [JsonProperty]
        public ObservableCollection<Keyring> keyrings { get; set; } = new();
        [JsonProperty]
        public ObservableCollection<AccessFile> accessFiles { get; set; } = new();

        public ObservableCollection<object> combinedList
        {
            get
            {
                var output = new ObservableCollection<object>();

                foreach (Keyring entry in keyrings)
                {
                    output.Add(entry);
                }
                foreach (AccessFile entry in accessFiles)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }

        public Keyring(AccessFileReference accessFileReferenceToSelf, string name = "unnamed")
        {
            _name = name;
            this.accessFileReferenceToSelf = accessFileReferenceToSelf;
            
            
            CheckedChanged += CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateChildren;
            CheckedWriteChanged += CheckedWriteChangedUpdateChildren;
        }
        
        public Keyring(string name = "unnamed")
        {
            _name = name;
            
            CheckedChanged += CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateChildren;
            CheckedWriteChanged += CheckedWriteChangedUpdateChildren;
        }

        // Add a symmetric reference and update it accordingly
        public void AddSymmetricReference(SymmetricReference symmetricReference)
        {
            SymmetricReferences.Add(symmetricReference);
            symmetricReference.keyringParent = this;
        }
        
        public void AddKeyring(Keyring keyring)
        {
            keyrings.Add(keyring);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }

        public void AddRangeKeyring(List<Keyring> keyringEntries)
        {
            keyrings.AddRange(keyringEntries);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void RemoveKeyring(Keyring keyring)
        {
            keyrings.Remove(keyring);
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void AddAccessFile(AccessFile accessFile)
        {
            accessFiles.Add(accessFile);
            RaisePropertyChanged(nameof(accessFiles));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void AddRangeAccessFile(List<AccessFile> accessFileEntries)
        {
            accessFiles.AddRange(accessFileEntries);
            RaisePropertyChanged(nameof(accessFileEntries));
            RaisePropertyChanged(nameof(combinedList));
        }
        
        public void RemoveAccessFile(AccessFile accessFile)
        {
            accessFiles.Remove(accessFile);
            RaisePropertyChanged(nameof(accessFiles));
            RaisePropertyChanged(nameof(combinedList));
        }

        public void ClearKeyrings()
        {
            keyrings.Clear();
            RaisePropertyChanged(nameof(keyrings));
            RaisePropertyChanged(nameof(combinedList));
        }

        public void ClearAccessFiles() {
            accessFiles.Clear();
            RaisePropertyChanged(nameof(accessFiles));
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
            foreach (Keyring child in keyrings)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (AccessFile child in accessFiles)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }
        
        // Update children isCheckedWrite based own value
        protected void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        {
            foreach (Keyring child in keyrings)
            {
                child.isCheckedWrite = isCheckedWrite;
            }
            
            foreach (AccessFile child in accessFiles)
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
            
            foreach (Keyring child in keyrings)
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
            
            foreach (AccessFile child in accessFiles)
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
            List<Keyring> ancestorList = new();

            // Find chain of unchecked ancestors and set isChecked to true
            while (localParent != null)
            {
                if (localParent.isChecked == true)
                {
                    ancestorChecked = true;

                    // Disable events updating ancestors or children while setting values  
                    foreach (Keyring item in ancestorList)
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


        public void CopyFromOtherKeyring(Keyring ke)
        {
            keyrings.Clear();
            accessFiles.Clear();
            
            name = ke.name;
            
            MergeAllEntriesFromOtherKeyring(ke);
        }
        
        public void MergeAllEntriesFromOtherKeyring(Keyring ke)
        {
            // Add all KeyringEntries to own and merge recursively if name conflicts are found
            foreach (Keyring item in ke.keyrings)
            {
                bool nameAlreadyInUse = false;
                foreach (Keyring ownKe in keyrings)
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
            
            foreach (AccessFile otherAF in ke.accessFiles)
            {
                // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Checking", 
                //     item.filename, isChecked, parent?.name ?? "null");

                bool nameAlreadyInUse = false;
                bool fileAlreadyExists = false;
                foreach (AccessFile ownAF in accessFiles)
                {
                    
                    if (otherAF.filename.Equals(ownAF.filename))
                    {
                        // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Filename match", 
                        //     item.filename, isChecked, parent?.name ?? "null");
                        if (ownAF.IsEqual(otherAF))
                        {
                            // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Exact copy already exists", 
                            //     item.filename, isChecked, parent?.name ?? "null");
                            fileAlreadyExists = true;
                        }
                        // If they point to the exact same page
                        else if (ownAF.HasSameStaticProperties(otherAF))
                        {
                            ownAF.MergeWithOtherAccessFileEntry(otherAF);
                            fileAlreadyExists = true;
                        }
                        else
                        {
                            // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Name is used by existing file", 
                            //     item.filename, isChecked, parent?.name ?? "null");
                            nameAlreadyInUse = true;
                        }
                        
                        break;
                    }
                }
                
                // Rename new accessfile if name is already in use
                if (nameAlreadyInUse)
                {
                    int cnt = 1;
                    bool newNameInUse = true;
                    
                    // Check new names until either an identical copy or no match is found 
                    while (newNameInUse)
                    {
                        string newName = otherAF.filename + "(" + cnt + ")";
                        
                        // Find any AccessFileEntry with the same filename
                        newNameInUse = accessFiles.Any(x => x.filename.Equals(newName));
                        if (newNameInUse)
                        {
                            AccessFile af = accessFiles.First(x => x.filename.Equals(newName));

                            // If they point to the exact same page
                            if (af.HasSameStaticProperties(otherAF))
                            {
                                af.MergeWithOtherAccessFileEntry(otherAF);
                                fileAlreadyExists = true;
                                break;
                            }

                            cnt++;
                        }
                        else
                        {
                            otherAF.filename = newName;
                            break;
                        }
                    }
                }

                if (fileAlreadyExists == false)
                {
                    // Console.WriteLine("AccessFile: filename='{0}', Checked='{1}', Parent.Name='{2}': Adding file", 
                    //     item.filename, isChecked, parent?.name ?? "null");
                    AddAccessFile(otherAF);
                }
            }
        }

        // Recursively add checked children to another keyring 
        public void AddToOtherKeyringRecursivelyBasedOnIsChecked(Keyring outputKeyring)
        {
            foreach (Keyring ke in keyrings)
            {
                Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
            }
            
            foreach (AccessFile accessFileEntry in accessFiles)
            {
                if (accessFileEntry.isChecked == true)
                {
                    outputKeyring.AddAccessFile(accessFileEntry);                    
                }
            }
        }
        
        // Recursively add copies of checked children to another keyring 
        public void AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(Keyring outputKeyring)
        {
            foreach (Keyring ke in keyrings)
            {
                Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
            }
            
            foreach (AccessFile accessFileEntry in accessFiles)
            {
                if (accessFileEntry.isChecked == true)
                {
                    var afCopy = accessFileEntry.Copy();
                    
                    outputKeyring.AddAccessFile(afCopy);                    
                }
            }
        }
        
        // Recursively add copies of children to another keyring 
        public void AddCopiesToOtherKeyringRecursively(Keyring outputKeyring)
        {
            foreach (Keyring ke in keyrings)
            {
                Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
            }
            
            foreach (AccessFile accessFileEntry in accessFiles)
            {
                var afCopy = accessFileEntry.Copy();
                    
                outputKeyring.AddAccessFile(afCopy);     
            }
        }

        public void PrepareForExportRecursively()
        {
            foreach (Keyring ke in keyrings)
            {
                ke.PrepareForExportRecursively();
            }

            // Remove private keys from AccessFileEntries if they do not have write access checked
            foreach (AccessFile accessFileEntry in accessFiles)
            {
                accessFileEntry.PrepareForExport();
                
                if (accessFileEntry.isCheckedWrite != true)
                {
                    accessFileEntry.keyList.ForEach(e => e.PrivateKey = null);
                    // AccessFileEntry.privateKey = null;
                }
            }
        }

        // Return true if this or any descendant keyring has at least one AccessFileEntry
        public bool HasAccessFileEntryDescendant()
        {
            return accessFiles.Count > 0 || keyrings.Any(ke => ke.HasAccessFileEntryDescendant());
        }

        public void RemoveEmptyDescendantsRecursively()
        {
            List<Keyring> removeList = new();
            foreach (Keyring ke in keyrings)
            {
                if (ke.HasAccessFileEntryDescendant() == false)
                {
                    removeList.Add(ke);
                }
                else
                {
                    ke.RemoveEmptyDescendantsRecursively();
                }
            }
            foreach (Keyring ke in removeList)
            {
                RemoveKeyring(ke);
            }
        }

        public void SortAllRecursively()
        {
            SortKeyrings();
            SortAccessFiles();

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
        
        public void SortAccessFiles()
        {
            var sortedList = accessFiles.OrderBy(entry => entry.filename).ToList();
            ClearAccessFiles();
            AddRangeAccessFile(sortedList);
        }

        public List<AccessFile> GetAllAndDescendantAccessFileEntries()
        {
            var outputList = new List<AccessFile>();

            outputList.AddRange(accessFiles);

            foreach (var keyring in keyrings)
            {
                outputList.AddRange(keyring.GetAllAndDescendantAccessFileEntries());
            }

            return outputList;
        }
        
        public void PrintInfoRecursively()
        {
            Console.WriteLine("KeyRing: Name='{0}', Checked='{1}', Parent.Name='{2}'", 
                name, isChecked, parent?.name ?? "null");
            foreach (AccessFile item in accessFiles)
            {
                item.PrintInfo();
            }

            foreach (Keyring item in keyrings)
            {
                item.PrintInfoRecursively();
            }
        }
    }
}