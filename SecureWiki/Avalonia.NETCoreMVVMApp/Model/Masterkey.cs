using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Masterkey
    {
        // Dictionary with server link as key and (pagetitle, masterkey) as value 
        [JsonProperty]
        public Dictionary<string, (string, byte[])> Dictionary { get; set; }
        
        [JsonProperty] public string mappingPage { get; set; }
        
        public Masterkey() {}
    }
}