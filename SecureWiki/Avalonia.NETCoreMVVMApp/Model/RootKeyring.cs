using Newtonsoft.Json;

namespace SecureWiki.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RootKeyring : Keyring
    {

        [JsonProperty] public string rand;
        
        public RootKeyring()
        {
            name = "Root";
            isChecked = false;
            rand = "I am a master keyring";
        }
        
        
        
    }
}
