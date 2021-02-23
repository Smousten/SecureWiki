using System.Collections.Generic;

namespace SecureWiki.Model
{
    public class KeyringEntry
    {
        public string name { get; set; }
        public List<KeyringEntry> keyrings { get; set; }
        public List<DataFileEntry> dataFiles { get; set; }
    }
}