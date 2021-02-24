using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SecureWiki.Model
{
    public class KeyringEntry
    {
        public string name { get; set; }
        // public List<KeyringEntry> keyRings { get; set; }
        public ObservableCollection<KeyringEntry> keyrings { get; set; }
        public List<DataFileEntry> dataFiles { get; set; }

        public ObservableCollection<object> combinedList
        {
            get
            {
                var output = new ObservableCollection<object>();

                foreach (KeyringEntry entry in keyrings)
                {
                    output.Add(entry);
                }
                foreach (DataFileEntry entry in dataFiles)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }
    }
}