using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DynamicData;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Keyring //: IReactiveObject
    {
        [JsonProperty(Order = -4)]
        public string name { get; set; }

        [JsonProperty(Order = 99)] public List<SymmetricReference> SymmetricReferences = new();
        [JsonProperty] public InboxReference InboxReferenceToSelf;
        public AccessFileReference accessFileReferenceToSelf;
        public bool HasBeenChanged = false;

        public Keyring? parent;
        
        public ObservableCollection<Keyring> keyrings { get; set; } = new();
        public ObservableCollection<AccessFile> accessFiles { get; set; } = new();

        public Keyring(AccessFileReference accessFileReferenceToSelf, string name = "unnamed")
        {
            this.name = name;
            this.accessFileReferenceToSelf = accessFileReferenceToSelf;
            accessFileReferenceToSelf.KeyringTarget = this;
            HasBeenChanged = true;
        }
        
        public Keyring(string name = "unnamed")
        {
            this.name = name;
            }
        
        public Keyring()
        {
            name = "unnamed";
        }
        
        public bool IsValid()
        {
            var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            // properties.Add(GetType().GetProperty(nameof(name)));

            // Fields to be checked
            fields.Add(typeof(Keyring).GetField(nameof(InboxReferenceToSelf)));
            fields.Add(typeof(Keyring).GetField(nameof(SymmetricReferences)));
            
            foreach (var val in properties.Select(prop => prop?.GetValue(this)))
            {
                switch (val)
                {
                    case null:
                    case string {Length: < 2}:
                    case byte[] {Length: < 2}:
                        return false;
                }
            }
            
            if (fields.Any(field => field?.GetValue(this) == null))
            {
                return false;
            }

            if (SymmetricReferences.Any(symmRef => !symmRef.IsValid()))
            {
                return false;
            }

            // Check if reference is also valid
            return InboxReferenceToSelf.IsValid();
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
        }

        public void AddRangeKeyring(List<Keyring> keyringEntries)
        {
            keyrings.AddRange(keyringEntries);
        }
        
        public void RemoveKeyring(Keyring keyring)
        {
            keyrings.Remove(keyring);
        }
        
        public void AddAccessFile(AccessFile accessFile)
        {
            accessFiles.Add(accessFile);
        }
        
        public void AddRangeAccessFile(List<AccessFile> accessFileEntries)
        {
            accessFiles.AddRange(accessFileEntries);
        }
        
        public void RemoveAccessFile(AccessFile accessFile)
        {
            accessFiles.Remove(accessFile);
        }

        public void ClearKeyrings()
        {
            keyrings.Clear();
        }

        public void ClearAccessFiles() {
            accessFiles.Clear();
        }
        
        // public event PropertyChangedEventHandler? PropertyChanged;
        // public event PropertyChangingEventHandler? PropertyChanging;
        //
        // public void RaisePropertyChanging(PropertyChangingEventArgs args)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public void RaisePropertyChanged(PropertyChangedEventArgs args)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public void RaisePropertyChanged(string propertyName)
        // {
        //     PropertyChangedEventHandler? handler = PropertyChanged;
        //     handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
        //
        // [NotifyPropertyChangedInvocator]
        // public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        // {
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
        //
        // protected virtual void OnCheckedChanged(EventArgs e)
        // {
        //     EventHandler handler = CheckedChanged;
        //     handler(this, e);
        // }
        //
        // protected virtual void OnCheckedWriteChanged(EventArgs e)
        // {
        //     EventHandler handler = CheckedWriteChanged;
        //     handler(this, e);
        // }
        //
        // public event EventHandler CheckedChanged = null!;
        // public event EventHandler CheckedWriteChanged = null!;
        //
        // // Update parent isChecked based own and any other siblings' values
        // protected void CheckedChangedUpdateParent(object? sender, EventArgs e)
        // {
        //     parent?.UpdateIsCheckedBasedOnChildren();
        // }
        //
        // // Update children isChecked based own value
        // protected void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        // {
        //     // Disable events updating ancestors while setting values  
        //     foreach (Keyring child in keyrings)
        //     {
        //         child.CheckedChanged -= child.CheckedChangedUpdateParent;
        //         child.isChecked = isChecked;
        //         child.CheckedChanged += child.CheckedChangedUpdateParent;
        //     }
        //     
        //     foreach (AccessFile child in accessFiles)
        //     {
        //         child.CheckedChanged -= child.CheckedChangedUpdateParent;
        //         child.isChecked = isChecked;
        //         child.CheckedChanged += child.CheckedChangedUpdateParent;
        //     }
        // }
        //
        // // Update children isCheckedWrite based own value
        // protected void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        // {
        //     foreach (Keyring child in keyrings)
        //     {
        //         child.isCheckedWrite = isCheckedWrite;
        //     }
        //     
        //     foreach (AccessFile child in accessFiles)
        //     {
        //         child.isCheckedWrite = isCheckedWrite;
        //     }
        // }
        //
        // public virtual void UpdateIsCheckedBasedOnChildren()
        // {
        //     // Prevent feedback loop
        //     this.CheckedChanged -= this.CheckedChangedUpdateChildren;
        //     
        //     bool anyChecked = false;
        //     bool atLeastTwoChecked = false;
        //     bool anyUnchecked = false;
        //     bool ancestorChecked = false;
        //     
        //     foreach (Keyring child in keyrings)
        //     {
        //         if (child.isChecked == true)
        //         {
        //             if (anyChecked)
        //             {
        //                 atLeastTwoChecked = true;
        //             }
        //             anyChecked = true;
        //         }
        //         else
        //         {
        //             anyUnchecked = true;
        //         }
        //     }
        //     
        //     foreach (AccessFile child in accessFiles)
        //     {
        //         if (child.isChecked == true)
        //         {
        //             if (anyChecked)
        //             {
        //                 atLeastTwoChecked = true;
        //             }
        //             anyChecked = true;
        //         }
        //         else
        //         {
        //             anyUnchecked = true;
        //         }
        //     }
        //
        //     var localParent = parent;
        //     List<Keyring> ancestorList = new();
        //
        //     // Find chain of unchecked ancestors and set isChecked to true
        //     while (localParent != null)
        //     {
        //         if (localParent.isChecked == true)
        //         {
        //             ancestorChecked = true;
        //
        //             // Disable events updating ancestors or children while setting values  
        //             foreach (Keyring item in ancestorList)
        //             {
        //                 item.CheckedChanged -= item.CheckedChangedUpdateChildren;
        //                 item.CheckedChanged -= item.CheckedChangedUpdateParent;
        //                 item.isChecked = true;
        //                 item.CheckedChanged += item.CheckedChangedUpdateChildren;
        //                 item.CheckedChanged += item.CheckedChangedUpdateParent;
        //             }
        //
        //             break;
        //         }
        //
        //         ancestorList.Add(localParent);
        //         localParent = localParent.parent;
        //     }
        //
        //     // Console.WriteLine("AnyUnchecked='{0}'", anyUnchecked);
        //     // Console.WriteLine("AnyChecked='{0}'", anyChecked);
        //     // Console.WriteLine("atLeastTwoChecked='{0}'", atLeastTwoChecked);
        //     // Console.WriteLine("ancestorChecked='{0}'", ancestorChecked);
        //     //
        //     // Change here to interact with IsThreeState properly
        //     // if (anyChecked && anyUnchecked)
        //     // {
        //     //     IsChecked = false;
        //     // }
        //     if (anyUnchecked == false || atLeastTwoChecked || (ancestorChecked && anyChecked))
        //     {
        //         isChecked = true;
        //     }
        //     else if (anyChecked == false)
        //     {
        //         isChecked = false;
        //     }
        //     
        //     // Restore event handler
        //     this.CheckedChanged += this.CheckedChangedUpdateChildren;
        // }


        public void CopyFromOtherKeyring(Keyring ke)
        {
            keyrings.Clear();
            accessFiles.Clear();
            
            name = ke.name;
            InboxReferenceToSelf = ke.InboxReferenceToSelf;
            accessFileReferenceToSelf = ke.accessFileReferenceToSelf;
            parent = ke.parent;
            SymmetricReferences = ke.SymmetricReferences;
            
            MergeAllEntriesFromOtherKeyring(ke);
        }
        
        public void CopyFromOtherKeyringNonRecursively(Keyring ke)
        {
            ClearKeyrings();
            AddRangeKeyring(ke.keyrings.ToList());
            ClearAccessFiles();
            AddRangeAccessFile(ke.accessFiles.ToList());
            
            name = ke.name;
            InboxReferenceToSelf = ke.InboxReferenceToSelf;
            accessFileReferenceToSelf = ke.accessFileReferenceToSelf;
            parent = ke.parent;
            SymmetricReferences = ke.SymmetricReferences;
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

        // // Recursively add checked children to another keyring 
        // public void AddToOtherKeyringRecursivelyBasedOnIsChecked(Keyring outputKeyring)
        // {
        //     foreach (Keyring ke in keyrings)
        //     {
        //         Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
        //         outputKeyring.AddKeyring(keCopy);
        //         
        //         ke.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
        //     }
        //     
        //     foreach (AccessFile accessFileEntry in accessFiles)
        //     {
        //         if (accessFileEntry.isChecked == true)
        //         {
        //             outputKeyring.AddAccessFile(accessFileEntry);                    
        //         }
        //     }
        // }
        
        // // Recursively add copies of checked children to another keyring 
        // public void AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(Keyring outputKeyring)
        // {
        //     foreach (Keyring ke in keyrings)
        //     {
        //         Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
        //         outputKeyring.AddKeyring(keCopy);
        //         
        //         ke.AddCopiesToOtherKeyringRecursivelyBasedOnIsChecked(keCopy);
        //     }
        //     
        //     foreach (AccessFile accessFileEntry in accessFiles)
        //     {
        //         if (accessFileEntry.isChecked == true)
        //         {
        //             var afCopy = accessFileEntry.Copy();
        //             
        //             outputKeyring.AddAccessFile(afCopy);                    
        //         }
        //     }
        // }
        
        // Recursively add copies of children to another keyring 
        public void AddCopiesToOtherKeyringRecursively(Keyring outputKeyring)
        {
            foreach (Keyring ke in keyrings)
            {
                Keyring keCopy = new(ke.accessFileReferenceToSelf, ke.name);
                outputKeyring.AddKeyring(keCopy);
                
                ke.AddCopiesToOtherKeyringRecursively(keCopy);
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
                // accessFileEntry.PrepareForExport();
                
                // if (accessFileEntry.isCheckedWrite != true)
                // {
                    accessFileEntry.keyList.ForEach(e => e.PrivateKey = null);
                    // AccessFileEntry.privateKey = null;
                // }
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
        
        public List<SymmetricReference> GetAllAndDescendantSymmetricReferencesToGenericFiles(List<Keyring> visitedKeyrings)
        {
            var outputList = new List<SymmetricReference>();
            visitedKeyrings.Add(this);

            foreach (var symmRef in SymmetricReferences)
            {
                if (symmRef.type == PageType.GenericFile)
                {
                    outputList.Add(symmRef);
                }
                else
                {
                    var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                    if (kr == null)
                    {
                        Console.WriteLine("GetAllAndDescendantSymmetricReferencesToGenericFiles:- Keyring is null");
                        continue;
                    }
                    
                    if (visitedKeyrings.Contains(kr))
                    {
                        Console.WriteLine("keyring already visited, name = " + this.name);
                        continue;
                    }
                    var res = kr.GetAllAndDescendantSymmetricReferencesToGenericFiles(visitedKeyrings);
                    outputList.AddRange(res);
                }
            }

            return outputList;
        }

        // public List<SymmetricReference> GetAllAndDescendantSymmetricReferencesToKeyrings(List<Keyring> visitedKeyrings)
        // {
        //     Console.WriteLine("GetAllAndDescendantSymmetricReferencesToKeyrings entered, visitedKeyrings.count = " + visitedKeyrings.Count);
        //     var outputList = new List<SymmetricReference>();
        //     visitedKeyrings.Add(this);
        //
        //     foreach (var symmRef in SymmetricReferences)
        //     {
        //         Console.WriteLine("getting descendants from symmRef.target='{0}', type=='{1}'", 
        //             symmRef.accessFileTargetPageName, symmRef.type.ToString());
        //         if (symmRef.type == PageType.Keyring)
        //         {
        //             outputList.Add(symmRef);
        //             
        //             var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
        //             if (kr == null)
        //             {
        //                 Console.WriteLine("GetAllAndDescendantSymmetricReferencesToGenericFiles:- Keyring is null");
        //                 continue;
        //             }
        //             
        //             if (visitedKeyrings.Contains(kr))
        //             {
        //                 Console.WriteLine("keyring already visited, name = " + this.name);
        //                 continue;
        //             }
        //             
        //             var res = kr.GetAllAndDescendantSymmetricReferencesToKeyrings(visitedKeyrings);
        //             outputList.AddRange(res);
        //         }
        //     }
        //
        //     return outputList;
        // }
        
        public List<Keyring> GetAllAndDescendantKeyrings(List<Keyring> visitedKeyrings)
        {
            var outputList = new List<Keyring>();
            visitedKeyrings.Add(this);
        
            foreach (var symmRef in SymmetricReferences)
            {
                if (symmRef.type == PageType.Keyring)
                {
                    var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                    if (kr == null)
                    {
                        Console.WriteLine("GetAllAndDescendantSymmetricReferencesToGenericFiles:- Keyring is null");
                        continue;
                    }
                    outputList.Add(kr);
                    if (visitedKeyrings.Contains(kr))
                    {
                        Console.WriteLine("keyring already visited, name = " + this.name);
                        continue;
                    }
                    
                    var res = kr.GetAllAndDescendantKeyrings(visitedKeyrings);
                    outputList.AddRange(res);
                }
            }
        
            return outputList;
        }
        
        public void PrintInfoRecursively()
        {
            // Console.WriteLine("KeyRing: Name='{0}', Checked='{1}', Parent.Name='{2}'", 
            //     name, isChecked, parent?.name ?? "null");
            Console.WriteLine("KeyRing: Name='{0}', Parent.Name='{1}'", 
                name, parent?.name ?? "null");
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