using System.Collections.ObjectModel;

namespace SecureWiki.Model
{
    public class RootKeyring
    {
        public string name { get; set; }

        public ObservableCollection<KeyringEntry> keyrings { get; set; } 
            = new();
        public ObservableCollection<DataFile> dataFiles { get; set; } 
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
                foreach (DataFile entry in dataFiles)
                {
                    output.Add(entry);
                }
                
                return output;
            }   
        }
    }
}