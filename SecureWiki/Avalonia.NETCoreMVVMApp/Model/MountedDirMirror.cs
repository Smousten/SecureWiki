using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Avalonia.Input;
using DynamicData;
using JetBrains.Annotations;
using ReactiveUI;
using SecureWiki.Utilities;

namespace SecureWiki.Model
{
    public class MountedDirMirror
    {
        public MDFolder RootFolder;
        public MDFolderKeyring KeyringFolder;

        public MountedDirMirror()
        {
            RootFolder = new MDFolder("root","", null);
            KeyringFolder = new MDFolderKeyring("Keyrings","", null);
        }

        public MDFile? GetMDFile(string path)
        {
            var pathArr = path.Split('/');
            var cnt = 0;

            while (pathArr[cnt].Length < 1)
            {
                Console.WriteLine(pathArr[cnt]);
                cnt++;
            }

            return pathArr[cnt].Equals(KeyringFolder.name)
                ? KeyringFolder.FindFileRecursively(pathArr, cnt+1)
                : RootFolder.FindFileRecursively(pathArr, cnt);
        }

        public MDFile? CreateFile(string path, SymmetricReference reference)
        {
            var pathArr = path.Split('/');
            var cnt = 0;

            while (pathArr[cnt].Length < 1)
            {
                Console.WriteLine(pathArr[cnt]);
                cnt++;
            }
            
            return pathArr[cnt].Equals(KeyringFolder.name)
                ? KeyringFolder.CreateFileRecursively(pathArr, cnt+1, reference)
                : RootFolder.CreateFileRecursively(pathArr, cnt, reference);
        }
        
        public void AddFile(string path, MDFile mdFile)
        {
            var pathArr = path.Split('/');
            var cnt = 0;

            while (pathArr[cnt].Length < 1)
            {
                Console.WriteLine(pathArr[cnt]);
                cnt++;
            }

            if (pathArr[cnt].Equals(KeyringFolder.name))
            {
                KeyringFolder.AddFileRecursively(pathArr, cnt+1, mdFile);
            }
            else
            {
                RootFolder.AddFileRecursively(pathArr, cnt, mdFile);
            }
        }

        public MDFolder? GetMDFolder(string path)
        {
            var pathArr = path.Split('/');
            var cnt = 0;

            while (pathArr[cnt].Length < 1)
            {
                Console.WriteLine(pathArr[cnt]);
                cnt++;
            }

            return pathArr[cnt].Equals(KeyringFolder.name)
                ? KeyringFolder.FindFolderRecursively(pathArr, cnt+1)
                : RootFolder.FindFolderRecursively(pathArr, cnt);
        }

        public MDFolder? AddFolder(string path)
        {
            var pathArr = path.Split('/');
            var cnt = 0;

            while (pathArr[cnt].Length < 1)
            {
                Console.WriteLine(pathArr[cnt]);
                cnt++;
            }

            return pathArr[cnt].Equals(KeyringFolder.name)
                ? KeyringFolder.AddFolderRecursively(pathArr, cnt+1)
                : RootFolder.AddFolderRecursively(pathArr, cnt);
        }

        public void Clear()
        {
            RootFolder.ClearFiles();
            RootFolder.ClearFolders();
            KeyringFolder.ClearFiles();
            KeyringFolder.ClearFolders();
        }

        public void PrintInfo()
        {
            RootFolder.PrintInfoRecursively();
            KeyringFolder.PrintInfoRecursively();
        }

        public void CreateFileStructureRecursion(string path)
        {
            var keyringPath = Path.Combine(path, KeyringFolder.name);
            Directory.CreateDirectory(keyringPath);
            
            RootFolder.CreateFileStructureRecursion(path, new List<string>{keyringPath});
            KeyringFolder.CreateFileStructureRecursion(keyringPath);
        }

        public MDFile? Move(string oldPath, string newPath)
        {
            var mdFile = GetMDFile(oldPath);

            if (mdFile == null)
            {
                Console.WriteLine("Move:- could not find file at oldPath='{0}'", oldPath);
                return null;
            }
            
            mdFile.Parent.RemoveFile(mdFile);
            AddFile(newPath, mdFile);
            return mdFile;
        }
        
        public MDFolder? MoveFolder(string oldPath, string newPath)
        {
            var mdFolder = GetMDFolder(oldPath);

            if (mdFolder == null)
            {
                Console.WriteLine("Move:- could not find folder at oldPath='{0}'", oldPath);
                return null;
            }
            
            mdFolder.Parent?.RemoveFolder(mdFolder);

            var newPathSplit = newPath.Split('/');
            var newParentMDFolderPath = newPath[..(newPath.Length - newPathSplit[^1].Length - 1)];
            var newParentMDFolder = GetMDFolder(newParentMDFolderPath) ?? AddFolder(newParentMDFolderPath);

            newParentMDFolder?.AddFolder(mdFolder);
            return mdFolder;
        }

        public List<(SymmetricReference, bool)> GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedRootFolder()
        {
            return RootFolder.GetAllAndDescendantSymmetricReferencesBasedOnIsChecked();
        }
        
        public (List<(SymmetricReference, bool)>, List<(SymmetricReference, bool)>) GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring()
        {
            return KeyringFolder.GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring();
        }

        public List<(SymmetricReference, bool)> GetNecessarySymmetricReferencesToCheckedItems()
        {
            var outputList = new List<(SymmetricReference, bool)>();
            
            outputList.AddRange(GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedRootFolder());
            var (fileList, keyringList) = GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring();
            outputList.AddRange(fileList);
            outputList.AddRange(keyringList);

            return outputList;
        }
    }
    
    public abstract class MDItem : IReactiveObject
    {
        protected string _name;
        public string name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged(nameof(name));
            }
        }
        
        protected string _path;
        public string path
        {
            get => _path;
            set
            {
                _path = value;
                RaisePropertyChanged(nameof(path));
            }
        }
        
        protected bool? _isChecked = false;
        public bool? isChecked
        {
            get => (_isChecked ?? false);
            set
            {
                _isChecked = value;
                RaisePropertyChanged(nameof(isChecked));
                RaisePropertyChanged(nameof(isCheckedWriteEnabled));
                OnCheckedChanged(EventArgs.Empty);
            }
        }
        
        protected bool? _isCheckedWrite = false;
        public bool? isCheckedWrite
        {
            get => (_isCheckedWrite ?? false);
            set
            {
                _isCheckedWrite = value;
                RaisePropertyChanged(nameof(isCheckedWrite));
                OnCheckedWriteChanged(EventArgs.Empty);
            }
        }
        
        protected bool? _isCheckedEnabled = true;
        public bool? isCheckedEnabled
        {
            get => (_isCheckedEnabled ?? false);
            set
            {
                _isCheckedEnabled = value;
                RaisePropertyChanged(nameof(isCheckedEnabled));
            }
        }
      
        protected bool? _isCheckedWriteEnabled = null;
        public bool? isCheckedWriteEnabled
        {
            get
            {
                return _isCheckedWriteEnabled ?? (isChecked == true && isCheckedEnabled == true);
            }
            set
            {
                _isCheckedWriteEnabled = value;
                RaisePropertyChanged(nameof(isCheckedWriteEnabled));
            }
        }


        public MDItem(string name, string path)
        {
            this.name = name;
            this.path = path;
        }

        protected MDItem()
        {
            
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
            // The Rider IDE incorrectly thinks handler can never be null
            // ReSharper disable once ConstantConditionalAccessQualifier
            handler?.Invoke(this, e);
        }
        
        protected virtual void OnCheckedWriteChanged(EventArgs e)
        {
            EventHandler handler = CheckedWriteChanged;
            // The Rider IDE incorrectly thinks handler can never be null
            // ReSharper disable once ConstantConditionalAccessQualifier
            handler?.Invoke(this, e);
        }

        public event EventHandler CheckedChanged = null!;
        public event EventHandler CheckedWriteChanged = null!;
    }

    public class MDFolder : MDItem
    {
        public MDFolder? Parent;

        public ObservableCollection<MDFolder> Folders = new();
        public ObservableCollection<MDFile> Files = new();
        public List<MDFolder> AncestorList = new();
        
        public ObservableCollection<object> combinedList
        {
            get
            {
                var output = new ObservableCollection<object>();

                foreach (var entry in Folders)
                {
                    if (AncestorList.Contains(entry)) continue;
                    output.Add(entry);
                }
                foreach (var entry in Files)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }
        
        public MDFolder(string name, string path, MDFolder? parent) : base(name, path)
        {
            Parent = parent;
            
            CheckedChanged += CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateChildren;
            CheckedWriteChanged += CheckedWriteChangedUpdateChildren;
        }

        protected MDFolder() : base()
        {
            CheckedChanged += CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateChildren;
            CheckedWriteChanged += CheckedWriteChangedUpdateChildren;
        }

        public void CreateFileStructureRecursion(string path, List<string>? exceptions = null)
        {
            var currentFiles = Directory.GetFiles(path).ToList();
            var currentFolders = Directory.GetDirectories(path).ToList();
            // Console.WriteLine("CreateFileStructureRecursion entered");
            // Console.WriteLine();
            // Console.WriteLine("printing files");
            // foreach (var item in currentFiles)
            // {
            //     Console.WriteLine(item);
            // }
            //
            // Console.WriteLine();
            // Console.WriteLine("printing folders");
            // foreach (var item in currentFolders)
            // {
            //     Console.WriteLine(item);
            // }
            //
            foreach (var file in Files)
            {
                var currentFile = currentFiles.FirstOrDefault(x => x.Equals(Path.Combine(path, file.name)));
                if (currentFile == null)
                {
                    Console.WriteLine("Creating: " + Path.Combine(path, file.name));
                    File.Create(Path.Combine(path, file.name)).Dispose();
                }
                else
                {
                    currentFiles.Remove(currentFile);
                }
            }

            // Delete files not in MDM or exception list
            foreach (var file in currentFiles.Where(file => exceptions?.Exists(x => x.Equals(file)) != true))
            {
                Console.WriteLine("Deleting: " + Path.Combine(path, file));
                File.Delete(Path.Combine(path, file));
            }

            foreach (var childFolder in Folders)
            {
                var currentFolder = currentFolders.FirstOrDefault(x => x.Equals(Path.Combine(path, childFolder.name)));
                if (currentFolder == null)
                {
                    Console.WriteLine("Creating: " + Path.Combine(path, childFolder.name));
                    Directory.CreateDirectory(Path.Combine(path, childFolder.name));
                }
                else
                {
                    currentFolders.Remove(currentFolder);
                }
                childFolder.CreateFileStructureRecursion(Path.Combine(path, childFolder.name));
            }
            
            // Delete folders not in MDM or exception list
            foreach (var folder in currentFolders.Where(folder => exceptions?.Exists(x => x.Equals(folder)) != true))
            {
                try
                {
                    Console.WriteLine("Deleting: " + Path.Combine(path, folder));
                    Directory.Delete(Path.Combine(path, folder), true);
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine("CreateFileStructureRecursion:- UnauthorizedAccessException " +
                                      "when attempting to delete folder");
                }
            }
        }

        public void AddFile(MDFile mdFile)
        {
            if (!Files.Contains(mdFile))
            {
                // var index = Files.BinarySearch(mdFile, new MDFileComparer());
                // Files.Insert(index, mdFile);
                Files.Add(mdFile);
                SortFiles();
            }
            
            RaisePropertiesChangedFiles();
        }

        public void AddRangeFiles(List<MDFile> mdFiles)
        {
            Files.AddRange(mdFiles);
            SortFiles();
            
            RaisePropertiesChangedFiles();
        }
        
        public void RemoveFile(MDFile mdFile)
        {
            if (Files.Contains(mdFile))
            {
                Files.Remove(mdFile);
                SortFiles();
            }
            
            RaisePropertiesChangedFiles();
        }

        public void AddFolder(MDFolder mdFolder)
        {
            if (!Folders.Contains(mdFolder))
            {
                // var index = Folders.BinarySearch(mdFolder, new MDFolderComparer());
                // Folders.Insert(index, mdFolder);
                Folders.Add(mdFolder);
                mdFolder.AncestorList.Add(this);
                mdFolder.AncestorList.AddRange(AncestorList);
                SortFolders();
            }
            
            RaisePropertiesChangedFolders();
        }
        
        public void AddRangeFolders(List<MDFolder> mdFolders)
        {
            Folders.AddRange(mdFolders);
            foreach (var folder in mdFolders)
            {
                folder.AncestorList.Add(this);
                folder.AncestorList.AddRange(AncestorList);
            }
            SortFolders();
            
            RaisePropertiesChangedFolders();
        }

        public void RemoveFolder(MDFolder mdFolder)
        {
            if (Folders.Contains(mdFolder))
            {
                Folders.Remove(mdFolder);
                SortFolders();
            }
            
            RaisePropertiesChangedFolders();
        }

        public void ClearFiles()
        {
            Files.Clear();
            
            RaisePropertiesChangedFiles();
        }
        
        public void ClearFolders()
        {
            Folders.Clear();
            
            RaisePropertiesChangedFolders();
        }

        public void SortFiles()
        {
            var sortedList = Files.OrderBy(x => x.name).ToList();
            Files.Clear();
            Files.AddRange(sortedList);
            RaisePropertiesChangedFiles();
        }
        
        public void SortFolders()
        {
            var sortedList = Folders.OrderBy(x => x.name).ToList();
            Folders.Clear();
            Folders.AddRange(sortedList);
            RaisePropertiesChangedFolders();
        }

        public List<MDFolder> GetMDFolders()
        {
            return Folders.ToList();
        }
        
        public List<MDFile> GetMDFiles()
        {
            return Files.ToList();
        }

        public MDFile? FindFileRecursively(string[] path, int cnt)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {name = path[cnt]}, new MDFileComparer());
                if (index < 0)
                {
                    return null;
                }
                return Files[index];
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    return null;
                }
                cnt++;
                return Folders[index].FindFileRecursively(path, cnt);
            }
        }

        public MDFile? CreateFileRecursively(string[] path, int cnt, SymmetricReference reference)
        {
            if (path[cnt].Length < 1)
            {
                Console.WriteLine("CreateFileRecursively:- Illegal path, returning null in name=" + name);
                return null;
            }
            
            var pathString = string.Join("/", path.Take(cnt).ToArray());
            
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {name = path[cnt]}, new MDFileComparer());
                if (index < 0)
                {
                    var newMDFile = NewMDFile(path[cnt], pathString, reference);
                    AddFile(newMDFile);
                    return newMDFile;
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var newFolder = NewFolder(path[cnt], pathString);
                    AddFolder(newFolder);
                    cnt++;
                    return newFolder.CreateFileRecursively(path, cnt, reference);
                }

                cnt++;
                return Folders[index].CreateFileRecursively(path, cnt, reference);
            }

            return null;
        }

        protected virtual MDFile NewMDFile(string filename, string pathString, SymmetricReference reference)
        {
            var mdFile = new MDFile(filename, pathString, MDFile.Type.GenericFile, this, reference);
            reference.MDFile = mdFile;
            return mdFile;
        }

        public void AddFileRecursively(string[] path, int cnt, MDFile mdFile)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {name = path[cnt]}, new MDFileComparer());
                if (index < 0)
                {
                    AddFile(mdFile);
                    return;
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var pathString = string.Join("/", path.Take(cnt).ToArray());
                    var newFolder = NewFolder(path[cnt], pathString);
                    AddFolder(newFolder);
                    cnt++;
                    newFolder.AddFileRecursively(path, cnt, mdFile);
                }
                else
                {
                    cnt++;
                    Folders[index].AddFileRecursively(path, cnt, mdFile);
                }
            }

            return;
        }

        protected virtual MDFolder NewFolder(string folderName, string path)
        {
            Console.WriteLine("protected virtual MDFolder NewFolder(string folderName) entered in " + name);
            var newFolder = new MDFolder(folderName, path, this);
            return newFolder;
        }

        // TODO: refactor - make common function for file/folder
        public MDFolder? FindFolderRecursively(string[] path, int cnt)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0) return null;
                return Folders[index];
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0) return null;
                cnt++;
                return Folders[index].FindFolderRecursively(path, cnt);
            }
        }

        // TODO: refactor - make common function for file/folder
        public MDFolder? AddFolderRecursively(string[] path, int cnt)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Folders.BinarySearch(new MDFolder() {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var pathString = string.Join("/", path.Take(cnt).ToArray());
                    var newFolder = NewFolder(path[cnt], pathString);
                    AddFolder(newFolder);
                    return newFolder;
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var pathString = string.Join("/", path.Take(cnt).ToArray());
                    var newFolder = NewFolder(path[cnt], pathString);
                    AddFolder(newFolder);
                    cnt++;
                    newFolder.AddFolderRecursively(path, cnt);
                }
                else
                {
                    cnt++;
                    Folders[index].AddFolderRecursively(path, cnt);
                }
            }

            return null;
        }

        public void PrintInfoRecursively()
        {
            PrintOwnInfo();
            foreach (var mdfolder in Folders)
            {
                mdfolder.PrintInfoRecursively();
            }
            foreach (var mdfile in Files)
            {
                mdfile.PrintInfo();
            }
        }

        public virtual void PrintOwnInfo()
        {
            Console.WriteLine("MDFolder '{0}', \tchecked='{1}', \tcheckedEnabled='{2}', \tcheckedWrite='{3}', \tcheckedWriteEnabled='{4}'", 
                name, isChecked,  isCheckedEnabled, isCheckedWrite, isCheckedWriteEnabled);
        }
        
        public void RaisePropertiesChangedFiles()
        {
            RaisePropertyChanged(nameof(Files));
            RaisePropertyChanged(nameof(combinedList));
            OnPropertyChanged(nameof(combinedList));
        }
        
        public void RaisePropertiesChangedFolders()
        {
            RaisePropertyChanged(nameof(Folders));
            RaisePropertyChanged(nameof(combinedList));
            OnPropertyChanged(nameof(combinedList));
        }
        
        // Update parent isChecked based own and any other siblings' values
        protected internal void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            Parent?.UpdateIsCheckedBasedOnChildren();
        }
        
        // Update children isChecked based own value
        protected virtual void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        {
            // Disable events updating ancestors while setting values  
            foreach (var child in Folders)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (var child in Files)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }
        
        // Update children isCheckedWrite based own value
        protected virtual void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        {
            foreach (var child in Folders)
            {
                child.isCheckedWrite = isCheckedWrite;
            }
            
            foreach (var child in Files)
            {
                child.isCheckedWrite = isCheckedWrite;
            }
        }

        // Currently not used
       public virtual void UpdateIsCheckedBasedOnChildren()
        {
            // // Prevent feedback loop
            // this.CheckedChanged -= this.CheckedChangedUpdateChildren;
            //
            // bool anyChecked = false;
            // bool atLeastTwoChecked = false;
            // bool anyUnchecked = false;
            // bool ancestorChecked = false;
            //
            // foreach (var child in Folders)
            // {
            //     if (child.isChecked == true)
            //     {
            //         if (anyChecked)
            //         {
            //             atLeastTwoChecked = true;
            //         }
            //         anyChecked = true;
            //     }
            //     else
            //     {
            //         anyUnchecked = true;
            //     }
            // }
            //
            // foreach (var child in Files)
            // {
            //     if (child.isChecked == true)
            //     {
            //         if (anyChecked)
            //         {
            //             atLeastTwoChecked = true;
            //         }
            //         anyChecked = true;
            //     }
            //     else
            //     {
            //         anyUnchecked = true;
            //     }
            // }
            //
            // var localParent = Parent;
            // List<MDFolder> ancestorList = new();
            //
            // // Find chain of unchecked ancestors and set isChecked to true
            // while (localParent != null)
            // {
            //     if (localParent.isChecked == true)
            //     {
            //         ancestorChecked = true;
            //
            //         // Disable events updating ancestors or children while setting values  
            //         foreach (var item in ancestorList)
            //         {
            //             item.CheckedChanged -= item.CheckedChangedUpdateChildren;
            //             item.CheckedChanged -= item.CheckedChangedUpdateParent;
            //             item.isChecked = true;
            //             item.CheckedChanged += item.CheckedChangedUpdateChildren;
            //             item.CheckedChanged += item.CheckedChangedUpdateParent;
            //         }
            //
            //         break;
            //     }
            //
            //     ancestorList.Add(localParent);
            //     localParent = localParent.Parent;
            // }
            //
            // // Console.WriteLine("AnyUnchecked='{0}'", anyUnchecked);
            // // Console.WriteLine("AnyChecked='{0}'", anyChecked);
            // // Console.WriteLine("atLeastTwoChecked='{0}'", atLeastTwoChecked);
            // // Console.WriteLine("ancestorChecked='{0}'", ancestorChecked);
            // //
            // // Change here to interact with IsThreeState properly
            // // if (anyChecked && anyUnchecked)
            // // {
            // //     IsChecked = false;
            // // }
            // if (anyUnchecked == false || atLeastTwoChecked || (ancestorChecked && anyChecked))
            // {
            //     isChecked = true;
            // }
            // else if (anyChecked == false)
            // {
            //     isChecked = false;
            // }
            //
            // // Restore event handler
            // this.CheckedChanged += this.CheckedChangedUpdateChildren;
        }
       
       public List<SymmetricReference> GetAllAndDescendantSymmetricReferences()
       {
           var outputList = new List<SymmetricReference>();

           foreach (var file in Files)
           {
               outputList.Add(file.symmetricReference);
           }

           foreach (var child in Folders)
           {
               outputList.AddRange(child.GetAllAndDescendantSymmetricReferences());
           }

           return outputList;
       }
       
       public List<(SymmetricReference, bool)> GetAllAndDescendantSymmetricReferencesBasedOnIsChecked()
       {
           var outputList = (from file in Files where file.isChecked == true select (file.symmetricReference, file.isCheckedWrite == true)).ToList();
           
           foreach (var child in Folders)
           {
               if (child.isChecked == true)
               {
                   outputList.AddRange(child.GetAllAndDescendantSymmetricReferencesBasedOnIsChecked());
               }
           }

           return outputList;
       }
    }

    public class MDFolderKeyring : MDFolder
    {
        public MDFolderKeyring(string name, string path, MDFolder? parent) : base(name, path, parent)
        {
        }
        
        protected override MDFolder NewFolder(string folderName, string path)
        {
            var newFolder = new MDFolderKeyring(folderName, path, this);
            return newFolder;
        }
        protected override MDFile NewMDFile(string filename, string pathString, SymmetricReference reference)
        {
            var mdFile = new MDFile(filename, pathString, MDFile.Type.AccessFile, this, reference);
            reference.MDFile = mdFile;
            return mdFile;
        }
        
        // Update children isChecked based own value
        protected override void CheckedChangedUpdateChildren(object? sender, EventArgs e)
        {
            if (isChecked == false) isCheckedWrite = false;
            
            // Disable events updating ancestors while setting values  
            foreach (var child in Folders)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.isCheckedEnabled = !isChecked;
                child.isCheckedWrite = isChecked;
                child.isCheckedWriteEnabled = !isChecked;
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
            
            foreach (var child in Files)
            {
                child.CheckedChanged -= child.CheckedChangedUpdateParent;
                child.isChecked = isChecked;
                child.isCheckedEnabled = !isChecked;
                child.isCheckedWrite = child.canHaveWriteAccess == true && isChecked == true;
                if (isChecked == true)
                {
                    child.isCheckedWriteEnabled = false;
                }
                else
                {
                    child.isCheckedWriteEnabled = null;
                }
                child.CheckedChanged += child.CheckedChangedUpdateParent;
            }
        }
        
        // Does not update children isCheckedWrite based own value
        protected override void CheckedWriteChangedUpdateChildren(object? sender, EventArgs e)
        {
        }

        public override void PrintOwnInfo()
        {
            Console.WriteLine("MDFolderKeyring '{0}', \tchecked='{1}', \tcheckedEnabled='{2}', \tcheckedWrite='{3}', \tcheckedWriteEnabled='{4}'", 
                name, isChecked,  isCheckedEnabled, isCheckedWrite, isCheckedWriteEnabled);
        }

        public (List<(SymmetricReference, bool)>, List<(SymmetricReference, bool)>) GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring()
        {
            var FileList = (from file in Files where file.isChecked == true select (file.symmetricReference, file.isCheckedWrite == true)).ToList();
            var FolderList = new List<(SymmetricReference, bool)>();
            
            foreach (var child in Folders)
            {
                if (child.isChecked == true)
                {
                    var mdFile = child.Files.FirstOrDefault(e => e.name.Equals("self"));
                    if (mdFile != null) FolderList.Add((mdFile.symmetricReference, child.isCheckedWrite == true));
                }
                else if (child.GetType() == typeof(MDFolderKeyring))
                {
                    var (FileListRes, FolderListRes) = ((MDFolderKeyring) child)
                        .GetAllAndDescendantSymmetricReferencesBasedOnIsCheckedKeyring();
                    FileList.AddRange(FileListRes);
                    FolderList.AddRange(FolderListRes);
                }
            }

            return (FileList, FolderList);
        }
        
    }

    public class MDFile : MDItem
    {
        public enum Type
        {
            GenericFile,
            Keyring,
            AccessFile
        }
        
        public bool? isCheckedWrite
        {
            get
            {
                if (canHaveWriteAccess == false)
                {
                    return false;
                }
                return (_isCheckedWrite ?? false);
            }
            set
            {
                _isCheckedWrite = value;
                RaisePropertyChanged(nameof(isCheckedWrite));
            }
        }

        private bool? _isCheckedWriteEnabled = null;
        public bool? isCheckedWriteEnabled
        {
            get
            {
                if (_isCheckedWriteEnabled != null) return _isCheckedWriteEnabled;
                if (canHaveWriteAccess == false)
                {
                    return false;
                }
                return isChecked ?? false;
            }
            set
            {
                if (canHaveWriteAccess == true)
                {
                    _isCheckedWriteEnabled = value;
                    RaisePropertyChanged(nameof(isCheckedWriteEnabled));
                    OnCheckedWriteChanged(EventArgs.Empty);
                }
            }
        }

        private bool? _canHaveWriteAccess = null;
        public bool? canHaveWriteAccess
        {
            get
            {
                if (_canHaveWriteAccess != null) return _canHaveWriteAccess;
                return symmetricReference.targetAccessFile?.keyList.TrueForAll(
                    e => e.PrivateKey == null) == false;
            }
            set
            {
                _canHaveWriteAccess = value;
                RaisePropertyChanged(nameof(isCheckedWriteEnabled));
            }
        }
        
        public MDFolder Parent;
        public Type TargetType;

        // public AccessFileReference accessFileReference;
        public SymmetricReference symmetricReference { get; set; }

        public MDFile(string filename, string path, Type targetType, MDFolder parent, SymmetricReference reference) : base(filename, path)
        {
            Parent = parent;
            symmetricReference = reference;
            this.TargetType = targetType;
            
            // Set event handlers
            CheckedChanged -= CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateParent;
        }

        public MDFile()
        {
            // Set event handlers
            CheckedChanged -= CheckedChangedUpdateParent;
            CheckedChanged += CheckedChangedUpdateParent;
        }

        public void PrintInfo()
        {
            Console.Write("MDFile '{0}'", name);
            if (name.Length < 12) Console.Write("\t");
            Console.WriteLine("\tchecked='{0}', \tcheckedEnabled='{1}', \tcheckedWrite='{2}', " +
                              "\tcheckedWriteEnabled='{3}', \tcanHaveWriteAccess='{4}'", 
                isChecked,  isCheckedEnabled, isCheckedWrite, isCheckedWriteEnabled, canHaveWriteAccess);
        }

        public void CheckedChangedUpdateParent(object? sender, EventArgs e)
        {
            Parent.UpdateIsCheckedBasedOnChildren();
        }
    }
}