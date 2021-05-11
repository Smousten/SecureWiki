using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Masterkey
    {
        [JsonProperty] public string pageTitle;
        [JsonProperty] public byte[] symmKey;
        
        public Masterkey() {}
    }
}