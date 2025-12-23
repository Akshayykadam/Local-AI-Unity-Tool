using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// State of the semantic index.
    /// </summary>
    public enum IndexState
    {
        Idle,
        Indexing,
        Ready,
        Error
    }

    /// <summary>
    /// Main coordinator for the semantic indexing pipeline.
    /// Orchestrates scanning, parsing, embedding, and storage.
    /// </summary>
    public class SemanticIndex : IDisposable
    {
        public event Action<float, string> OnIndexProgress;
        public event Action<IndexState> OnStateChanged;
        
        private readonly ProjectScanner _scanner;
        private readonly CSharpParser _parser;
        private readonly CodeChunker _chunker;
        private readonly IndexCache _cache;
        private readonly EmbeddingService _embedder;
        private readonly VectorStore _vectorStore;
        
        private IndexState _state = IndexState.Idle;
        private CancellationTokenSource _cts;
        
        public IndexState State => _state;
        public int IndexedChunkCount => _vectorStore?.Count ?? 0;
        
        public SemanticIndex()
        {
            _scanner = new ProjectScanner();
            _parser = new CSharpParser();
            _chunker = new CodeChunker();
            _cache = new IndexCache();
            _embedder = new EmbeddingService();
            _vectorStore = new VectorStore();
            
            // Initialize
            _embedder.Initialize();
            _vectorStore.Load();
            
            if (_vectorStore.Count > 0)
            {
                SetState(IndexState.Ready);
            }
        }
        
        /// <summary>
        /// Rebuilds the entire index from scratch.
        /// </summary>
        public async Task RebuildIndexAsync(List<string> folders = null, CancellationToken externalToken = default)
        {
            if (_state == IndexState.Indexing)
            {
                Debug.LogWarning("[SemanticSearch] Indexing already in progress");
                return;
            }
            
            folders ??= ProjectScanner.GetDefaultFolders();
            
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _cts.Token;
            
            SetState(IndexState.Indexing);
            
            try
            {
                // Clear existing data
                _cache.Clear();
                _vectorStore.Clear();
                
                ReportProgress(0, "Scanning project files...");
                
                // Scan files
                var files = await _scanner.ScanFoldersAsync(
                    folders,
                    LocalAISettings.MaxIndexedFiles,
                    new Progress<float>(p => ReportProgress(p * 0.2f, $"Scanning files... ({(int)(p * 100)}%)")),
                    token
                );
                
                if (token.IsCancellationRequested) return;
                
                ReportProgress(0.2f, $"Found {files.Count} C# files");
                
                // Process each file
                int processed = 0;
                int totalChunks = 0;
                
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;
                    
                    float fileProgress = 0.2f + (0.7f * processed / files.Count);
                    ReportProgress(fileProgress, $"Processing: {System.IO.Path.GetFileName(file.Path)}");
                    
                    try
                    {
                        // Parse file
                        var chunks = _parser.ParseFile(file.Path);
                        
                        // Split large chunks
                        chunks = _chunker.ProcessChunks(chunks);
                        
                        // Generate embeddings and store
                        var chunkIds = new List<string>();
                        
                        foreach (var chunk in chunks)
                        {
                            float[] embedding = _embedder.GenerateEmbedding(
                                $"{chunk.Name} {chunk.Summary} {chunk.Content}"
                            );
                            
                            _vectorStore.Add(chunk.ChunkId, embedding, chunk);
                            chunkIds.Add(chunk.ChunkId);
                            totalChunks++;
                        }
                        
                        // Update cache
                        _cache.UpdateFile(file.Path, file.Hash, chunkIds);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SemanticSearch] Error processing {file.Path}: {ex.Message}");
                    }
                    
                    processed++;
                    
                    // Yield periodically to prevent blocking
                    if (processed % 10 == 0)
                    {
                        await Task.Yield();
                    }
                }
                
                if (token.IsCancellationRequested)
                {
                    ReportProgress(1, "Indexing cancelled");
                    SetState(IndexState.Idle);
                    return;
                }
                
                // Save everything
                ReportProgress(0.9f, "Saving index...");
                _cache.Save();
                _vectorStore.Save();
                
                ReportProgress(1, $"Indexed {totalChunks} chunks from {processed} files");
                SetState(IndexState.Ready);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Rebuild failed: {ex.Message}");
                SetState(IndexState.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
        
        /// <summary>
        /// Performs incremental update, only processing changed files.
        /// </summary>
        public async Task IncrementalUpdateAsync(List<string> folders = null, CancellationToken externalToken = default)
        {
            if (_state == IndexState.Indexing)
            {
                Debug.LogWarning("[SemanticSearch] Indexing already in progress");
                return;
            }
            
            folders ??= ProjectScanner.GetDefaultFolders();
            
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _cts.Token;
            
            SetState(IndexState.Indexing);
            
            try
            {
                ReportProgress(0, "Scanning for changes...");
                
                var files = await _scanner.ScanFoldersAsync(folders, LocalAISettings.MaxIndexedFiles, null, token);
                
                if (token.IsCancellationRequested) return;
                
                // Find changed and new files
                var changedFiles = new List<ProjectScanner.FileInfo>();
                var currentPaths = new HashSet<string>();
                
                foreach (var file in files)
                {
                    currentPaths.Add(file.Path.Replace("\\", "/"));
                    
                    if (_cache.HasFileChanged(file.Path, file.Hash))
                    {
                        changedFiles.Add(file);
                    }
                }
                
                // Find deleted files
                var cachedFiles = _cache.GetCachedFiles();
                var deletedFiles = new List<string>();
                
                foreach (var cached in cachedFiles)
                {
                    if (!currentPaths.Contains(cached.Replace("\\", "/")))
                    {
                        deletedFiles.Add(cached);
                    }
                }
                
                ReportProgress(0.1f, $"Found {changedFiles.Count} changed, {deletedFiles.Count} deleted files");
                
                // Remove deleted files from index
                foreach (var deleted in deletedFiles)
                {
                    _vectorStore.RemoveByFile(deleted);
                    _cache.RemoveFile(deleted);
                }
                
                // Process changed files
                int processed = 0;
                int totalChunks = 0;
                
                foreach (var file in changedFiles)
                {
                    if (token.IsCancellationRequested) break;
                    
                    float progress = 0.1f + (0.8f * processed / Math.Max(1, changedFiles.Count));
                    ReportProgress(progress, $"Updating: {System.IO.Path.GetFileName(file.Path)}");
                    
                    try
                    {
                        // Remove old chunks
                        _vectorStore.RemoveByFile(file.Path);
                        
                        // Parse and re-index
                        var chunks = _parser.ParseFile(file.Path);
                        chunks = _chunker.ProcessChunks(chunks);
                        
                        var chunkIds = new List<string>();
                        
                        foreach (var chunk in chunks)
                        {
                            float[] embedding = _embedder.GenerateEmbedding(
                                $"{chunk.Name} {chunk.Summary} {chunk.Content}"
                            );
                            
                            _vectorStore.Add(chunk.ChunkId, embedding, chunk);
                            chunkIds.Add(chunk.ChunkId);
                            totalChunks++;
                        }
                        
                        _cache.UpdateFile(file.Path, file.Hash, chunkIds);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SemanticSearch] Error updating {file.Path}: {ex.Message}");
                    }
                    
                    processed++;
                }
                
                if (token.IsCancellationRequested)
                {
                    ReportProgress(1, "Update cancelled");
                    SetState(IndexState.Ready);
                    return;
                }
                
                // Save
                ReportProgress(0.9f, "Saving index...");
                _cache.Save();
                _vectorStore.Save();
                
                ReportProgress(1, $"Updated {totalChunks} chunks from {processed} files");
                SetState(IndexState.Ready);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Incremental update failed: {ex.Message}");
                SetState(IndexState.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
        
        /// <summary>
        /// Searches the index with a natural language query.
        /// </summary>
        public List<SearchResult> Query(string naturalLanguageQuery, int topK = 5)
        {
            if (string.IsNullOrWhiteSpace(naturalLanguageQuery))
            {
                return new List<SearchResult>();
            }
            
            if (_state != IndexState.Ready)
            {
                Debug.LogWarning("[SemanticSearch] Index not ready. Run rebuild first.");
                return new List<SearchResult>();
            }
            
            // Generate query embedding
            float[] queryEmbedding = _embedder.GenerateEmbedding(naturalLanguageQuery);
            
            // Search vector store
            return _vectorStore.Search(queryEmbedding, topK);
        }
        
        /// <summary>
        /// Cancels any ongoing indexing operation.
        /// </summary>
        public void CancelIndexing()
        {
            _cts?.Cancel();
        }
        
        /// <summary>
        /// Clears the entire index.
        /// </summary>
        public void ClearIndex()
        {
            _cache.Clear();
            _vectorStore.Clear();
            SetState(IndexState.Idle);
        }
        
        private void SetState(IndexState state)
        {
            _state = state;
            OnStateChanged?.Invoke(state);
        }
        
        private void ReportProgress(float progress, string message)
        {
            OnIndexProgress?.Invoke(progress, message);
        }
        
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _embedder?.Dispose();
            _vectorStore?.Dispose();
        }
    }
}
