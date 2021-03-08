using System.Collections.Generic;
using System.IO;
using System.Linq;
using SecureWiki.MediaWiki;

namespace SecureWiki.Cryptography
{
    public class CacheManager
    {
        private Dictionary<string, CacheEntry> _dict = new();
        private string _dirpath;

        public CacheManager()
        {
            var cachePath = "RevisionCache";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            _dirpath = Path.Combine(projectDir, cachePath);
            Directory.CreateDirectory(_dirpath);
        }

        public void AddEntry(string pageTitle, Revision rev)
        {
            CacheEntry ce;
            if (!_dict.ContainsKey(pageTitle))
            {
                ce = new(_dirpath, pageTitle);                
            }
            else
            {
                ce = _dict[pageTitle];
            }
            
            ce.AddEntry(rev.revisionID, rev.content);
        }

        public string? GetFilePath(string pageTitle, string revid)
        {
            return _dict[pageTitle].GetFilePath(revid);
        }
    }
}

