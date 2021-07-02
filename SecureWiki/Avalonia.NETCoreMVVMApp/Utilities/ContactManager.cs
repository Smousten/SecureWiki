using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Model;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class OwnContact : Contact
    {
        [JsonProperty] public int RevidCounter;
        
        public OwnContact(string nickname, InboxReference inboxReference, int revidCounter = 0) : base(nickname,
            inboxReference)
        {
            RevidCounter = revidCounter;
        }

        public Contact ConvertToBaseClass()
        {
            InboxReference.privateKey = null;
            return new(Nickname, InboxReference);
        }

        public OwnContact Copy()
        {
            var jsonData = JSONSerialization.SerializeObject(this);

            OwnContact copy = (JSONSerialization.DeserializeObject(jsonData, typeof(OwnContact)) as OwnContact)!;
            
            return copy;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Contact
    {
        [JsonProperty] public InboxReference InboxReference { get; set; }
        [JsonProperty] public string Nickname { get; set; }

        [JsonConstructor]
        public Contact(string nickname, InboxReference inboxReference)
        {
            Nickname = nickname;
            InboxReference = inboxReference;
        }

        public bool HasSameStaticProperties(Contact refContact)
        {
            // TODO: might need to fix equals method
            bool output = InboxReference.HasSameStaticProperties(refContact.InboxReference);
        
            return output;
        }
    }
    
    
    [JsonObject(MemberSerialization.OptIn)]
    public class ContactManager
    {
        [JsonProperty] public List<OwnContact> OwnContacts;
        [JsonProperty] public List<Contact> Contacts;

        public ContactManager()
        {
            OwnContacts = new List<OwnContact>();
            Contacts = new List<Contact>();
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

        public Contact? FindContact(InboxReference inboxReference)
        {
            return Contacts.FirstOrDefault(c => c.InboxReference.HasSameStaticProperties(inboxReference));
        }
        
        public Contact? FindOwnContact(InboxReference inboxReference)
        {
            return OwnContacts.FirstOrDefault(c => c.InboxReference.HasSameStaticProperties(inboxReference));
        }
    }
}