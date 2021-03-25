namespace SecureWiki.FuseCommunication
{
    public interface IFuseInteraction
    {
        // Received create operation from FUSE
        // Should add new file to keyring json file
        void Create(string filename, string filepath);

        // Received read operation from FUSE
        // Should return byte[] stored on server or in cache
        void Read(string filename, string filepath);
        
        // Received write operation from FUSE
        // Should upload new version to server
        void Write(string filename, string filepath);

        // Received rename operation from FUSE
        // Should update keyring json file to reflect rename
        // Alternatively, used to delete (new path contains .Trash) and upload (old path contains .goutputstream)
        void Rename(string filename, string[] filepaths);

        // Received mkdir operation from FUSE
        // Should make new keyring object in keyring json file
        void Mkdir(string filename, string filepath);
        
        // Run Thread
        void RunListener();
    }
}