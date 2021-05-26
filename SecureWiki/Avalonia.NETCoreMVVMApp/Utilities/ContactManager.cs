using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Cryptography;
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
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Contact
    {
        [JsonProperty] public InboxReference InboxReference { get; set; }
        [JsonProperty] public string Nickname { get; set; }

        // Empty constructor used for deserialization
        // public Contact()
        // {
        // }
        
        [JsonConstructor]
        public Contact(string nickname, InboxReference inboxReference)
        {
            Nickname = nickname;
            InboxReference = inboxReference;
        }
        

        public bool HasSameStaticProperties(Contact refContact)
        {
            // TODO: might need to fix equals method
            bool output = InboxReference.Equals(refContact.InboxReference);
        
            return output;
        }
        
    }
    
    
    
    public class ContactManager
    {
        public List<OwnContact> OwnContacts;
        public List<Contact> Contacts;
        public Manager manager;

        public ContactManager(Manager manager)
        {
            OwnContacts = new List<OwnContact>();
            Contacts = new List<Contact>();
            this.manager = manager;
        }

        // public void AddContact(Contact contact)
        // {
        //     Contacts.Add(contact);
        // }
        //
        // public void RemoveContact(Contact contact)
        // {
        //     if (Contacts.Contains(contact))
        //     {
        //         Contacts.Remove(contact);
        //     }
        // }
        //
        // public void AddOwnContact(OwnContact contact)
        // {
        //     OwnContacts.Add(contact);
        // }
        //
        // public void RemoveOwnContact(OwnContact contact)
        // {
        //     if (OwnContacts.Contains(contact))
        //     {
        //         OwnContacts.Remove(contact);
        //     }
        // }
        //
        // public void AddRangeContacts(List<Contact> contacts)
        // {
        //     Contacts.AddRange(contacts);
        // }
        //
        // public void AddRangeOwnContacts(List<OwnContact> contacts)
        // {
        //     OwnContacts.AddRange(contacts);
        // }
        //
        // public void ClearContacts()
        // {
        //     Contacts.Clear();
        // }
        //
        // public void ClearOwnContacts()
        // {
        //     OwnContacts.Clear();
        // }
        //
        // public void MergeContacts(List<Contact> newContacts)
        // {
        //     var newList = MergeContactLists(Contacts, newContacts);
        //     ClearContacts();
        //     AddRangeContacts(newList);
        // }
        //
        // // TODO: Check if this actually works
        // public void MergeOwnContacts(List<OwnContact> newContacts)
        // {
        //     var newList = MergeOwnContactLists(OwnContacts, newContacts);
        //     ClearOwnContacts();
        //     AddRangeOwnContacts(newList);
        // }
        //
        // private List<Contact> MergeContactLists(List<Contact> existingContacts, List<Contact> newContacts)
        // {
        //     // Intermediate lists
        //     List<Contact> newInputList = new();
        //     List<Contact> resultingList = new();
        //     
        //     // Sort input contacts
        //     List<Contact> inputList = new();
        //     inputList.AddRange(newContacts);
        //     inputList = inputList.OrderBy(entry => entry.PageTitle).ToList();
        //     
        //     // Sort own contacts
        //     List<Contact> ownList = new();
        //     ownList.AddRange(existingContacts);
        //     ownList = ownList.OrderBy(entry => entry.PageTitle).ToList();
        //
        //     // Check if there are any collisions in regards to PageTitle and ServerLink
        //     int l = 0;
        //     foreach (var inputContact in inputList)
        //     {
        //         // As both lists have been sorted by page title, we only need to iterate through each once
        //         while (true)
        //         {
        //             // If no collision is possible
        //             if (l >= ownList.Count)
        //             {
        //                 newInputList.Add(inputContact);
        //                 break;
        //             }
        //             
        //             // Compares the strings and returns -1, 0, or 1 depending on their relation
        //             // Ordinal comparision simply looks at the byte value of each char, disregarding culture and alphabet
        //             var comp = string.Compare(inputContact.PageTitle, ownList[l].PageTitle, 
        //                 StringComparison.Ordinal);
        //             
        //             // Keep iterating until reference is equal or greater than self
        //             if (comp > 0)
        //             {
        //                 l++;
        //                 continue;
        //             }
        //
        //             // If reference is greater than, there are no more potential collisions
        //             if (comp < 0)
        //             {
        //                 newInputList.Add(inputContact);
        //                 break;
        //             }
        //
        //             // If page titles are equal, but server link differs
        //             if (!inputContact.ServerLink.Equals(ownList[l].ServerLink))
        //             {
        //                 // If there are more entries with the same page title, continue
        //                 if (l < ownList.Count - 1)
        //                 {
        //                     if (inputContact.PageTitle.Equals(ownList[l + 1].PageTitle))
        //                     {
        //                         l++;
        //                         continue;
        //                     }
        //                 }
        //                 else
        //                 {
        //                     newInputList.Add(inputContact);
        //                     break;
        //                 }
        //             }
        //
        //             // If contact has same static information, but different nickname
        //             if (!inputContact.Nickname.Equals(ownList[l].Nickname))
        //             {
        //                 string loggerMsg = $"Imported contact with nickname '{inputContact.Nickname}' " +
        //                                    $"contains same information as existing contact with nickname " +
        //                                    $"'{ownList[l].Nickname}' and will not be added to contacts.";
        //                 manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //             }
        //
        //             break;
        //         }
        //     }
        //     
        //     // Sort lists by nickname
        //     newInputList = newInputList.OrderBy(entry => entry.Nickname).ToList();
        //     ownList = ownList.OrderBy(entry => entry.Nickname).ToList();
        //
        //     // Check if there are any collisions in regard to Nickname.
        //     // Simpler version of previous loop.
        //     int k = 0;
        //     foreach (var inputContact in newInputList)
        //     {
        //         while (true)
        //         {
        //             if (k >= ownList.Count)
        //             {
        //                 resultingList.Add(inputContact);
        //                 break;
        //             }
        //             
        //             var comp = string.Compare(inputContact.Nickname, ownList[k].Nickname, 
        //                 StringComparison.Ordinal);
        //             
        //             if (comp > 0)
        //             {
        //                 k++;
        //                 continue;
        //             }
        //             if (comp < 0)
        //             {
        //                 resultingList.Add(inputContact);
        //                 break;
        //             }
        //
        //             inputContact.Nickname += "(1)"; 
        //                 
        //             string loggerMsg = $"Imported contact with nickname '{ownList[k].Nickname}' " +
        //                                $"contains different information compared to existing contact with same nickname " +
        //                                $"and will be renamed to '{inputContact.Nickname}'.";
        //             manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //             
        //             resultingList.Add(inputContact);
        //             break;
        //         }
        //     }
        //     
        //     resultingList.AddRange(ownList);
        //     resultingList = resultingList.OrderBy(entry => entry.Nickname).ToList();
        //
        //     return resultingList;
        // }
        //
        // // TODO: copy from MergeContactLists, test and document
        // private List<OwnContact> MergeOwnContactLists(List<OwnContact> existingContacts, List<OwnContact> newContacts)
        // {
        //     List<OwnContact> newInputList = new();
        //     List<OwnContact> resultingList = new();
        //     
        //     List<OwnContact> inputList = new();
        //     inputList.AddRange(newContacts);
        //     inputList = inputList.OrderBy(entry => entry.PageTitle).ToList();
        //     
        //     List<OwnContact> ownList = new();
        //     ownList.AddRange(existingContacts);
        //     ownList = ownList.OrderBy(entry => entry.PageTitle).ToList();
        //
        //     // Check if there are any collisions in regards to PageTitle and ServerLink
        //     int l = 0;
        //     foreach (var inputContact in inputList)
        //     {
        //         while (true)
        //         {
        //             if (l >= ownList.Count)
        //             {
        //                 newInputList.Add(inputContact);
        //                 break;
        //             }
        //             
        //             // Compares the strings and returns -1, 0, or 1 depending on their relation
        //             var comp = inputContact.PageTitle.CompareTo(ownList[l].PageTitle);
        //             
        //             if (comp > 0)
        //             {
        //                 l++;
        //                 continue;
        //             }
        //
        //             if (comp < 0)
        //             {
        //                 newInputList.Add(inputContact);
        //                 break;
        //             }
        //
        //             if (!inputContact.ServerLink.Equals(ownList[l].ServerLink))
        //             {
        //                 newInputList.Add(inputContact);
        //                 break;
        //             }
        //
        //             if (!inputContact.Nickname.Equals(ownList[l].Nickname))
        //             {
        //                 string loggerMsg = $"Imported contact with nickname '{inputContact.Nickname}' " +
        //                                    $"contains same information as existing contact with nickname " +
        //                                    $"'{ownList[l].Nickname}' and will not be added to contacts.";
        //                 manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //             }
        //
        //             break;
        //         }
        //     }
        //     
        //     // Sort lists by nickname
        //     newInputList = newInputList.OrderBy(entry => entry.Nickname).ToList();
        //     ownList = ownList.OrderBy(entry => entry.Nickname).ToList();
        //
        //     // Check if there are any collisions in regard to Nickname
        //     int k = 0;
        //     foreach (var inputContact in newInputList)
        //     {
        //         while (true)
        //         {
        //             if (k >= ownList.Count)
        //             {
        //                 resultingList.Add(inputContact);
        //                 break;
        //             }
        //             
        //             // Compares the strings and returns -1, 0, or 1 depending on their relation
        //             var comp = inputContact.Nickname.CompareTo(ownList[k].Nickname);
        //             
        //             if (comp > 0)
        //             {
        //                 k++;
        //                 continue;
        //             }
        //             if (comp < 0)
        //             {
        //                 resultingList.Add(inputContact);
        //                 break;
        //             }
        //
        //             inputContact.Nickname += "(1)"; 
        //                 
        //             string loggerMsg = $"Imported contact with nickname '{ownList[k].Nickname}' " +
        //                                $"contains different information compared to existing contact with same nickname " +
        //                                $"and will be renamed to '{inputContact.Nickname}'.";
        //             manager.WriteToLogger(loggerMsg, null, LoggerEntry.LogPriority.Warning);
        //             
        //             resultingList.Add(inputContact);
        //             break;
        //         }
        //     }
        //     
        //     resultingList.AddRange(ownList);
        //     resultingList = resultingList.OrderBy(entry => entry.Nickname).ToList();
        //
        //     return resultingList;
        // }
        //
        // public void SortContacts()
        // {
        //     var sortedList = Contacts.OrderBy(entry => entry.Nickname).ToList();
        //     ClearContacts();
        //     AddRangeContacts(sortedList);
        // }
        //
        // public void SortOwnContacts()
        // {
        //     var sortedList = OwnContacts.OrderBy(entry => entry.Nickname).ToList();
        //     ClearOwnContacts();
        //     AddRangeOwnContacts(sortedList);
        // }
        //
        // public Contact? GetContactByNickname(string nickname)
        // {
        //     return Contacts.Find(entry => entry.Nickname.Equals(nickname));
        // }
        //
        // public Contact? GetContactByInboxReference(InboxReference inboxReference)
        // {
        //     return Contacts.Find(entry => entry.InboxReference.Equals(inboxReference));
        // }
        //
        // public Contact? GetContactByServerLink(string serverLink)
        // {
        //     return Contacts.Find(entry => entry.ServerLink.Equals(serverLink));
        // }
        //
        // public List<Contact>? GetContactsByServerLink(string serverLink)
        // {
        //     var contacts = Contacts.FindAll(entry => entry.ServerLink.Equals(serverLink));
        //
        //     // Return results if any found, otherwise null
        //     return contacts.Count > 0 ? contacts : null;
        // }
        //
        // public Contact? GetContactByPageTitleAndServerLink(string pageTitle, string serverLink)
        // {
        //     return Contacts.Find(entry => entry.PageTitle.Equals(pageTitle) 
        //                                   && entry.ServerLink.Equals(serverLink));
        // }
        //
        // public OwnContact? GetOwnContactByNickname(string nickname)
        // {
        //     return OwnContacts.Find(entry => entry.Nickname.Equals(nickname));
        // }
        //
        // public OwnContact? GetOwnContactByPageTitle(string pageTitle)
        // {
        //     return OwnContacts.Find(entry => entry.PageTitle.Equals(pageTitle));
        // }
        //
        // public OwnContact? GetOwnContactByServerLink(string serverLink)
        // {
        //     return OwnContacts.Find(entry => entry.ServerLink.Equals(serverLink));
        // }
        //
        // public List<OwnContact>? GetOwnContactsByServerLink(string serverLink)
        // {
        //     var contacts = OwnContacts.FindAll(entry => entry.ServerLink.Equals(serverLink));
        //
        //     // Return results if any found, otherwise null
        //     return contacts.Count > 0 ? contacts : null;
        // }
        //
        // public List<string>? GetAllUniqueServerLinksFromOwnContacts()
        // {
        //     List<string> output = new();
        //
        //     var sortedContacts = OwnContacts.OrderBy(c => c.ServerLink).ToList();
        //
        //     // Iterate over all contacts and add unique server links to output list
        //     int i = 0;
        //     while (i < sortedContacts.Count)
        //     {
        //         int cnt = 1;
        //
        //         while (i + cnt < sortedContacts.Count &&
        //                sortedContacts[i].ServerLink.Equals(sortedContacts[i + cnt].ServerLink))
        //         {
        //             cnt++;
        //         }
        //
        //         output.Add(sortedContacts[i].ServerLink);
        //         i += cnt;
        //     }
        //
        //     // If any server links have been found, return those.
        //     return output.Count > 0 ? output : null;
        // }
    }
}