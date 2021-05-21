using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Masterkey
    {
        [JsonProperty] public string pageName;
        [JsonProperty] public byte[] symmKey;
        
        public Masterkey() {}
    }
}