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
            // var currentDir = Directory.GetCurrentDirectory();
            // var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../TestData"));
            // _dirpath = Path.Combine(projectDir, cachePath);
            // _cacheManager = new CacheManager();
        }
        
        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void TestAddEntry()
        {
            // var pageTitle = RandomString.GenerateRandomAlphanumericString();
            // Revision rev = new Revision() {content = "testContent", revisionID = "100"};
            // _cacheManager.AddEntry(pageTitle, rev);
        }

        [Test]
        public void TestCleanCacheDirectory()
        {
        }

        [Test]
        public void TestGetLatestRevisionID()
        {
        }
    }
}