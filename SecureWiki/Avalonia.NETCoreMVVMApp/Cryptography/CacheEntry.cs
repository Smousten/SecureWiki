using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SecureWiki.MediaWiki;

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
        private string pageTitle;

        public CacheEntry(string dirPath, string pageTitle)
        {
            _dirPath = dirPath;
            this.pageTitle = pageTitle;
        }

        public void AddEntry(string revid, string content)
        {
            if (DictContainsKey(revid))
            {
                return;
            }
            
            var hash = new RandomString().ComputeHash(pageTitle + revid);
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

        public string? GetFilePath(string revid)
        {
            if (DictContainsKey(revid) == false)
            {
                return null;
            }
            
            return Path.Combine(_dirPath, _dict[revid]);
        }

        public string? GetLatestRevID()
        {
            int highestIDint = -1;

            foreach (var item in _dict)
            {
                Console.WriteLine("GetLatestRevID:- Checking item.Key='{0}'", item.Key);
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

            Console.WriteLine("Returning '{0}'", highestIDint.ToString());
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