using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class OwnContact : Contact
    {
        [JsonProperty] public byte[] PrivateKey;
        [JsonProperty] public int revidCounter;
        
        public OwnContact(string serverLink, string pageTitle, string nickname, int revidCounter = 0) 
            : base(serverLink, pageTitle, nickname)
        {
            this.revidCounter = revidCounter;

            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            PrivateKey = newPrivateKey;
            PublicKey = newPublicKey;
        }
        
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class Contact
    {
        [JsonProperty] public byte[] PublicKey { get; set; }
        // [JsonProperty] public byte[]? PrivateKey;
        [JsonProperty] public string ServerLink { get; set; }
        [JsonProperty] public string PageTitle { get; set; }
        [JsonProperty] public string Nickname { get; set; }

        public Contact()
        {
            
        }
        
        public Contact(string serverLink, string pageTitle, string nickname, 
            byte[] publicKey)
        {
            ServerLink = serverLink;
            PageTitle = pageTitle;
            Nickname = nickname;
            PublicKey = publicKey;
            // PrivateKey = privateKey;
        }
        
        public Contact(string serverLink, string pageTitle, string nickname)
        {
            ServerLink = serverLink;
            PageTitle = pageTitle;
            Nickname = nickname;
            
            // Crypto crypto = new();
            // var (newPrivateKey, newPublicKey) = crypto.GenerateRSAParams();
            // // PrivateKey = newPrivateKey;
            // PublicKey = newPublicKey;
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
        [JsonProperty] public List<OwnContact> OwnContacts;
        [JsonProperty] public List<Contact> Contacts;
        public Manager manager;

        public ContactManager(Manager manager)
        {
            OwnContacts = new List<OwnContact>();
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
        
        public void AddOwnContact(OwnContact contact)
        {
            OwnContacts.Add(contact);
        }
        
        public void RemoveOwnContact(OwnContact contact)
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
        
        public void AddRangeOwnContacts(List<OwnContact> contacts)
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
            var newList = MergeContactLists(Contacts, newContacts);
            ClearContacts();
            AddRangeContacts(newList);
        }
        
        // TODO: Check if this actually works
        public void MergeOwnContacts(List<OwnContact> newContacts)
        {
            var newList = MergeOwnContactLists(OwnContacts, newContacts);
            ClearOwnContacts();
            AddRangeOwnContacts(newList);
        }

        private List<Contact> MergeContactLists(List<Contact> existingContacts, List<Contact> newContacts)
        {
            List<Contact> newInputList = new();
            List<Contact> resultingList = new();
            
            List<Contact> inputList = new();
            inputList.AddRange(newContacts);
            inputList = inputList.OrderBy(entry => entry.PageTitle).ToList();
            
            List<Contact> ownList = new();
            ownList.AddRange(existingContacts);
            ownList = ownList.OrderBy(entry => entry.PageTitle).ToList();

            // Check if there are any collisions in regards to PageTitle and ServerLink
            int l = 0;
            foreach (var inputContact in inputList)
            {
                while (true)
                {
                    if (l >= ownList.Count)
                    {
                        newInputList.Add(inputContact);
                        break;
                    }
                    
                    // Compares the strings and returns -1, 0, or 1 depending on their relation
                    var comp = inputContact.PageTitle.CompareTo(ownList[l].PageTitle);
                    
                    if (comp > 0)
                    {
                        l++;
                        continue;
                    }

                    if (comp < 0)
                    {
                        newInputList.Add(inputContact);
                        break;
                    }

                    if (!inputContact.ServerLink.Equals(ownList[l].ServerLink))
                    {
                        newInputList.Add(inputContact);
                        break;
                    }

                    if (!inputContact.Nickname.Equals(ownList[l].Nickname))
                    {
                        string loggerMsg = $"Imported contact with nickname '{inputContact.Nickname}' " +
                                           $"contains same information as existing contact with nickname " +
                                           $"'{ownList[l].Nickname}' and will not be added to contacts.";
                        manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                    }

                    break;
                }
            }
            
            // Sort lists by nickname
            newInputList = newInputList.OrderBy(entry => entry.Nickname).ToList();
            ownList = ownList.OrderBy(entry => entry.Nickname).ToList();

            // Check if there are any collisions in regard to Nickname
            int k = 0;
            foreach (var inputContact in newInputList)
            {
                while (true)
                {
                    if (k >= ownList.Count)
                    {
                        resultingList.Add(inputContact);
                        break;
                    }
                    
                    // Compares the strings and returns -1, 0, or 1 depending on their relation
                    var comp = inputContact.Nickname.CompareTo(ownList[k].Nickname);
                    
                    if (comp > 0)
                    {
                        k++;
                        continue;
                    }
                    if (comp < 0)
                    {
                        resultingList.Add(inputContact);
                        break;
                    }

                    inputContact.Nickname += "(1)"; 
                        
                    string loggerMsg = $"Imported contact with nickname '{ownList[k].Nickname}' " +
                                       $"contains different information compared to existing contact with same nickname " +
                                       $"and will be renamed to '{inputContact.Nickname}'.";
                    manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                    
                    resultingList.Add(inputContact);
                    break;
                }
            }
            
            resultingList.AddRange(ownList);
            resultingList = resultingList.OrderBy(entry => entry.Nickname).ToList();

            return resultingList;
        }
        
        private List<OwnContact> MergeOwnContactLists(List<OwnContact> existingContacts, List<OwnContact> newContacts)
        {
            List<OwnContact> newInputList = new();
            List<OwnContact> resultingList = new();
            
            List<OwnContact> inputList = new();
            inputList.AddRange(newContacts);
            inputList = inputList.OrderBy(entry => entry.PageTitle).ToList();
            
            List<OwnContact> ownList = new();
            ownList.AddRange(existingContacts);
            ownList = ownList.OrderBy(entry => entry.PageTitle).ToList();

            // Check if there are any collisions in regards to PageTitle and ServerLink
            int l = 0;
            foreach (var inputContact in inputList)
            {
                while (true)
                {
                    if (l >= ownList.Count)
                    {
                        newInputList.Add(inputContact);
                        break;
                    }
                    
                    // Compares the strings and returns -1, 0, or 1 depending on their relation
                    var comp = inputContact.PageTitle.CompareTo(ownList[l].PageTitle);
                    
                    if (comp > 0)
                    {
                        l++;
                        continue;
                    }

                    if (comp < 0)
                    {
                        newInputList.Add(inputContact);
                        break;
                    }

                    if (!inputContact.ServerLink.Equals(ownList[l].ServerLink))
                    {
                        newInputList.Add(inputContact);
                        break;
                    }

                    if (!inputContact.Nickname.Equals(ownList[l].Nickname))
                    {
                        string loggerMsg = $"Imported contact with nickname '{inputContact.Nickname}' " +
                                           $"contains same information as existing contact with nickname " +
                                           $"'{ownList[l].Nickname}' and will not be added to contacts.";
                        manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                    }

                    break;
                }
            }
            
            // Sort lists by nickname
            newInputList = newInputList.OrderBy(entry => entry.Nickname).ToList();
            ownList = ownList.OrderBy(entry => entry.Nickname).ToList();

            // Check if there are any collisions in regard to Nickname
            int k = 0;
            foreach (var inputContact in newInputList)
            {
                while (true)
                {
                    if (k >= ownList.Count)
                    {
                        resultingList.Add(inputContact);
                        break;
                    }
                    
                    // Compares the strings and returns -1, 0, or 1 depending on their relation
                    var comp = inputContact.Nickname.CompareTo(ownList[k].Nickname);
                    
                    if (comp > 0)
                    {
                        k++;
                        continue;
                    }
                    if (comp < 0)
                    {
                        resultingList.Add(inputContact);
                        break;
                    }

                    inputContact.Nickname += "(1)"; 
                        
                    string loggerMsg = $"Imported contact with nickname '{ownList[k].Nickname}' " +
                                       $"contains different information compared to existing contact with same nickname " +
                                       $"and will be renamed to '{inputContact.Nickname}'.";
                    manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
                    
                    resultingList.Add(inputContact);
                    break;
                }
            }
            
            resultingList.AddRange(ownList);
            resultingList = resultingList.OrderBy(entry => entry.Nickname).ToList();

            return resultingList;
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

        public Contact? GetContactByNickname(string nickname)
        {
            return Contacts.Find(entry => entry.Nickname.Equals(nickname));
        }
        
        public Contact? GetContactByPageTitle(string pageTitle)
        {
            return Contacts.Find(entry => entry.PageTitle.Equals(pageTitle));
        }
        
        public Contact? GetContactByServerLink(string serverLink)
        {
            return Contacts.Find(entry => entry.ServerLink.Equals(serverLink));
        }
        
        public List<Contact>? GetContactsByServerLink(string serverLink)
        {
            var contacts = Contacts.FindAll(entry => entry.ServerLink.Equals(serverLink));

            // Return results if any found, otherwise null
            return contacts.Count > 0 ? contacts : null;
        }
        
        public OwnContact? GetOwnContactByNickname(string nickname)
        {
            return OwnContacts.Find(entry => entry.Nickname.Equals(nickname));
        }
        
        public OwnContact? GetOwnContactByPageTitle(string pageTitle)
        {
            return OwnContacts.Find(entry => entry.PageTitle.Equals(pageTitle));
        }
        
        public OwnContact? GetOwnContactByServerLink(string serverLink)
        {
            return OwnContacts.Find(entry => entry.ServerLink.Equals(serverLink));
        }
        
        public List<OwnContact>? GetOwnContactsByServerLink(string serverLink)
        {
            var contacts = OwnContacts.FindAll(entry => entry.ServerLink.Equals(serverLink));

            // Return results if any found, otherwise null
            return contacts.Count > 0 ? contacts : null;
        }
        
    }
}