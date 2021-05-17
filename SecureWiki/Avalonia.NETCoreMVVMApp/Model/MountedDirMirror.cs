using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DynamicData;
using JetBrains.Annotations;
using ReactiveUI;
using SecureWiki.Utilities;

namespace SecureWiki.Model
{
    public class MountedDirMirror
    {
        public MDFolder RootFolder;

        public MountedDirMirror()
        {
            RootFolder = new MDFolder("root", null);
        }

        public MDFile? GetMDFile(string path)
        {
            var pathArr = path.Split('/');
            return RootFolder.FindFileRecursively(pathArr, 0);
        }

        public MDFile? AddFile(string path, SymmetricReference reference)
        {
            var pathArr = path.Split('/');
            return RootFolder.AddFileRecursively(pathArr, 0, reference);
        }

        public MDFolder? GetMDFolder(string path)
        {
            var pathArr = path.Split('/');
            return RootFolder.FindFolderRecursively(pathArr, 0);
        }

        public MDFolder? AddFolder(string path)
        {
            var pathArr = path.Split('/');
            return RootFolder.AddFolderRecursively(pathArr, 0);
        }

        public void Clear()
        {
            RootFolder.ClearFiles();
            RootFolder.ClearFolders();
        }

        public void PrintInfo()
        {
            RootFolder.PrintInfoRecursively();
        }

        public void CreateFileStructureRecursion(string path)
        {
            RootFolder.CreateFileStructureRecursion(path);
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
        
        protected bool? _isChecked = false;
        public bool? isChecked
        {
            get => (_isChecked ?? false);
            set
            {
                _isChecked = value;
                RaisePropertyChanged(nameof(isChecked));
                RaisePropertyChanged(nameof(isCheckedWriteEnabled));
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
            }
        }
      
        public bool isCheckedWriteEnabled => isChecked ?? false;
        

        public MDItem(string name)
        {
            this.name = name;
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
    }

    public class MDFolder : MDItem
    {
        public MDFolder? Parent;

        // private List<MDFolder> Folders = new();
        // private List<MDFile> Files = new();

        private ObservableCollection<MDFolder> Folders = new();
        private ObservableCollection<MDFile> Files = new();
        
        public ObservableCollection<object> combinedList
        {
            get
            {
                var output = new ObservableCollection<object>();

                foreach (var entry in Folders)
                {
                    output.Add(entry);
                }
                foreach (var entry in Files)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }
        
        public MDFolder(string name, MDFolder? parent) : base(name)
        {
            Parent = parent;
        }

        private MDFolder()
        {
            
        }

        public void CreateFileStructureRecursion(string path)
        {
            foreach (var file in Files)
            {
                File.Create(Path.Combine(path, file.name)).Dispose();
            }

            foreach (var childFolder in Folders)
            {
                Directory.CreateDirectory(Path.Combine(path, childFolder.name));
                childFolder.CreateFileStructureRecursion(Path.Combine(path, childFolder.name));
            }
        }

        public void AddFile(MDFile mdFile)
        {
            if (!Files.Contains(mdFile))
            {
                // var index = Files.BinarySearch(mdFile, new MDFileComparer());
                // Files.Insert(index, mdFile);
                Files.Add(mdFile);
            }
            
            RaisePropertiesChangedFiles();
        }

        public void AddRangeFiles(List<MDFile> mdFiles)
        {
            Files.AddRange(mdFiles);
            
            RaisePropertiesChangedFiles();
        }
        
        public void RemoveFile(MDFile mdFile)
        {
            if (Files.Contains(mdFile))
            {
                Files.Remove(mdFile);
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
            }
            
            RaisePropertiesChangedFolders();
        }
        
        public void AddRangeFolders(List<MDFolder> mdFolders)
        {
            Folders.AddRange(mdFolders);
            
            RaisePropertiesChangedFolders();
        }

        public void RemoveFolder(MDFolder mdFolder)
        {
            if (Folders.Contains(mdFolder))
            {
                Folders.Remove(mdFolder);
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
            ClearFiles();
            AddRangeFiles(sortedList);
        }
        
        public void SortFolders()
        {
            var sortedList = Folders.OrderBy(x => x.name).ToList();
            ClearFolders();
            AddRangeFolders(sortedList);
        }

        public MDFile? FindFileRecursively(string[] path, int cnt)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {name = path[cnt]}, new MDFileComparer());
                if (index < 0) return null;
                return Files[index];
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0) return null;
                cnt++;
                return Folders[index].FindFileRecursively(path, cnt);
            }
        }

        public MDFile? AddFileRecursively(string[] path, int cnt, SymmetricReference reference)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {name = path[cnt]}, new MDFileComparer());
                if (index < 0)
                {
                    var newMDFile = new MDFile(path[cnt], this, reference);
                    AddFile(newMDFile);
                    return newMDFile;
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var newFolder = new MDFolder(path[cnt], this);
                    AddFolder(newFolder);
                    cnt++;
                    newFolder.AddFileRecursively(path, cnt, reference);
                }
                else
                {
                    cnt++;
                    Folders[index].AddFileRecursively(path, cnt, reference);
                }
            }

            return null;
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
                    var newMDFolder = new MDFolder(path[cnt], this);
                    AddFolder(newMDFolder);
                    return newMDFolder;
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {name = path[cnt]}, new MDFolderComparer());
                if (index < 0)
                {
                    var newFolder = new MDFolder(path[cnt], this);
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
            Console.WriteLine("MDFolder '{0}':", name);
            foreach (var mdfolder in Folders)
            {
                mdfolder.PrintInfoRecursively();
            }
            foreach (var mdfile in Files)
            {
                mdfile.PrintInfo();
            }
        }

        // Events
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
    }

    public class MDFile : MDItem
    {
        public new bool? isCheckedWrite
        {
            get
            {
                if (symmetricReference.targetAccessFile?.keyList.TrueForAll(e => e.PrivateKey == null) == true)
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
        
        public new bool isCheckedWriteEnabled
        {
            get
            {
                if (symmetricReference.targetAccessFile?.keyList.TrueForAll(e => e.PrivateKey == null) == true)
                {
                    return false;
                }
                return isChecked ?? false;
            }
        }
        
        public MDFolder Parent;

        // public AccessFileReference accessFileReference;
        public SymmetricReference symmetricReference { get; set; }

        public MDFile(string filename, MDFolder parent, SymmetricReference reference) : base(filename)
        {
            Parent = parent;
            symmetricReference = reference;
        }

        public MDFile()
        {
        }

        public void PrintInfo()
        {
            Console.WriteLine("MDFile '{0}'", name);
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
    }
    
    
    
    
}