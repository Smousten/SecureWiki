using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Keyring //: IReactiveObject
    {
        [JsonProperty(Order = -4)]
        public string name { get; set; }

        [JsonProperty(Order = 99)] public List<SymmetricReference> SymmetricReferences = new();
        [JsonProperty] public InboxReference InboxReferenceToSelf;
        public AccessFileReference accessFileReferenceToSelf;
        public bool HasBeenChanged = false;

        public Keyring? parent;
        
        public Keyring(AccessFileReference accessFileReferenceToSelf, string name = "unnamed")
        {
            this.name = name;
            this.accessFileReferenceToSelf = accessFileReferenceToSelf;
            accessFileReferenceToSelf.KeyringTarget = this;
            HasBeenChanged = true;
            var accessFileParent = this.accessFileReferenceToSelf.AccessFileParent;
            if (accessFileParent != null)
                accessFileParent.HasTargetBeenChanged = true;
        }
        
        public Keyring(string name = "unnamed")
        {
            this.name = name;
            }
        
        public Keyring()
        {
            name = "unnamed";
        }
        
        public bool IsValid()
        {
            // var properties = new List<PropertyInfo?>();
            var fields = new List<FieldInfo?>();

            // Properties to be checked
            // properties.Add(GetType().GetProperty(nameof(name)));

            // Fields to be checked
            fields.Add(typeof(Keyring).GetField(nameof(InboxReferenceToSelf)));
            fields.Add(typeof(Keyring).GetField(nameof(SymmetricReferences)));
            //
            // foreach (var val in properties.Select(prop => prop?.GetValue(this)))
            // {
            //     switch (val)
            //     {
            //         case null:
            //         case string {Length: < 2}:
            //         case byte[] {Length: < 2}:
            //             return false;
            //     }
            // }
            //
            if (fields.Any(field => field?.GetValue(this) == null))
            {
                return false;
            }

            if (SymmetricReferences.Any(symmRef => !symmRef.IsValid()))
            {
                return false;
            }

            // Check if reference is also valid
            return InboxReferenceToSelf.IsValid();
        }

        // Add a symmetric reference and update it accordingly
        public void AddSymmetricReference(SymmetricReference symmetricReference)
        {
            SymmetricReferences.Add(symmetricReference);
            symmetricReference.keyringParent = this;
        }
        
        // Remove a symmetric reference if it exists and update it accordingly
        public void AttemptRemoveSymmetricReference(SymmetricReference symmetricReference)
        {
            if (SymmetricReferences.Contains(symmetricReference))
            {
                symmetricReference.keyringParent = null;
                SymmetricReferences.Remove(symmetricReference);
            }
        }

        public InboxReference GetInboxReference(InboxReference.AccessLevel accessLevel)
        {
            return InboxReferenceToSelf.Copy(accessLevel);
        }
        
        public void CopyFromOtherKeyring(Keyring ke)
        {
            name = ke.name;
            InboxReferenceToSelf = ke.InboxReferenceToSelf;
            accessFileReferenceToSelf = ke.accessFileReferenceToSelf;
            parent = ke.parent;
            SymmetricReferences = ke.SymmetricReferences;
        }

        public List<SymmetricReference> GetAllAndDescendantSymmetricReferencesToGenericFiles(List<Keyring> visitedKeyrings)
        {
            var outputList = new List<SymmetricReference>();
            visitedKeyrings.Add(this);

            foreach (var symmRef in SymmetricReferences)
            {
                if (symmRef.type == PageType.GenericFile)
                {
                    outputList.Add(symmRef);
                }
                else
                {
                    var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                    if (kr == null)
                    {
                        Console.WriteLine("GetAllAndDescendantSymmetricReferencesToGenericFiles:- Keyring is null");
                        continue;
                    }
                    
                    if (visitedKeyrings.Contains(kr))
                    {
                        Console.WriteLine("keyring already visited, name = " + this.name);
                        continue;
                    }
                    var res = kr.GetAllAndDescendantSymmetricReferencesToGenericFiles(visitedKeyrings);
                    outputList.AddRange(res);
                }
            }

            return outputList;
        }

        public List<Keyring> GetAllAndDescendantKeyrings(List<Keyring> visitedKeyrings)
        {
            var outputList = new List<Keyring>();
            visitedKeyrings.Add(this);
        
            foreach (var symmRef in SymmetricReferences)
            {
                if (symmRef.type == PageType.Keyring)
                {
                    var kr = symmRef.targetAccessFile?.AccessFileReference?.KeyringTarget;
                    if (kr == null)
                    {
                        Console.WriteLine("GetAllAndDescendantSymmetricReferencesToGenericFiles:- Keyring is null");
                        continue;
                    }
                    outputList.Add(kr);
                    if (visitedKeyrings.Contains(kr))
                    {
                        Console.WriteLine("keyring already visited, name = " + this.name);
                        continue;
                    }
                    
                    var res = kr.GetAllAndDescendantKeyrings(visitedKeyrings);
                    outputList.AddRange(res);
                }
            }
        
            return outputList;
        }

        public (List<AccessFile>, List<Keyring>) GetAllChangedAccessFilesAndKeyrings(Keyring keyring)
        {
            var accessFileList = new List<AccessFile>();
            var keyringFileList = new List<Keyring>();

            foreach (var symmRef in keyring.SymmetricReferences)
            {
                if (symmRef.targetAccessFile is {HasBeenChanged: true})
                {
                    accessFileList.Add(symmRef.targetAccessFile);
                }

                if (symmRef.targetAccessFile is {HasTargetBeenChanged: true} && 
                    symmRef.targetAccessFile.AccessFileReference.KeyringTarget != null)
                {
                    keyringFileList.Add(symmRef.targetAccessFile.AccessFileReference.KeyringTarget);
                }

                if (symmRef.type == PageType.Keyring)
                {
                    if (symmRef.targetAccessFile?.AccessFileReference.KeyringTarget == null) continue;
                    var (resAccessFiles, resKeyrings) = GetAllChangedAccessFilesAndKeyrings(symmRef.targetAccessFile.AccessFileReference
                        .KeyringTarget);
                    accessFileList.AddRange(resAccessFiles);
                    keyringFileList.AddRange(resKeyrings);
                }
            }

            return (accessFileList, keyringFileList);
        }
    }
}