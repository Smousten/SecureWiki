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
        public byte[] signedPrivateKey { get; set; }
        [JsonProperty]
        public byte[] signedPublicKey { get; set; }
        [JsonProperty]
        public string revisionStart { get; set; }
        [JsonProperty]
        public string revisionEnd { get; set; }

        public DataFileKey()
        {
            Crypto crypto = new();
            var (newSymmKey, newIV) = crypto.GenerateAESParams();
            var (newPrivateKey, newPublicKey) = crypto.GenerateRSAParams();
            
            symmKey = newSymmKey;
            iv = newIV;
            privateKey = newPrivateKey;
            publicKey = newPublicKey;
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

        public void SignKey(byte[] key)
        {
            Crypto crypto = new();
            if (privateKey != null)
            {
                signedPrivateKey = crypto.SignData(key, privateKey);
                signedPublicKey = crypto.SignData(key, publicKey);    
            }
        }
    }
}