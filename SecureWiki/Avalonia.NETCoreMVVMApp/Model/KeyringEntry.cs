using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SecureWiki.Model
{
    public class KeyringEntry
    {
        public string name { get; set; }
        // public List<KeyringEntry> keyrings { get; set; }
        public ObservableCollection<KeyringEntry> keyrings { get; set; }
        public List<DataFileEntry> dataFiles { get; set; }
    }
}