using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigManager
    {
        [JsonProperty] public Dictionary<string, ConfigEntry> ConfigDictionary;

        [JsonProperty] public CachePreferences cachePreferences;

        [JsonProperty] public string DefaultServerLink;
        
        public ConfigManager(CachePreferences.CacheSetting cacheSetting = CachePreferences.CacheSetting.KeepLatest, string defaultServerLink = "http://localhost/mediawiki/api.php")
        {
            ConfigDictionary = new Dictionary<string, ConfigEntry>();
            cachePreferences = new CachePreferences(cacheSetting);
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

        public ConfigEntry? GetServerCredentials(string serverLink)
        {
            if (ConfigDictionary.ContainsKey(serverLink))
            {
                return ConfigDictionary[serverLink];
            }
            else
            {
                return null;
            }
        }

        public CachePreferences.CacheSetting? GetSetting(string pageTitle)
        {
            return cachePreferences.GetSettingOrDefault(pageTitle);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigEntry
    {
        [JsonProperty] 
        public string? Username;
        [JsonProperty] 
        public string? Password;

        public ConfigEntry(string? username, string? password)
        {
            this.Username = username;
            this.Password = password;
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

        public CacheSetting GetSettingOrDefault(string pageTitle)
        {
            return ExceptionDictionary.ContainsKey(pageTitle) ? ExceptionDictionary[pageTitle] : GeneralSetting;
        }
        
        public CacheSetting? GetSetting(string pageTitle)
        {
            return ExceptionDictionary.ContainsKey(pageTitle) ? ExceptionDictionary[pageTitle] : null;
        }

        public void SetPreference(string pageTitle, CacheSetting? setting)
        {
            if (setting == null)
            {
                ExceptionDictionary.Remove(pageTitle);
                return;
            }
            
            if (ExceptionDictionary.ContainsKey(pageTitle))
            {
                ExceptionDictionary[pageTitle] = (CacheSetting) setting;
            }
            else
            {
                ExceptionDictionary.Add(pageTitle, (CacheSetting) setting);
            }
        }
        
    }
}