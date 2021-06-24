using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SecureWiki.MediaWiki;
using SecureWiki.Utilities;

namespace SecureWiki.Cryptography
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CacheManager
    {
        [JsonProperty]
        private Dictionary<string, CacheEntry> _dict = new();
        [JsonProperty]
        private string _dirpath;

        public CacheManager()
        {
            // Create cache directory if it does not already exist
            const string cachePath = "RevisionCache";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            _dirpath = Path.Combine(projectDir, cachePath);
            Directory.CreateDirectory(_dirpath);
        }

        public void AddEntry(string pageName, string revid, string content)
        {
            // Get correct CacheEntry, or create a new if one isn't found
            CacheEntry ce;
            if (!_dict.ContainsKey(pageName))
            {
                ce = new CacheEntry(_dirpath, pageName);  
                _dict.Add(pageName, ce);
            }
            else
            {
                ce = _dict[pageName];
            }

            // Verify properties relevant to new CacheEntry

            ce.AddEntry(revid, content);
        }

        // Return path to the latest entry in the cache (for that page title)
        public string? GetFilePath(string pageName)
        {
            string? latestRevId = GetLatestRevisionID(pageName);

            if (latestRevId == null)
            {
                return null;
            }

            return GetFilePath(pageName, latestRevId);
        }
        
        // Return path to the specified entry in the cache, if it exists
        public string? GetFilePath(string pageName, string revid)
        {
            if (_dict.ContainsKey(pageName) == false)
            {
                return null;
            }
            
            var output = _dict[pageName].GetFilePath(revid);

            return output;
        }

        // Clean cache directory based on the passed CachePreferences, i.e. KeepAll, KeepNone, KeepLatest
        // Default is to remove all entries in the cache that is not the latest entry for a page title
        public void CleanCacheDirectory(CachePreferences preferences)
        {
            // Files that should not be deleted
            List<string> exemptionList = new(); 
            
            foreach (var (key, cacheEntry) in _dict)
            {
                // call CleanCacheEntry based on own settings if set, general setting otherwise
                List<string> cleanedList = 
                    cacheEntry.CleanCacheEntry(preferences.ExceptionDictionary.ContainsKey(key) ? 
                        preferences.ExceptionDictionary[key] : preferences.GeneralSetting);

                exemptionList.AddRange(cleanedList);
            }

            // Get all files in cache directory
            var dirFileArray = Directory.GetFiles(_dirpath);
            List<string> dirFileList = new(); 
            foreach (var item in dirFileArray)
            {
                dirFileList.Add(Path.GetFileName(item));
            }

            // Keep the files in the exemption list
            var toBeDeleted = dirFileList.Except(exemptionList).ToList();
            
            // Delete the rest
            foreach (var file in toBeDeleted)
            {
                File.Delete(Path.Combine(_dirpath, file));
            }
        }

        // Get revision id of latest entry in cache belonging to the given page title, if one exists.
        public string? GetLatestRevisionID(string pageName)
        {
            if (_dict.ContainsKey(pageName) == false)
            {
                Console.WriteLine("GetLatestRevisionID:- Dict does not contain key '{0}'", pageName);
                return null;
            }
            
            var latestRevId = _dict[pageName].GetLatestRevID();

            return latestRevId;
        }

        public void PrintInfo()
        {
            Console.WriteLine("CacheManager:- PrintInfo():");
            foreach (var (key, cacheEntry) in _dict)
            {
                Console.WriteLine("item.Key='{0}', item.Value='{1}'", key, cacheEntry);
                cacheEntry.PrintInfo();
            }
        }
    }
}