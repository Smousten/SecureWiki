using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MasterKeyring : Keyring
    {

        // Maps pageNames to filepaths
        [JsonProperty] private Dictionary<string, string> MountedDirMapping = new();
        
        public MasterKeyring() : base()
        {
            name = "Root";
            // isChecked = false;
        }

        public void SetMountedDirMapping(string pageName, string filepath)
        {
            MountedDirMapping[pageName] = filepath;
        }

        public void SetMountedDirMappingNested(MDFolder mdFolder, string newPath)
        {
            foreach (var file in mdFolder.GetMDFiles())
            {
                SetMountedDirMapping(file.symmetricReference.accessFileTargetPageName, 
                    newPath + '/' + file.name);
            }

            foreach (var folder in mdFolder.GetMDFolders())
            {
                SetMountedDirMappingNested(folder, newPath + '/' + folder.name);                
            }
        }
        
        public string? GetMountedDirMapping(string pageName)
        {
            if (MountedDirMapping.ContainsKey(pageName))
            {
                return MountedDirMapping[pageName];
            }
            else
            {
                return null;
            }
        }

        public void CopyFromOtherKeyringNonRecursively(MasterKeyring ke)
        {
            base.CopyFromOtherKeyringNonRecursively(ke);
            MountedDirMapping = ke.MountedDirMapping;
        }


    }
}
