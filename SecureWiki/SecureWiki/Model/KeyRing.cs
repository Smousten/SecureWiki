namespace SecureWiki.Model
{
    public class KeyRing
    {
        public string name { get; set; }
        public KeyRing[] keyRings { get; set; }
        public DataFile[] dataFiles { get; set; }
    }
}