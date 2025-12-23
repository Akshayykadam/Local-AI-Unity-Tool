using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// A search result from the vector store.
    /// </summary>
    public struct SearchResult
    {
        public string ChunkId;
        public float Score;
        public CodeChunk Chunk;
        
        public override string ToString() => $"{Chunk.Name} (score: {Score:F3})";
    }

    /// <summary>
    /// Stored vector entry for serialization.
    /// </summary>
    [Serializable]
    internal class VectorEntry
    {
        public string ChunkId;
        public float[] Embedding;
        public string FilePath;
        public int StartLine;
        public int EndLine;
        public string Type;
        public string Name;
        public string Content;
        public string Summary;
    }

    /// <summary>
    /// Lightweight file-based vector storage with similarity search.
    /// Stores embeddings in binary format for fast load/save.
    /// </summary>
    public class VectorStore : IDisposable
    {
        private const string VECTORS_FILE = "vectors.bin";
        private const string METADATA_FILE = "metadata.json";
        
        private readonly string _storeDirectory;
        private readonly int _embeddingDim;
        private List<VectorEntry> _entries;
        private bool _isDirty;
        
        public int Count => _entries?.Count ?? 0;
        
        public VectorStore(string storeDirectory = null, int embeddingDim = 384)
        {
            _storeDirectory = storeDirectory ?? GetDefaultStoreDirectory();
            _embeddingDim = embeddingDim;
            _entries = new List<VectorEntry>();
        }
        
        /// <summary>
        /// Adds a vector to the store.
        /// </summary>
        public void Add(string chunkId, float[] embedding, CodeChunk chunk)
        {
            // Check if entry already exists and update it
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].ChunkId == chunkId)
                {
                    _entries[i] = CreateEntry(chunkId, embedding, chunk);
                    _isDirty = true;
                    return;
                }
            }
            
            // Add new entry
            _entries.Add(CreateEntry(chunkId, embedding, chunk));
            _isDirty = true;
        }
        
        /// <summary>
        /// Removes vectors associated with a file.
        /// </summary>
        public void RemoveByFile(string filePath)
        {
            string normalized = filePath.Replace("\\", "/");
            int removed = _entries.RemoveAll(e => e.FilePath.Replace("\\", "/") == normalized);
            if (removed > 0) _isDirty = true;
        }
        
        /// <summary>
        /// Removes a specific vector by ID.
        /// </summary>
        public void Remove(string chunkId)
        {
            int removed = _entries.RemoveAll(e => e.ChunkId == chunkId);
            if (removed > 0) _isDirty = true;
        }
        
        /// <summary>
        /// Searches for the most similar vectors to the query.
        /// </summary>
        public List<SearchResult> Search(float[] queryEmbedding, int topK = 5)
        {
            var results = new List<SearchResult>();
            
            if (_entries.Count == 0 || queryEmbedding == null)
            {
                return results;
            }
            
            // Calculate similarity for all entries
            var scored = new List<(int index, float score)>();
            
            for (int i = 0; i < _entries.Count; i++)
            {
                float score = EmbeddingService.CosineSimilarity(queryEmbedding, _entries[i].Embedding);
                scored.Add((i, score));
            }
            
            // Sort by score descending
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            
            // Take top K
            int count = Math.Min(topK, scored.Count);
            for (int i = 0; i < count; i++)
            {
                var entry = _entries[scored[i].index];
                results.Add(new SearchResult
                {
                    ChunkId = entry.ChunkId,
                    Score = scored[i].score,
                    Chunk = new CodeChunk
                    {
                        FilePath = entry.FilePath,
                        StartLine = entry.StartLine,
                        EndLine = entry.EndLine,
                        Type = entry.Type,
                        Name = entry.Name,
                        Content = entry.Content,
                        Summary = entry.Summary,
                        ChunkId = entry.ChunkId
                    }
                });
            }
            
            return results;
        }
        
        /// <summary>
        /// Saves the store to disk.
        /// </summary>
        public void Save()
        {
            if (!_isDirty && File.Exists(Path.Combine(_storeDirectory, VECTORS_FILE)))
            {
                return; // No changes
            }
            
            try
            {
                if (!Directory.Exists(_storeDirectory))
                {
                    Directory.CreateDirectory(_storeDirectory);
                }
                
                // Save embeddings in binary format for efficiency
                string vectorsPath = Path.Combine(_storeDirectory, VECTORS_FILE);
                using (var stream = new FileStream(vectorsPath, FileMode.Create))
                using (var writer = new BinaryWriter(stream))
                {
                    // Header
                    writer.Write(_entries.Count);
                    writer.Write(_embeddingDim);
                    
                    // Vectors
                    foreach (var entry in _entries)
                    {
                        writer.Write(entry.ChunkId ?? "");
                        writer.Write(entry.FilePath ?? "");
                        writer.Write(entry.StartLine);
                        writer.Write(entry.EndLine);
                        writer.Write(entry.Type ?? "");
                        writer.Write(entry.Name ?? "");
                        writer.Write(entry.Content ?? "");
                        writer.Write(entry.Summary ?? "");
                        
                        // Embedding
                        for (int i = 0; i < _embeddingDim; i++)
                        {
                            float val = (i < entry.Embedding.Length) ? entry.Embedding[i] : 0;
                            writer.Write(val);
                        }
                    }
                }
                
                _isDirty = false;
                Debug.Log($"[SemanticSearch] Saved {_entries.Count} vectors to {vectorsPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Failed to save vectors: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads the store from disk.
        /// </summary>
        public void Load()
        {
            string vectorsPath = Path.Combine(_storeDirectory, VECTORS_FILE);
            
            if (!File.Exists(vectorsPath))
            {
                _entries = new List<VectorEntry>();
                return;
            }
            
            try
            {
                using (var stream = new FileStream(vectorsPath, FileMode.Open))
                using (var reader = new BinaryReader(stream))
                {
                    int count = reader.ReadInt32();
                    int dim = reader.ReadInt32();
                    
                    _entries = new List<VectorEntry>(count);
                    
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new VectorEntry
                        {
                            ChunkId = reader.ReadString(),
                            FilePath = reader.ReadString(),
                            StartLine = reader.ReadInt32(),
                            EndLine = reader.ReadInt32(),
                            Type = reader.ReadString(),
                            Name = reader.ReadString(),
                            Content = reader.ReadString(),
                            Summary = reader.ReadString(),
                            Embedding = new float[dim]
                        };
                        
                        for (int j = 0; j < dim; j++)
                        {
                            entry.Embedding[j] = reader.ReadSingle();
                        }
                        
                        _entries.Add(entry);
                    }
                }
                
                _isDirty = false;
                Debug.Log($"[SemanticSearch] Loaded {_entries.Count} vectors from {vectorsPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Failed to load vectors: {ex.Message}");
                _entries = new List<VectorEntry>();
            }
        }
        
        /// <summary>
        /// Clears all vectors from the store.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _isDirty = true;
            
            // Delete files
            string vectorsPath = Path.Combine(_storeDirectory, VECTORS_FILE);
            if (File.Exists(vectorsPath))
            {
                File.Delete(vectorsPath);
            }
        }
        
        /// <summary>
        /// Gets all unique file paths in the store.
        /// </summary>
        public HashSet<string> GetIndexedFiles()
        {
            var files = new HashSet<string>();
            foreach (var entry in _entries)
            {
                files.Add(entry.FilePath);
            }
            return files;
        }
        
        private VectorEntry CreateEntry(string chunkId, float[] embedding, CodeChunk chunk)
        {
            return new VectorEntry
            {
                ChunkId = chunkId,
                Embedding = embedding,
                FilePath = chunk.FilePath,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                Type = chunk.Type,
                Name = chunk.Name,
                Content = chunk.Content,
                Summary = chunk.Summary
            };
        }
        
        private static string GetDefaultStoreDirectory()
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            return Path.Combine(projectPath, "Library", "LocalAI", "SemanticIndex");
        }
        
        public void Dispose()
        {
            if (_isDirty)
            {
                Save();
            }
        }
    }
}
