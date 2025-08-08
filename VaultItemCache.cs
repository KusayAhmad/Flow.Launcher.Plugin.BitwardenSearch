using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using System.Linq;

namespace Flow.Launcher.Plugin.BitwardenSearch
{
    public class CachedVaultItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Username { get; set; }
        public bool HasTotp { get; set; }
        public List<string> Uris { get; set; } = new List<string>();
        public DateTime CacheTime { get; set; }
        public int Reprompt { get; set; }
    }

    public class VaultItemCache
    {
        private readonly string _cacheFilePath;
        private readonly object _cacheLock = new object();
        private Dictionary<string, CachedVaultItem> _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(48); // Increased from 24 hours for better performance

        public VaultItemCache(string cacheDirectory)
        {
            Directory.CreateDirectory(cacheDirectory);
            _cacheFilePath = Path.Combine(cacheDirectory, "vault_items_cache.json");
            _cache = new Dictionary<string, CachedVaultItem>();
            LoadCache();
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    _cache = JsonConvert.DeserializeObject<Dictionary<string, CachedVaultItem>>(json) 
                        ?? new Dictionary<string, CachedVaultItem>();
                    
                    // Clean expired entries during load
                    CleanExpiredEntries();
                }
                Logger.Log($"Loaded {_cache.Count} items from vault cache", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading vault cache", ex);
                _cache = new Dictionary<string, CachedVaultItem>();
            }
        }

        public void SaveCache()
        {
            lock (_cacheLock)
            {
                try
                {
                    CleanExpiredEntries();
                    var json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
                    File.WriteAllText(_cacheFilePath, json);
                    Logger.Log($"Saved {_cache.Count} items to vault cache", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error saving vault cache", ex);
                }
            }
        }

        private void CleanExpiredEntries()
        {
            var now = DateTime.Now;
            var expiredKeys = _cache
                .Where(kvp => (now - kvp.Value.CacheTime) > _cacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                Logger.Log($"Removed {expiredKeys.Count} expired entries from vault cache", LogLevel.Debug);
            }
        }

        public void UpdateCache(List<BitwardenItem> items)
        {
            lock (_cacheLock)
            {
                foreach (var item in items)
                {
                    var cachedItem = new CachedVaultItem
                    {
                        Id = item.id,
                        Name = item.name,
                        Username = item.login?.username,
                        HasTotp = item.hasTotp,
                        Uris = item.login?.uris?.Select(u => u.uri).ToList() ?? new List<string>(),
                        CacheTime = DateTime.Now,
                        Reprompt = item.reprompt
                    };
                    Logger.Log($"Caching item {item.name} with reprompt value: {item.reprompt}", LogLevel.Debug);
                    _cache[item.id] = cachedItem;
                }
                SaveCache();
            }
        }

        public List<BitwardenItem> SearchCache(string searchTerm)
        {
            lock (_cacheLock)
            {
                Logger.Log($"🔍 Searching vault cache for: '{searchTerm}'", LogLevel.Debug);
                CleanExpiredEntries();
                
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    Logger.Log("❌ Search term is empty", LogLevel.Debug);
                    return new List<BitwardenItem>();
                }
                
                var searchTermLower = searchTerm.ToLower();
                var searchWords = searchTermLower.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                Logger.Log($"🔍 Search words: [{string.Join(", ", searchWords)}]", LogLevel.Debug);
                
                var matchingItems = _cache
                    .Values
                    .Where(item =>
                    {
                        var itemName = item.Name.ToLower();
                        var itemUsername = item.Username?.ToLower() ?? "";
                        var itemUris = item.Uris.Select(uri => uri.ToLower()).ToList();
                        
                        // Check if all search words are contained in the item
                        var matches = searchWords.All(word =>
                            itemName.Contains(word) ||
                            itemUsername.Contains(word) ||
                            itemUris.Any(uri => uri.Contains(word))
                        );
                        
                        if (matches)
                        {
                            Logger.Log($"✅ Cache match: '{item.Name}'", LogLevel.Debug);
                        }
                        
                        return matches;
                    })
                    .OrderByDescending(item =>
                    {
                        // Score based on how well the search term matches
                        var score = 0;
                        var itemName = item.Name.ToLower();
                        var itemUsername = item.Username?.ToLower() ?? "";
                        
                        // Exact name match gets highest score
                        if (itemName.Equals(searchTermLower))
                            score += 100;
                        // Name starts with search term
                        else if (itemName.StartsWith(searchTermLower))
                            score += 50;
                        // Name contains search term
                        else if (itemName.Contains(searchTermLower))
                            score += 25;
                        
                        // Username matches
                        if (itemUsername.Contains(searchTermLower))
                            score += 10;
                        
                        return score;
                    })
                    .Select(item =>
                    {
                        Logger.Log($"Returning cached item {item.Name} with reprompt value: {item.Reprompt}", LogLevel.Debug);
                        return new BitwardenItem
                        {
                            id = item.Id,
                            name = item.Name,
                            hasTotp = item.HasTotp,
                            reprompt = item.Reprompt,
                            login = new BitwardenLogin
                            {
                                username = item.Username,
                                uris = item.Uris.Select(uri => new BitwardenUri { uri = uri }).ToList()
                            }
                        };
                    })
                    .ToList();
                
                Logger.Log($"🎯 Cache search complete: Found {matchingItems.Count} results for '{searchTerm}'", LogLevel.Debug);
                return matchingItems;
            }
        }

        public bool IsCacheValid()
        {
            lock (_cacheLock)
            {
                Logger.Log($"🔍 Checking vault cache validity. Cache count: {_cache.Count}", LogLevel.Debug);
                
                if (_cache.Count == 0) 
                {
                    Logger.Log("❌ Vault cache is empty", LogLevel.Debug);
                    return false;
                }

                // Check if any item is still valid (not expired)
                var now = DateTime.Now;
                var validItems = _cache.Count(kvp => (now - kvp.Value.CacheTime) <= _cacheExpiration);
                var isValid = validItems > 0;
                
                Logger.Log($"✅ Vault cache validity: {isValid} (valid items: {validItems}/{_cache.Count})", LogLevel.Debug);
                return isValid;
            }
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }
                Logger.Log("Vault cache cleared", LogLevel.Debug);
            }
        }
    }
}