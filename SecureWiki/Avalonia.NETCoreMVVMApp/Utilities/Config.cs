using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ProtectedData = CrossPlatformProtectedData.ProtectedData;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigManager
    {
        [JsonProperty] public Dictionary<string, ConfigEntry> ConfigDictionary;

        [JsonProperty] public CachePreferences CachePreference;

        [JsonProperty] public string DefaultServerLink;
        
        // TODO: fix default server link
        public ConfigManager(CachePreferences.CacheSetting cacheSetting = CachePreferences.CacheSetting.KeepLatest, string defaultServerLink = "http://localhost/mediawiki/api.php")
        {
            ConfigDictionary = new Dictionary<string, ConfigEntry>();
            CachePreference = new CachePreferences(cacheSetting);
            DefaultServerLink = defaultServerLink;
        }

        // Add entry if it does not already exist, overwrite if new version contains username
        public void AddEntry(string serverLink, string? username = null, string? password = null)
        {
            if (!ConfigDictionary.ContainsKey(serverLink))
            {
                ConfigDictionary.Add(serverLink, new ConfigEntry(username, password));
            }
            else
            {
                if (username != null)
                {
                    ConfigDictionary[serverLink] = new ConfigEntry(username, password);
                }    
            }
        }

        // Remove key, value in the config dictionary 
        public void RemoveEntry(string serverLink)
        {
            if (ConfigDictionary.ContainsKey(serverLink))
            {
                Console.WriteLine("removing config entry: " + serverLink);
                ConfigDictionary.Remove(serverLink);
            }
        }

        public ConfigEntry? GetServerCredentials(string serverLink)
        {
            return ConfigDictionary.ContainsKey(serverLink) ? ConfigDictionary[serverLink] : null;
        }

        public CachePreferences.CacheSetting? GetSetting(string pageName)
        {
            return CachePreference.GetSettingOrDefault(pageName);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigEntry
    {
        [JsonProperty] 
        public string? Username;

        [JsonProperty] 
        public string? ProtectedPassword;
        
        [JsonProperty] 
        public string? Entropy;

        public ConfigEntry(string? username, string? password)
        {
            Username = username;
            if (password != null)
            {
                Entropy = GenerateEntropy();
                ProtectedPassword = Protect(password, Entropy);
            }
        }

        private string GenerateEntropy()
        {
            byte[] entropy = new byte[20];
            using(RNGCryptoServiceProvider rng = new())
            {
                rng.GetBytes(entropy);
            }

            return Convert.ToBase64String(entropy);
        }
        
        public static string? Protect(string data, string entropy)
        {
            try
            {
                // Encrypt the data using DataProtectionScope.CurrentUser. The result can be decrypted
                // only by the same current user.
                var dataBytes = Encoding.ASCII.GetBytes(data);
                var entropyBytes = Convert.FromBase64String(entropy);
                var protectedBytes = ProtectedData.Protect( dataBytes, entropyBytes, DataProtectionScope.CurrentUser );
                return Convert.ToBase64String(protectedBytes);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("Data was not encrypted. An error occurred.");
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public static string? Unprotect(string data, string entropy)
        {
            try
            {
                //Decrypt the data using DataProtectionScope.CurrentUser.
                var protectedBytes = Convert.FromBase64String(data);
                var entropyBytes = Convert.FromBase64String(entropy);
                var unprotectedBytes = ProtectedData.Unprotect(protectedBytes, entropyBytes, DataProtectionScope.CurrentUser );
                return Encoding.ASCII.GetString(unprotectedBytes);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("Data was not decrypted. An error occurred.");
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CachePreferences
    {
        // Specifies which revisions are kept in cache after shutdown
        public enum CacheSetting
        {
            KeepAll,
            KeepLatest,
            KeepNone
        }

        [JsonProperty] public CacheSetting GeneralSetting;
        [JsonProperty] public Dictionary<string, CacheSetting> ExceptionDictionary;

        public CachePreferences(CacheSetting setting = CacheSetting.KeepLatest)
        {
            GeneralSetting = setting;
            ExceptionDictionary = new Dictionary<string, CacheSetting>();
        }

        public CacheSetting GetSettingOrDefault(string pageName)
        {
            return ExceptionDictionary.ContainsKey(pageName) ? ExceptionDictionary[pageName] : GeneralSetting;
        }
        
        public CacheSetting? GetSetting(string pageName)
        {
            return ExceptionDictionary.ContainsKey(pageName) ? ExceptionDictionary[pageName] : null;
        }

        public void SetPreference(string pageName, CacheSetting? setting)
        {
            if (setting == null)
            {
                ExceptionDictionary.Remove(pageName);
                return;
            }
            
            if (ExceptionDictionary.ContainsKey(pageName))
            {
                ExceptionDictionary[pageName] = (CacheSetting) setting;
            }
            else
            {
                ExceptionDictionary.Add(pageName, (CacheSetting) setting);
            }
        }
        
    }
}