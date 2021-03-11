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