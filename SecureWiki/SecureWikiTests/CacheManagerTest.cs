using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NUnit.Framework;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Utilities;

namespace SecureWikiTests
{
    public class CacheManagerTests
    {
        private CacheManager _cacheManager;
        private Dictionary<string, CacheEntry> _dict = new();
        private string _dirpath;
        private const string cachePath = "RevisionCache";

        
        [SetUp]
        public void Setup()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../TestData"));
            _dirpath = Path.Combine(projectDir, cachePath);
            _cacheManager = new CacheManager();
        }
        
        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void TestAddEntryAndGetLatestRevision()
        {
            var pageTitle = RandomString.GenerateRandomAlphanumericString();
            var revid = "100";
            var content = "testContent";
            _cacheManager.AddEntry(pageTitle, revid, content);
            
            Assert.AreEqual(_cacheManager.GetLatestRevisionID(pageTitle), "100");
            
            
        }

        [Test]
        public void TestCleanCacheDirectory()
        {
        }
        
    }
}