using System.Collections.Generic;
using System.IO;
using SecureWiki.MediaWiki;

namespace SecureWiki.Cryptography
{
    public class CacheEntry
    {
        private Dictionary<string, string> _dict = new();
        private string _dirPath;
        private string pageTitle;

        public CacheEntry(string dirPath, string pageTitle)
        {
            _dirPath = dirPath;
            this.pageTitle = pageTitle;
        }

        public void AddEntry(string revid, string content)
        {
            var hash = new RandomString().ComputeHash(pageTitle + revid);
            var path = Path.Combine(_dirPath, hash);

            File.WriteAllText(path, content);
            
            _dict.Add(revid, hash);
        }
        
        public void AddExistingEntry(string revid, string filename)
        {
            _dict.Add(revid, filename);
        }
        
        public void RemoveEntry(string revid)
        {
            DeleteCacheFile(_dict[revid]);
            _dict.Remove(revid);
        }

        public string? GetFilePath(string revid)
        {
            return _dict[revid];
        }

        private void DeleteCacheFile(string filename)
        {
            var path = Path.Combine(_dirPath, filename);
            File.Delete(path);
        }
    }
}