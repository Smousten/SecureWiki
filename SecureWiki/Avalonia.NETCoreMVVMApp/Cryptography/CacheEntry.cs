using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.Utilities;

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
            _pageTitle = pageTitle;
        }

        public void AddEntry(string revid, string content)
        {
            if (DictContainsKey(revid))
            {
                return;
            }

            // Calculate filename
            var hash = RandomString.ComputeHash(_pageTitle + revid);
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
        
        // Delete cache file and its reference
        public void RemoveEntry(string revid)
        {
            DeleteCacheFile(_dict[revid]);
            _dict.Remove(revid);
        }

        public void RemoveAllEntries()
        {
            // Create copy of dictionary for local use
            var dict = new Dictionary<string, string>();
            foreach (var entry in _dict)
            {
                dict.Add(entry.Key, entry.Value);
            }

            // Iterate through copy and remove all
            foreach (var item in dict)
            {
                RemoveEntry(item.Key);
            }
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

            // Return file if it exists, remove reference otherwise
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
        
        // Delete unwanted cache files and return list of files to be kept
        public List<string> CleanCacheEntry(CachePreferences.CacheSetting setting)
        {
            List<string> exemptionList = new();

            switch (setting)
            {
                case CachePreferences.CacheSetting.KeepAll:
                    exemptionList.AddRange(_dict.Select(item => item.Value));
                    break;
                case CachePreferences.CacheSetting.KeepLatest:
                    var latestRev = GetLatestRevID();
                    if (latestRev != null)
                    {
                        exemptionList.Add(_dict[latestRev]);                        
                    }
                    RemoveAllButLatestEntry();
                    break;
                case CachePreferences.CacheSetting.KeepNone:
                    RemoveAllEntries();
                    break;
                default:
                    Console.WriteLine("CacheManager:- CleanCacheDirectory: CacheSetting not recognized");
                    throw new ArgumentOutOfRangeException();
            }

            return exemptionList;
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