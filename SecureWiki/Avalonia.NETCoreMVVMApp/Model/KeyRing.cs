using System.Collections.Generic;

namespace SecureWiki.Model
{
    public class KeyRing
    {
        public string name { get; set; }
        public List<KeyRing> keyRings { get; set; }
        public List<DataFile> dataFiles { get; set; }
    }
}