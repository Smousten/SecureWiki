using System.Collections.Generic;
using System.Linq;
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

        public void AddFile(string path, AccessFileReference reference)
        {
            var pathArr = path.Split('/');
            RootFolder.AddFileRecursively(pathArr, 0, reference);
        }
    }

    public class MDFolder
    {
        public string Name;
        public MDFolder? Parent;

        private List<MDFolder> Folders = new();
        private List<MDFile> Files = new();
        
        public MDFolder(string name, MDFolder? parent)
        {
            Name = name;
            Parent = parent;
        }

        private MDFolder()
        {
            
        }

        public void AddFile(MDFile mdFile)
        {
            if (!Files.Contains(mdFile))
            {
                Files.Add(mdFile);
            }
            SortFiles();
        }
        
        public void RemoveFile(MDFile mdFile)
        {
            if (Files.Contains(mdFile))
            {
                Files.Remove(mdFile);
            }
            SortFiles();
        }
        
        public void AddFolder(MDFolder mdFolder)
        {
            if (!Folders.Contains(mdFolder))
            {
                Folders.Add(mdFolder);
            }
            SortFolders();
        }

        public void RemoveFolder(MDFolder mdFolder)
        {
            if (Folders.Contains(mdFolder))
            {
                Folders.Remove(mdFolder);
            }
            SortFolders();
        }
        
        public void SortFiles()
        {
            Files = Files.OrderBy(x => x.Name).ToList();
        }
        
        public void SortFolders()
        {
            Folders = Folders.OrderBy(x => x.Name).ToList();
        }

        public MDFile? FindFileRecursively(string[] path, int cnt)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {Name = path[cnt]}, new MDFileComparer());
                if (index < 0) return null;
                return Files[index];
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {Name = path[cnt]}, new MDFolderComparer());
                if (index < 0) return null;
                cnt++;
                return Folders[index].FindFileRecursively(path, cnt);
            }
        }

        public void AddFileRecursively(string[] path, int cnt, AccessFileReference reference)
        {
            if (path.Length - cnt <= 1)
            {
                var index = Files.BinarySearch(new MDFile {Name = path[cnt]}, new MDFileComparer());
                if (index < 0)
                {
                    var newMDFile = new MDFile(path[cnt], this, reference);
                    AddFile(newMDFile);
                }
            }
            else
            {
                var index = Folders.BinarySearch(new MDFolder {Name = path[cnt]}, new MDFolderComparer());
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
        }
    }

    public class MDFile
    {
        public string Name;
        public MDFolder Parent;

        public AccessFileReference Reference;

        public MDFile(string name, MDFolder parent, AccessFileReference reference)
        {
            Name = name;
            Parent = parent;
            Reference = reference;
        }

        public MDFile()
        {
        }
        
    }
    
    
    
    
}