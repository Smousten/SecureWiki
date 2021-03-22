using Newtonsoft.Json;
using SecureWiki.Cryptography;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DataFileKey
    {
        [JsonProperty]
        public byte[] symmKey { get; set; }
        [JsonProperty]
        public byte[] iv { get; set; }
        [JsonProperty]
        public byte[]? privateKey { get; set; }
        [JsonProperty]
        public byte[] publicKey { get; set; }
        [JsonProperty]
        public string revisionStart { get; set; }
        [JsonProperty]
        public string revisionEnd { get; set; }

        public DataFileKey()
        {
            Crypto crypto = new();
            var (newSymmKey, newIV) = crypto.GenerateAESParams();
            var (newPrivateKey, newPublickey) = crypto.GenerateRSAParams();
            
            symmKey = newSymmKey;
            iv = newIV;
            privateKey = newPrivateKey;
            publicKey = newPublickey;
            revisionStart = "-1";
            revisionEnd = "-1";
        }

        public DataFileKey(byte[] symmKey, byte[] iv, byte[]? privateKey, byte[] publicKey, string revisionStart, string revisionEnd)
        {
            this.symmKey = symmKey;
            this.iv = iv;
            this.privateKey = privateKey;
            this.publicKey = publicKey;
            this.revisionStart = revisionStart;
            this.revisionEnd = revisionEnd;
        }
    }
}