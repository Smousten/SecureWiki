using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Contact
    {
        [JsonProperty] public byte[] PublicKey;
        [JsonProperty] public byte[]? PrivateKey;
        [JsonProperty] public string ServerLink;
        [JsonProperty] public string PageTitle;
        [JsonProperty] public string Nickname;

        public Contact(string serverLink, string pageTitle, string nickname, 
            byte[] publicKey, byte[]? privateKey)
        {
            ServerLink = serverLink;
            PageTitle = pageTitle;
            Nickname = nickname;
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }
        
        public Contact(string serverLink, string pageTitle, string nickname)
        {
            ServerLink = serverLink;
            PageTitle = pageTitle;
            Nickname = nickname;
            
            Crypto crypto = new();
            var (newPrivateKey, newPublicKey) = crypto.GenerateRSAParams();
            PrivateKey = newPrivateKey;
            PublicKey = newPublicKey;
        }

        public bool HasSameStaticProperties(Contact refContact)
        {
            bool output = ServerLink.Equals(refContact.ServerLink) && PageTitle.Equals(refContact.PageTitle);

            return output;
        }
        
    }
    
    [JsonObject(MemberSerialization.OptIn)]
    public class ContactManager
    {
        [JsonProperty] public List<Contact> OwnContacts;
        [JsonProperty] public List<Contact> Contacts;
        public Manager manager;

        public ContactManager(Manager manager)
        {
            OwnContacts = new List<Contact>();
            Contacts = new List<Contact>();
            this.manager = manager;
        }

        public void AddContact(Contact contact)
        {
            Contacts.Add(contact);
        }

        public void RemoveContact(Contact contact)
        {
            if (Contacts.Contains(contact))
            {
                Contacts.Remove(contact);
            }
        }
        
        public void AddOwnContact(Contact contact)
        {
            OwnContacts.Add(contact);
        }
        
        public void RemoveOwnContact(Contact contact)
        {
            if (OwnContacts.Contains(contact))
            {
                OwnContacts.Remove(contact);
            }
        }

        public void AddRangeContacts(List<Contact> contacts)
        {
            Contacts.AddRange(contacts);
        }
        
        public void AddRangeOwnContacts(List<Contact> contacts)
        {
            OwnContacts.AddRange(contacts);
        }
        
        public void ClearContacts()
        {
            Contacts.Clear();
        }
        
        public void ClearOwnContacts()
        {
            OwnContacts.Clear();
        }

        public void MergeContacts(List<Contact> newContacts)
        {
            MergeContactLists(Contacts, newContacts);
        }
        
        public void MergeOwnContacts(List<Contact> newContacts)
        {
            MergeContactLists(OwnContacts, newContacts);
        }

        private void MergeContactLists(List<Contact> existingContacts, List<Contact> newContacts)
        {
            List<Contact> newInputList = new();
            List<Contact> resultingList = new();
            
            List<Contact> inputList = new();
            inputList.AddRange(newContacts);
            inputList = inputList.OrderBy(entry => entry.PageTitle).ToList();
            
            List<Contact> ownList = new();
            ownList.AddRange(existingContacts);
            ownList = ownList.OrderBy(entry => entry.PageTitle).ToList();

            int l = 0;

            foreach (var inputContact in inputList)
            {
                while (true)
                {
                    if (l > ownList.Count)
                    {
                        break;
                    }

                    var comp = inputContact.PageTitle.CompareTo(ownList[l].PageTitle);
                    
                    if (comp > 0)
                    {
                        continue;
                    }
                    else if (comp < 0)
                    {
                        newInputList.Add(inputContact);
                        break;
                    }
                    else
                    {
                        if (!inputContact.ServerLink.Equals(ownList[l].ServerLink))
                        {
                            continue;
                        }
                        else if (!inputContact.Nickname.Equals(ownList[l].Nickname))
                        {
                            string loggerMsg = $"Imported contact with nickname '{inputContact.Nickname}' " +
                                               $"contains same information as existing contact with nickname " +
                                               $"'{ownList[l].Nickname}' and will not be added to contacts.";
                            manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                        }
                    }
                    
                    l++;
                }
            }
            
            // Sort lists by nickname
            newInputList = newInputList.OrderBy(entry => entry.Nickname).ToList();
            ownList = ownList.OrderBy(entry => entry.Nickname).ToList();

            int k = 0;
            foreach (var inputContact in newInputList)
            {
                while (true)
                {
                    if (k > ownList.Count)
                    {
                        break;
                    }

                    var comp = inputContact.Nickname.CompareTo(ownList[k].Nickname);
                    
                    if (comp > 0)
                    {
                        continue;
                    }
                    else if (comp < 0)
                    {
                        resultingList.Add(inputContact);
                        break;
                    }
                    else
                    {
                        inputContact.Nickname = inputContact.Nickname + "(1)"; 
                        
                        string loggerMsg = $"Imported contact with nickname '{ownList[k].Nickname}' " +
                                           $"contains different information compared to existing contact with same nickname " +
                                           $"and will be renamed to '{inputContact.Nickname}'.";
                        manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                    }
                    
                    k++;
                }
            }
            
            resultingList = resultingList.OrderBy(entry => entry.Nickname).ToList();
            ClearContacts();
            AddRangeContacts(resultingList);
        }

        public void SortContacts()
        {
            var sortedList = Contacts.OrderBy(entry => entry.Nickname).ToList();
            ClearContacts();
            AddRangeContacts(sortedList);
        }
        
        public void SortOwnContacts()
        {
            var sortedList = OwnContacts.OrderBy(entry => entry.Nickname).ToList();
            ClearOwnContacts();
            AddRangeOwnContacts(sortedList);
        }
        
    }
}