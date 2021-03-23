using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SecureWiki.MediaWiki;

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

        // Remove all entries in the cache that is not the latest entry for a page title
        public void CleanCacheDirectory()
        {
            // Find all latest entries
            List<string> exemptionList = new();
            foreach (var item in _dict)
            {
                item.Value.RemoveAllButLatestEntry();
                var revid = item.Value.GetLatestRevID();

                if (revid != null)
                {
                    var entryName = item.Value.GetFilePath(revid);

                    if (entryName != null)
                    {
                        string filename = Path.GetFileName(entryName);
                        exemptionList.Add(filename);
                    }
                    else
                    {
                        Console.WriteLine("entry name was null");
                    }
                }
                else
                {
                    Console.WriteLine("revid was null");
                }
            }

            // Get all files in cache directory
            string[] pathArray = Directory.GetFiles(_dirpath);

            // Check if file is in exemption list, delete otherwise
            foreach (string filepath in pathArray)
            {
                var filename = new FileInfo(filepath).Name;

                bool shouldBeKept = false;
                
                foreach (string exception in exemptionList)
                {
                    if (filename.Equals(exception))
                    {
                        shouldBeKept = true;
                        break;
                    }
                }

                if (!shouldBeKept)
                {
                    File.Delete(filepath);
                }
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