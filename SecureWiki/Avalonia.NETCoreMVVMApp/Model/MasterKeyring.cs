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
        [JsonProperty] public List<OwnContact> OwnContacts = new();
        [JsonProperty] public List<Contact> Contacts = new();
        
        public MasterKeyring()
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

            return null;
        }

        public void CopyFromOtherKeyringNonRecursively(MasterKeyring ke)
        {
            base.CopyFromOtherKeyringNonRecursively(ke);
            MountedDirMapping = ke.MountedDirMapping;
            OwnContacts = ke.OwnContacts;
            Contacts = ke.Contacts;
        }
        
        public List<OwnContact>? GetOwnContactsByServerLink(string serverLink)
        {
            var contacts = OwnContacts.FindAll(entry => entry.InboxReference.serverLink.Equals(serverLink));
        
            // Return results if any found, otherwise null
            return contacts.Count > 0 ? contacts : null;
        }

        public List<string>? GetAllUniqueServerLinksFromOwnContacts()
        {
            List<string> output = new();
        
            var sortedContacts = OwnContacts.OrderBy(c => c.InboxReference.serverLink).ToList();
        
            // Iterate over all contacts and add unique server links to output list
            int i = 0;
            while (i < sortedContacts.Count)
            {
                int cnt = 1;
        
                while (i + cnt < sortedContacts.Count &&
                       sortedContacts[i].InboxReference.serverLink.Equals(
                           sortedContacts[i + cnt].InboxReference.serverLink))
                {
                    cnt++;
                }
        
                output.Add(sortedContacts[i].InboxReference.serverLink);
                i += cnt;
            }
        
            // If any server links have been found, return those.
            return output.Count > 0 ? output : null;
        }

    }
}
