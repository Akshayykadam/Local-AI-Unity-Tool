using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Cached file information for incremental indexing.
    /// </summary>
    [Serializable]
    public class CachedFileInfo
    {
        public string Hash;
        public string LastModified;
        public List<string> ChunkIds = new List<string>();
    }
    
    /// <summary>
    /// Root structure for the index cache file.
    /// </summary>
    [Serializable]
    public class IndexCacheData
    {
        public string Version = "1.0";
        public string CreatedAt;
        public string LastUpdated;
        public Dictionary<string, CachedFileInfo> Files = new Dictionary<string, CachedFileInfo>();
    }

    /// <summary>
    /// File-based cache to track file hashes and avoid reprocessing unchanged files.
    /// </summary>
    public class IndexCache
    {
        private const string CACHE_FILENAME = "index_cache.json";
        
        private readonly string _cacheDirectory;
        private readonly string _cacheFilePath;
        private IndexCacheData _data;
        
        public int CachedFileCount => _data?.Files?.Count ?? 0;
        
        public IndexCache(string cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory ?? GetDefaultCacheDirectory();
            _cacheFilePath = Path.Combine(_cacheDirectory, CACHE_FILENAME);
            Load();
        }
        
        /// <summary>
        /// Loads the cache from disk.
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    _data = JsonUtility.FromJson<IndexCacheData>(json);
                    
                    // Handle deserialization issues with Dictionary
                    if (_data.Files == null)
                    {
                        _data.Files = new Dictionary<string, CachedFileInfo>();
                    }
                }
                else
                {
                    _data = new IndexCacheData
                    {
                        CreatedAt = DateTime.UtcNow.ToString("o"),
                        LastUpdated = DateTime.UtcNow.ToString("o")
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SemanticSearch] Failed to load cache: {ex.Message}");
                _data = new IndexCacheData
                {
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    LastUpdated = DateTime.UtcNow.ToString("o")
                };
            }
        }
        
        /// <summary>
        /// Saves the cache to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }
                
                _data.LastUpdated = DateTime.UtcNow.ToString("o");
                
                // Convert to serializable format (JsonUtility doesn't handle Dictionary well)
                string json = SerializeCache();
                File.WriteAllText(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Failed to save cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if a file has changed since it was last indexed.
        /// </summary>
        public bool HasFileChanged(string filePath, string currentHash)
        {
            if (!_data.Files.TryGetValue(NormalizePath(filePath), out var cachedInfo))
            {
                return true; // Not in cache = new file
            }
            
            return cachedInfo.Hash != currentHash;
        }
        
        /// <summary>
        /// Updates the cache entry for a file.
        /// </summary>
        public void UpdateFile(string filePath, string hash, List<string> chunkIds)
        {
            string key = NormalizePath(filePath);
            
            _data.Files[key] = new CachedFileInfo
            {
                Hash = hash,
                LastModified = DateTime.UtcNow.ToString("o"),
                ChunkIds = chunkIds ?? new List<string>()
            };
        }
        
        /// <summary>
        /// Gets the chunk IDs associated with a file.
        /// </summary>
        public List<string> GetChunkIds(string filePath)
        {
            if (_data.Files.TryGetValue(NormalizePath(filePath), out var info))
            {
                return info.ChunkIds;
            }
            return new List<string>();
        }
        
        /// <summary>
        /// Removes a file from the cache.
        /// </summary>
        public void RemoveFile(string filePath)
        {
            _data.Files.Remove(NormalizePath(filePath));
        }
        
        /// <summary>
        /// Clears the entire cache.
        /// </summary>
        public void Clear()
        {
            _data = new IndexCacheData
            {
                CreatedAt = DateTime.UtcNow.ToString("o"),
                LastUpdated = DateTime.UtcNow.ToString("o")
            };
            
            // Also delete the cache file
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }
        
        /// <summary>
        /// Gets all cached file paths.
        /// </summary>
        public IEnumerable<string> GetCachedFiles()
        {
            return _data.Files.Keys;
        }
        
        private string NormalizePath(string path)
        {
            return path.Replace("\\", "/");
        }
        
        private string SerializeCache()
        {
            // Manual JSON serialization since JsonUtility can't handle Dictionary
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"Version\": \"{_data.Version}\",\n");
            sb.Append($"  \"CreatedAt\": \"{_data.CreatedAt}\",\n");
            sb.Append($"  \"LastUpdated\": \"{_data.LastUpdated}\",\n");
            sb.Append("  \"Files\": {\n");
            
            int count = 0;
            foreach (var kvp in _data.Files)
            {
                sb.Append($"    \"{EscapeJson(kvp.Key)}\": {{\n");
                sb.Append($"      \"Hash\": \"{kvp.Value.Hash}\",\n");
                sb.Append($"      \"LastModified\": \"{kvp.Value.LastModified}\",\n");
                sb.Append($"      \"ChunkIds\": [{string.Join(", ", kvp.Value.ChunkIds.ConvertAll(id => $"\"{EscapeJson(id)}\""))}]\n");
                sb.Append("    }");
                
                if (++count < _data.Files.Count)
                    sb.Append(",");
                sb.Append("\n");
            }
            
            sb.Append("  }\n");
            sb.Append("}");
            
            return sb.ToString();
        }
        
        private string EscapeJson(string str)
        {
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
        
        private static string GetDefaultCacheDirectory()
        {
            // Store in Unity's Library folder (not version controlled)
            string projectPath = Application.dataPath.Replace("/Assets", "");
            return Path.Combine(projectPath, "Library", "LocalAI", "SemanticIndex");
        }
    }
}
