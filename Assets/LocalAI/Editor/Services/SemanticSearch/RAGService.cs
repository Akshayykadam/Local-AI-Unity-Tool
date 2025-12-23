using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Retrieval-Augmented Generation service.
    /// Combines semantic search with LLM reasoning for intelligent code queries.
    /// </summary>
    public class RAGService
    {
        private readonly SemanticIndex _index;
        private readonly Func<IInferenceService> _getInferenceService;
        
        private const int DEFAULT_TOP_K = 5;
        private const int MAX_CONTEXT_CHARS = 4000;
        
        public RAGService(SemanticIndex index, Func<IInferenceService> getInferenceService)
        {
            _index = index;
            _getInferenceService = getInferenceService;
        }
        
        /// <summary>
        /// Queries the codebase using RAG (retrieval + LLM reasoning).
        /// </summary>
        public async Task<string> QueryWithContextAsync(
            string userQuery,
            IProgress<string> progress = null,
            CancellationToken token = default,
            int topK = DEFAULT_TOP_K)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
            {
                return "Please enter a query.";
            }
            
            if (_index.State != IndexState.Ready)
            {
                return "⚠️ Index not ready. Please build the index first using the 'Re-Index' button.";
            }
            
            // Step 1: Retrieve relevant code chunks
            progress?.Report("🔍 Searching codebase...\n");
            
            List<SearchResult> results = _index.Query(userQuery, topK);
            
            if (results.Count == 0)
            {
                return "No relevant code found. Try a different query or rebuild the index.";
            }
            
            // Step 2: Build context from retrieved chunks
            string retrievedContext = BuildContext(results);
            
            // Step 3: Build RAG prompt
            string ragPrompt = BuildRAGPrompt(userQuery, retrievedContext, results);
            
            // Step 4: Get LLM response
            var inferenceService = _getInferenceService?.Invoke();
            
            if (inferenceService == null || !inferenceService.IsReady)
            {
                // Return just the search results if no LLM available
                return FormatSearchResultsOnly(userQuery, results);
            }
            
            progress?.Report("🤖 Analyzing with AI...\n\n");
            
            var responseBuilder = new StringBuilder();
            
            await inferenceService.StartInferenceAsync(
                ragPrompt,
                new Progress<string>(chunk => 
                {
                    responseBuilder.Append(chunk);
                    progress?.Report(chunk);
                }),
                token
            );
            
            return responseBuilder.ToString();
        }
        
        /// <summary>
        /// Performs search-only query (no LLM reasoning).
        /// </summary>
        public List<SearchResult> SearchOnly(string query, int topK = DEFAULT_TOP_K)
        {
            return _index.Query(query, topK);
        }
        
        private string BuildContext(List<SearchResult> results)
        {
            var sb = new StringBuilder();
            int totalChars = 0;
            
            foreach (var result in results)
            {
                string chunkText = FormatChunkForContext(result);
                
                if (totalChars + chunkText.Length > MAX_CONTEXT_CHARS)
                {
                    // Truncate to fit
                    int remaining = MAX_CONTEXT_CHARS - totalChars;
                    if (remaining > 100)
                    {
                        sb.Append(chunkText.Substring(0, remaining));
                        sb.AppendLine("\n[... truncated ...]");
                    }
                    break;
                }
                
                sb.Append(chunkText);
                totalChars += chunkText.Length;
            }
            
            return sb.ToString();
        }
        
        private string FormatChunkForContext(SearchResult result)
        {
            var sb = new StringBuilder();
            var chunk = result.Chunk;
            
            sb.AppendLine($"--- {chunk.Name} ({chunk.Type}) ---");
            sb.AppendLine($"File: {GetRelativePath(chunk.FilePath)} (lines {chunk.StartLine}-{chunk.EndLine})");
            
            if (!string.IsNullOrEmpty(chunk.Summary))
            {
                sb.AppendLine($"Summary: {chunk.Summary}");
            }
            
            sb.AppendLine("```csharp");
            
            // Limit code content
            string content = chunk.Content;
            if (content.Length > 800)
            {
                content = content.Substring(0, 800) + "\n// ... (truncated)";
            }
            sb.AppendLine(content.Trim());
            
            sb.AppendLine("```");
            sb.AppendLine();
            
            return sb.ToString();
        }
        
        private string BuildRAGPrompt(string query, string context, List<SearchResult> results)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("[INST] You are an expert Unity C# code assistant. Answer questions about the codebase using ONLY the retrieved code context below.");
            sb.AppendLine();
            sb.AppendLine("RULES:");
            sb.AppendLine("1. ONLY reference code that appears in the context below");
            sb.AppendLine("2. Quote exact file paths and line numbers when referencing code");
            sb.AppendLine("3. If the context doesn't contain enough information, say so clearly");
            sb.AppendLine("4. Be concise and accurate - no speculation");
            sb.AppendLine("5. Format code references as: `FileName.cs:Line` or `ClassName.MethodName()`");
            sb.AppendLine();
            sb.AppendLine("=== RETRIEVED CODE CONTEXT ===");
            sb.AppendLine(context);
            sb.AppendLine("=== END OF CONTEXT ===");
            sb.AppendLine();
            sb.AppendLine($"USER QUESTION: {query}");
            sb.AppendLine();
            sb.AppendLine("Provide a clear, factual answer based on the code context above. [/INST]");
            
            return sb.ToString();
        }
        
        private string FormatSearchResultsOnly(string query, List<SearchResult> results)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"## Search Results for: \"{query}\"\n");
            sb.AppendLine("*(No LLM available for analysis - showing raw results)*\n");
            
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var chunk = result.Chunk;
                
                sb.AppendLine($"### {i + 1}. {chunk.Name}");
                sb.AppendLine($"**File:** `{GetRelativePath(chunk.FilePath)}` (lines {chunk.StartLine}-{chunk.EndLine})");
                sb.AppendLine($"**Type:** {chunk.Type} | **Relevance:** {result.Score:P0}");
                
                if (!string.IsNullOrEmpty(chunk.Summary))
                {
                    sb.AppendLine($"> {chunk.Summary}");
                }
                
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        private string GetRelativePath(string fullPath)
        {
            string normalized = fullPath.Replace("\\", "/");
            int assetsIndex = normalized.IndexOf("Assets/");
            
            if (assetsIndex >= 0)
            {
                return normalized.Substring(assetsIndex);
            }
            
            return System.IO.Path.GetFileName(fullPath);
        }
    }
}
