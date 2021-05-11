using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AccessFileKey
    {
        [JsonProperty]
        public byte[] SymmKey { get; set; }
        [JsonProperty]
        public byte[]? PrivateKey { get; set; }
        [JsonProperty]
        public byte[] PublicKey { get; set; }
        [JsonProperty]
        public byte[]? SignedWriteKey { get; set; }
        [JsonProperty]
        public byte[] SignedReadKeys { get; set; }
        [JsonProperty]
        public string RevisionStart { get; set; }
        [JsonProperty]
        public string RevisionEnd { get; set; }
        
        public AccessFileKey() {}

        public AccessFileKey(byte[] ownerPrivateKey)
        {
            // Generate new symmetric key and new asymmetric key pair
            var newSymmKey = Crypto.GenerateSymmKey();
            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            
            SymmKey = newSymmKey;
            PrivateKey = newPrivateKey;
            PublicKey = newPublicKey;
            RevisionStart = "-1";
            RevisionEnd = "-1";
            
            // Sign private and public key with given owner private key
            SignedWriteKey = Crypto.SignData(ownerPrivateKey, PrivateKey!);
            SignedReadKeys = Crypto.SignData(ownerPrivateKey, 
                Utilities.ByteArrayCombiner.Combine(SymmKey,PublicKey));
        }

        public AccessFileKey(byte[] symmKey, byte[] iv, 
            byte[]? privateKey, byte[] publicKey, byte[]? signedWriteKey, byte[] signedReadKeys,
            string revisionStart, string revisionEnd)
        {
            SymmKey = symmKey;
            PrivateKey = privateKey;
            PublicKey = publicKey;
            SignedWriteKey = signedWriteKey;
            SignedReadKeys = signedReadKeys;
            RevisionStart = revisionStart;
            RevisionEnd = revisionEnd;
        }

        public bool MergeWithOtherKey(AccessFileKey otherAFKey)
        {
            if (!SymmKey.SequenceEqual(otherAFKey.SymmKey) ||
                // !iv.SequenceEqual(otherAFKey.iv) ||
                !PublicKey.SequenceEqual(otherAFKey.PublicKey) ||
                (PrivateKey != null && otherAFKey.PrivateKey != null && !PrivateKey.SequenceEqual(otherAFKey.PrivateKey))
                )
            {
                return false;
            }

            PrivateKey ??= otherAFKey.PrivateKey;

            var revStart = int.Parse(RevisionStart);
            var revEnd = int.Parse(RevisionEnd);
            var revStartOther = int.Parse(otherAFKey.RevisionStart);
            var revEndOther = int.Parse(otherAFKey.RevisionEnd);

            if (revStart > revStartOther)
            {
                RevisionStart = otherAFKey.RevisionStart;
            }

            if (revEnd < revEndOther)
            {
                RevisionEnd = otherAFKey.RevisionEnd;
            }

            return true;
        }
    }
}