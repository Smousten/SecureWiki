using System.Collections.ObjectModel;

namespace SecureWiki.Model
{
    public class RootKeyring
    {
        public string name { get; set; }

        public ObservableCollection<KeyringEntry> keyrings { get; set; } 
            = new();
        public ObservableCollection<DataFileEntry> dataFiles { get; set; } 
            = new();
        
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