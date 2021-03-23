using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SecureWiki.Cryptography
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CacheEntry
    {
        // <revid, filename>
        [JsonProperty]
        private Dictionary<string, string> _dict = new();
        [JsonProperty]
        private string _dirPath;
        [JsonProperty]
        private string _pageTitle;

        public CacheEntry(string dirPath, string pageTitle)
        {
            _dirPath = dirPath;
            this._pageTitle = pageTitle;
        }

        public void AddEntry(string revid, string content)
        {
            if (DictContainsKey(revid))
            {
                return;
            }
            
            var hash = new RandomString().ComputeHash(_pageTitle + revid);
            var path = Path.Combine(_dirPath, hash);

            File.WriteAllText(path, content);
                    
            _dict.Add(revid, hash);
        }

        public bool DictContainsKey(string revid)
        {
            return _dict.ContainsKey(revid);
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

        public void RemoveAllButLatestEntry()
        {
            string? latestRev = GetLatestRevID();

            // Create copy of dictionary for local use
            var dict = new Dictionary<string, string>();
            foreach (var entry in _dict)
            {
                dict.Add(entry.Key, entry.Value);
            }

            // Iterate through copy and remove all but latest revision
            foreach (var item in dict)
            {
                if (item.Key.Equals(latestRev))
                {
                    continue;
                }
                
                RemoveEntry(item.Key);
            }
        }

        // Return file path corresponding to specific revision id
        public string? GetFilePath(string revid)
        {
            if (DictContainsKey(revid) == false)
            {
                return null;
            }

            string path = Path.Combine(_dirPath, _dict[revid]);

            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                Console.WriteLine("File did not exist, removing cache entry");
                RemoveEntry(revid);
                return null;
            }
        }

        public string? GetLatestRevID()
        {
            int highestIDint = -1;

            foreach (var item in _dict)
            {
                try
                {
                    int itemKey = Int32.Parse(item.Key);

                    if (itemKey > highestIDint)
                    {
                        highestIDint = itemKey;
                    }
                }
                catch (FormatException e)
                {
                    Console.WriteLine("ERROR:- GetLatestRevId(): Could not parse item.Key='{0}' to Int32", item.Key);
                    Console.WriteLine(e.Message);
                }
            }

            if (highestIDint == -1)
            {
                return null;
            }

            return highestIDint.ToString();
        }

        private void DeleteCacheFile(string filename)
        {
            var path = Path.Combine(_dirPath, filename);
            File.Delete(path);
        }

        public void PrintInfo()
        {
            foreach (var item in _dict)
            {
                Console.WriteLine("item.Key='{0}', item.Value='{1}'", item.Key, item.Value);
            }
        }
    }
}