using System.Collections.Generic;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Contact
    {
        [JsonProperty] public byte[] PublicKey { get; set; }
        [JsonProperty] public byte[]? PrivateKey { get; set; }
        [JsonProperty] public string ServerLink { get; set; }
        [JsonProperty] public string PageTitle { get; set; }
        [JsonProperty] public string Nickname { get; set; }

        public Contact()
        {
        }

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
        
    }
    
    [JsonObject(MemberSerialization.OptIn)]
    public class ContactManager
    {
        [JsonProperty] public List<Contact> OwnContacts;
        [JsonProperty] public List<Contact> Contacts;

        public ContactManager()
        {
            OwnContacts = new List<Contact>();
            Contacts = new List<Contact>();
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
        
    }
}