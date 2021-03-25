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
            const string cachePath = "RevisionCache";
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../.."));
            _dirpath = Path.Combine(projectDir, cachePath);
            Directory.CreateDirectory(_dirpath);
        }

        public void AddEntry(string pageTitle, Revision rev)
        {
            // Get correct CacheEntry, or create a new if one isn't found
            CacheEntry ce;
            if (!_dict.ContainsKey(pageTitle))
            {
                ce = new(_dirpath, pageTitle);  
                _dict.Add(pageTitle, ce);
            }
            else
            {
                ce = _dict[pageTitle];
            }

            // Verify properties relevant to new CacheEntry
            if (rev.revisionID == null || rev.content == null)
            {
                Console.WriteLine("CacheManager:- AddEntry: input revision has null properties, " +
                                  "revisionID='{0}', content='{1}'", rev.revisionID, rev.content);
                return;
            }
            
            ce.AddEntry(rev.revisionID, rev.content);
        }

        // Return path to the latest entry in the cache (for that page title)
        public string? GetFilePath(string pageTitle)
        {
            string? latestRevId = GetLatestRevisionID(pageTitle);

            if (latestRevId == null)
            {
                return null;
            }

            return GetFilePath(pageTitle, latestRevId);
        }
        
        // Return path to the specified entry in the cache, if it exists
        public string? GetFilePath(string pageTitle, string revid)
        {
            if (_dict.ContainsKey(pageTitle) == false)
            {
                Console.WriteLine("GetFilePath:- Dict does not contain key '{0}', printing info", pageTitle);
                PrintInfo();
                return null;
            }
            
            var output = _dict[pageTitle].GetFilePath(revid);

            return output;
        }

        // Clean cache directory based on the passed CachePreferences, i.e. KeepAll, KeepNone, KeepLatest
        // Default is to remove all entries in the cache that is not the latest entry for a page title
        public void CleanCacheDirectory(CachePreferences preferences)
        {
            // Files that should be kept
            List<string> exemptionList = new(); 
            
            foreach (var item in _dict)
            {
                // call CleanCacheEntry based on own settings if set, general setting otherwise
                List<string> cleanedList = 
                    item.Value.CleanCacheEntry(preferences.ExceptionDictionary.ContainsKey(item.Key) ? 
                        preferences.ExceptionDictionary[item.Key] : preferences.GeneralSetting);

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


            // foreach (var item in exemptionList)
            // {
            //     Console.WriteLine("exemptionList: " + item);
            // }
            //
            // foreach (var item in dirFileList)
            // {
            //     Console.WriteLine("dirFileList: " + item);
            // }
            //
            // foreach (var item in toBeDeleted)
            // {
            //     Console.WriteLine("toBeDeleted: " + item);
            // }
            
            // Delete the rest
            foreach (var file in toBeDeleted)
            {
                File.Delete(file);
            }
        }

        public string? GetLatestRevisionID(string pageTitle)
        {
            if (_dict.ContainsKey(pageTitle) == false)
            {
                Console.WriteLine("GetFilePath:- Dict does not contain key '{0}'", pageTitle);
                PrintInfo();
                return null;
            }
            
            var latestRevId = _dict[pageTitle]?.GetLatestRevID();

            return latestRevId;
        }

        public void PrintInfo()
        {
            Console.WriteLine("CacheManager:- PrintInfo():");
            foreach (var item in _dict)
            {
                Console.WriteLine("item.Key='{0}', item.Value='{1}'", item.Key, item.Value);
                item.Value.PrintInfo();
            }
        }
    }
}