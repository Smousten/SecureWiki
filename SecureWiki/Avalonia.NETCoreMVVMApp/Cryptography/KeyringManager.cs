using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWiki.Cryptography
{
    public class KeyringManager
    {
        private readonly MasterKeyring _masterKeyring;
        private readonly Manager _manager;
        private DateTime _rootKeyringWriteTimestamp;
        public Masterkey masterKey { get; set; }

        public KeyringManager(MasterKeyring rk, Manager manager)
        {
            _masterKeyring = rk;
            _manager = manager;
        }
        
        public void RevokeAccess(AccessFile accessFile, string latestRevisionID)
        {
            // If no revisions are known to exist for current latest key
            // if (accessFile.keyList.Last().RevisionStart.Equals("-1")) return;
            
            // Set end revision for current latest key
            accessFile.keyList.Last().RevisionEnd = latestRevisionID;
            
            // Create
            AccessFileKey newAccessFileKey = new(accessFile.ownerPrivateKey!);
            accessFile.keyList.Add(newAccessFileKey);
        }

        public Dictionary<AccessFile, AccessFile> PrepareForExport(List<(AccessFile, bool)> inputList)
        {
            var dict = new Dictionary<AccessFile, AccessFile>();
            
            foreach (var (accessFile, isCheckedWrite) in inputList)
            {
                if (accessFile == null)
                {
                    Console.WriteLine("symmRef.targetAccessFile == null, symmRef.targetPageName = " 
                                      + accessFile.SymmetricReferenceToSelf.targetPageName);
                    continue;
                }
                var afCopy = accessFile.Copy();
                afCopy.PrepareForExport(isCheckedWrite);
                dict.Add(accessFile, afCopy);
            }

            return dict;
        }

        public Dictionary<Contact, List<AccessFile>> AddContactsToAccessFilesInBulk(List<(AccessFile, bool)> accessFiles, List<Contact> contacts)
        {
            var dict = new Dictionary<Contact, List<AccessFile>>();

            foreach (var contact in contacts)
            {
                dict.Add(contact, new List<AccessFile>());
            }
            
            foreach (var (af, isCheckedWrite) in accessFiles)
            {
                foreach (var contact in contacts)
                {
                    var inboxReference = af.inboxReferences.FirstOrDefault(c => c.HasSameStaticProperties(contact.InboxReference));

                    if (inboxReference == null)
                    {
                        var copy = contact.InboxReference.Copy(isCheckedWrite
                            ? InboxReference.AccessLevel.ReadWrite
                            : InboxReference.AccessLevel.Read);
                        copy.targetPageName = contact.InboxReference.targetPageName;
                        af.inboxReferences.Add(copy);
                        af.HasBeenChanged = true;
                        dict[contact].Add(af);
                    }
                    else
                    {
                        if (inboxReference.accessLevel == InboxReference.AccessLevel.Read && isCheckedWrite)
                        {
                            inboxReference.accessLevel = InboxReference.AccessLevel.ReadWrite;
                            dict[contact].Add(af);
                        }
                    }
                }
            }

            return dict;
        }

        // Create new access file and connect it to fresh references
        public void CreateAccessFileAndReferences(string pageNameTarget, string pageNameAccessFile, 
            string serverLink, PageType type, out SymmetricReference symmetricReference, 
            out AccessFile accessFile)
        {
            // Create access file and reference
            accessFile = new AccessFile(serverLink, pageNameTarget, type); 
            
            // Create symmetric reference to access file
            symmetricReference = new SymmetricReference(pageNameAccessFile,
                serverLink, type, pageNameTarget, accessFile);
            accessFile.SymmetricReferenceToSelf = symmetricReference;
        }

        public Keyring? CreateNewKeyring(string name, string serverLink)
        {
            var pageNameKeyring = _manager.GetFreshPageName();
            var pageNameAccessFile = _manager.GetFreshPageName();
            var pageNameInboxPage = _manager.GetFreshPageName();
            
            CreateAccessFileAndReferences(pageNameKeyring, pageNameAccessFile, serverLink, PageType.Keyring, 
                out SymmetricReference symmetricReference,
                out AccessFile accessFile);
            
            // Create new keyring object
            var keyring = new Keyring(accessFile.AccessFileReference, name);
            
            // Create inbox reference to inbox page
            InboxReference inboxReference = new(pageNameInboxPage, serverLink, InboxReference.AccessLevel.ReadWrite);

            var contact = new OwnContact("unnamed", inboxReference);

            keyring.OwnContact = contact;
            
            return keyring;
        }
        
        // Add symmetric reference to newEntries keyring
        public Keyring AddToDefaultKeyring(SymmetricReference symmetricReference)
        {
            Console.WriteLine("AddToDefaultKeyring entered");
            // AccessFile? accessFile;
            
            // Check if default Keyring already exists
            var symmRefToDefaultKeyring = _masterKeyring.SymmetricReferences.FirstOrDefault(
                e => e.type == PageType.Keyring 
                     && e.targetAccessFile?.AccessFileReference?.KeyringTarget!.name.Equals("newEntries") == true);
            var defaultKeyring = symmRefToDefaultKeyring?.targetAccessFile?.AccessFileReference?.KeyringTarget;
            
            // If no such keyring already exists
            if (defaultKeyring == null)
            {
                Console.WriteLine("defaultkeyring is null");
                defaultKeyring = CreateNewKeyring("newEntries", _manager.configManager.DefaultServerLink);
                _masterKeyring.AddSymmetricReference(defaultKeyring.accessFileReferenceToSelf.AccessFileParent.SymmetricReferenceToSelf);
            }
            // else
            // {
            //     Console.WriteLine("defaultkeyring is not null");
            //     if (defaultKeyring.accessFileReferenceToSelf == null)
            //     {
            //         Console.WriteLine("defaultKeyring.accessFileReferenceToSelf is null");
            //         Console.WriteLine("defaultKeyring.name = " + defaultKeyring.name);
            //     }
            //     if (defaultKeyring.accessFileReferenceToSelf?.AccessFileParent == null)
            //     {
            //         Console.WriteLine("defaultKeyring.accessFileReferenceToSelf?.AccessFileParent is null");
            //         Console.WriteLine("defaultKeyring.name = " + defaultKeyring.name);
            //     }
            //     accessFile = defaultKeyring.accessFileReferenceToSelf.AccessFileParent;
            // }
            //
            // if (accessFile == null)
            // {
            //     Console.WriteLine("accessFile is null");
            // }

            defaultKeyring.AddSymmetricReference(symmetricReference);
            return defaultKeyring;
        }
    }
}