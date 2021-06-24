using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Utilities;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MasterKeyring : Keyring
    {

        // Maps pageNames to filepaths
        [JsonProperty] private Dictionary<string, string> MountedDirMapping = new();
        
        // Save lists of contacts which contains inbox references
        [JsonProperty] public ContactManager ContactManager;
        
        public MasterKeyring()
        {
            name = "Root";
            // isChecked = false;
            ContactManager = new ContactManager();
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

            return null;
        }

        public void CopyFromOtherKeyring(MasterKeyring ke)
        {
            base.CopyFromOtherKeyring(ke);
            MountedDirMapping = ke.MountedDirMapping;
            ContactManager = ke.ContactManager;
        }
        
        public List<OwnContact>? GetOwnContactsByServerLink(string serverLink)
        {
            return ContactManager.GetOwnContactsByServerLink(serverLink);
        }

        public List<string>? GetAllUniqueServerLinksFromOwnContacts()
        {
            return ContactManager.GetAllUniqueServerLinksFromOwnContacts();
        }
    }
}
