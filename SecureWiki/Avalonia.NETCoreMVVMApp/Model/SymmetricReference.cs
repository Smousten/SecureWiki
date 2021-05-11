namespace SecureWiki.Model
{
    public class SymmetricReference
    {
        public string reference;
        public byte[] symmKey;
        public Type type;


        public SymmetricReference(string reference, byte[] symmKey)
        {
            this.reference = reference;
            this.symmKey = symmKey;
        }
    }

    public enum Type
    {
        AccessFile,
        Keyring
    }
}