using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DataFileKey
    {
        [JsonProperty]
        public byte[] SymmKey { get; set; }
        [JsonProperty]
        public byte[] IV { get; set; }
        [JsonProperty]
        public byte[]? PrivateKey { get; set; }
        [JsonProperty]
        public byte[] PublicKey { get; set; }
        [JsonProperty]
        public byte[] SignedPrivateKey { get; set; }
        [JsonProperty]
        public byte[] SignedPublicKey { get; set; }
        [JsonProperty]
        public string RevisionStart { get; set; }
        [JsonProperty]
        public string RevisionEnd { get; set; }

        public DataFileKey()
        {
            var (newSymmKey, newIV) = Crypto.GenerateAESParams();
            var (newPrivateKey, newPublicKey) = Crypto.GenerateRSAParams();
            
            SymmKey = newSymmKey;
            IV = newIV;
            PrivateKey = newPrivateKey;
            PublicKey = newPublicKey;
            RevisionStart = "-1";
            RevisionEnd = "-1";
        }

        public DataFileKey(byte[] symmKey, byte[] iv, byte[]? privateKey, byte[] publicKey, string revisionStart, string revisionEnd)
        {
            this.SymmKey = symmKey;
            this.IV = iv;
            this.PrivateKey = privateKey;
            this.PublicKey = publicKey;
            this.RevisionStart = revisionStart;
            this.RevisionEnd = revisionEnd;
        }

        public void SignKey(byte[] key)
        {
            if (PrivateKey != null)
            {
                SignedPrivateKey = Crypto.SignData(key, PrivateKey);
                SignedPublicKey = Crypto.SignData(key, PublicKey);    
            }
        }

        public bool MergeWithOtherKey(DataFileKey otherDFkey)
        {
            if (!SymmKey.SequenceEqual(otherDFkey.SymmKey) ||
                // !iv.SequenceEqual(otherDFkey.iv) ||
                !PublicKey.SequenceEqual(otherDFkey.PublicKey) ||
                (PrivateKey != null && otherDFkey.PrivateKey != null && !PrivateKey.SequenceEqual(otherDFkey.PrivateKey))
                )
            {
                return false;
            }

            PrivateKey ??= otherDFkey.PrivateKey;
            IV ??= otherDFkey.IV;

            var revStart = int.Parse(RevisionStart);
            var revEnd = int.Parse(RevisionEnd);
            var revStartOther = int.Parse(otherDFkey.RevisionStart);
            var revEndOther = int.Parse(otherDFkey.RevisionEnd);

            if (revStart > revStartOther)
            {
                RevisionStart = otherDFkey.RevisionStart;
            }

            if (revEnd < revEndOther)
            {
                RevisionEnd = otherDFkey.RevisionEnd;
            }

            return true;
        }
    }
}