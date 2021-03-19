using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                _dict.Add(pageTitle, ce);
            }
            else
            {
                ce = _dict[pageTitle];
            }
            
            ce.AddEntry(rev.revisionID, rev.content);
        }

        public string? GetFilePath(string pageTitle)
        {
            if (_dict.ContainsKey(pageTitle) == false)
            {
                Console.WriteLine("GetFilePath:- Dict does not contain key '{0}'", pageTitle);
                PrintInfo();
                return null;
            }
            
            var latestRevId = _dict[pageTitle]?.GetLatestRevID();

            if (latestRevId == null)
            {
                return null;
            }

            return GetFilePath(pageTitle, latestRevId);
        }
        
        public string? GetFilePath(string pageTitle, string revid)
        {
            if (_dict.ContainsKey(pageTitle) == false)
            {
                Console.WriteLine("GetFilePath:- Dict does not contain key '{0}'", pageTitle);
                PrintInfo();
                return null;
            }
            
            var output = _dict[pageTitle].GetFilePath(revid);
            // Console.WriteLine("CacheManager:- GetFilePath: output='{0}'", output);

            return output;
        }

        public void CleanCacheDirectory()
        {
            Console.WriteLine("CleanCacheDirectory entered");
            List<string> exeptionList = new();

            string? revid;
            string? entryName;
            
            foreach (var item in _dict)
            {
                item.Value.RemoveAllButLatestEntry();
                revid = item.Value.GetLatestRevID();

                if (revid != null)
                {
                    entryName = item.Value.GetFilePath(revid);

                    if (entryName != null)
                    {
                        string filename = Path.GetFileName(entryName);
                        exeptionList.Add(filename);
                        // Console.WriteLine("adding '{0}' to execptionList", filename);
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

            string[] pathArray = Directory.GetFiles(_dirpath);

            foreach (string filepath in pathArray)
            {
                var filename = new FileInfo(filepath).Name;

                bool shouldBeKept = false;
                
                foreach (string exception in exeptionList)
                {
                    if (filename.Equals(exception))
                    {
                        shouldBeKept = true;
                        // Console.WriteLine("File '{0}' should be kept", filename);
                        break;
                    }
                }

                if (!shouldBeKept)
                {
                    File.Delete(filepath);
                    // Console.WriteLine("File '{0}' should not be kept", filename);
                }
            }
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