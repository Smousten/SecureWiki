using System.Collections.Generic;
using Newtonsoft.Json;

namespace SecureWiki.Utilities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigManager
    {
        [JsonProperty] 
        public Dictionary<string, ConfigEntry> ConfigDictionary;

        [JsonProperty]
        public CachePreferences? cachePreferences;
        
        public ConfigManager(CachePreferences.CacheSetting cacheSetting = CachePreferences.CacheSetting.KeepNewest)
        {
            ConfigDictionary = new Dictionary<string, ConfigEntry>();
            cachePreferences = new CachePreferences(cacheSetting);
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

        public CachePreferences.CacheSetting? GetSetting(string pageTitle)
        {
            return cachePreferences?.GetSetting(pageTitle);
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
            KeepNewest,
            KeepNone
        }

        [JsonProperty] public CacheSetting GeneralSetting;
        [JsonProperty] public Dictionary<string, CacheSetting> ExceptionDictionary;

        public CachePreferences(CacheSetting setting = CacheSetting.KeepNewest)
        {
            GeneralSetting = setting;
            ExceptionDictionary = new Dictionary<string, CacheSetting>();
        }

        public CacheSetting GetSetting(string pageTitle)
        {
            return ExceptionDictionary.ContainsKey(pageTitle) ? ExceptionDictionary[pageTitle] : GeneralSetting;
        }

        public void SetPreference(string pageTitle, CacheSetting setting)
        {
            if (ExceptionDictionary.ContainsKey(pageTitle))
            {
                ExceptionDictionary[pageTitle] = setting;
            }
            else
            {
                ExceptionDictionary.Add(pageTitle, setting);
            }
        }
        
    }
}