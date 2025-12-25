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
    /// Enhanced with hybrid search, query expansion, and result reranking.
    /// </summary>
    public class RAGService
    {
        private readonly SemanticIndex _index;
        private readonly Func<IInferenceService> _getInferenceService;
        private readonly HybridSearchService _hybridSearch;
        private readonly QueryProcessor _queryProcessor;
        private readonly ResultReranker _reranker;
        
        private const int DEFAULT_TOP_K = 5;
        private const int MAX_CONTEXT_CHARS = 4000;
        private const float MIN_RELEVANCE_SCORE = 0.25f;
        
        public RAGService(SemanticIndex index, Func<IInferenceService> getInferenceService)
        {
            _index = index;
            _getInferenceService = getInferenceService;
            _hybridSearch = new HybridSearchService(index);
            _queryProcessor = new QueryProcessor();
            _reranker = new ResultReranker();
        }
        
        /// <summary>
        /// Queries the codebase using enhanced RAG pipeline.
        /// Pipeline: Query Expansion → Hybrid Search → Rerank → Context Build → LLM
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
            
            // Step 1: Classify query intent
            QueryIntent intent = _queryProcessor.ClassifyQuery(userQuery);
            progress?.Report($"🎯 Query Intent: {intent}\n");
            
            // Step 2: Perform hybrid search (semantic + keyword)
            progress?.Report("🔍 Searching codebase...\n");
            List<SearchResult> results = _hybridSearch.SearchWithIntent(userQuery, topK * 2);
            
            if (results.Count == 0)
            {
                return "No relevant code found. Try a different query or rebuild the index.";
            }
            
            // Step 3: Rerank results
            progress?.Report("📊 Ranking results...\n");
            results = _reranker.Rerank(results, userQuery, intent);
            
            // Step 4: Filter by relevance and deduplicate
            results = _reranker.FilterByRelevance(results, MIN_RELEVANCE_SCORE);
            results = _reranker.Deduplicate(results);
            
            // Take top K after filtering
            if (results.Count > topK)
            {
                results = results.GetRange(0, topK);
            }
            
            if (results.Count == 0)
            {
                return "No sufficiently relevant code found. Try rephrasing your query.";
            }
            
            progress?.Report($"📋 Found {results.Count} relevant chunks\n\n");
            
            // Step 5: Build context from retrieved chunks
            string retrievedContext = BuildContext(results);
            
            // Step 6: Build RAG prompt
            string ragPrompt = BuildRAGPrompt(userQuery, retrievedContext, results, intent);
            
            // Step 7: Get LLM response
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
        /// Gets context for a query without LLM response. Used for Chat integration.
        /// </summary>
        public string GetContextForQuery(string query, int topK = 3)
        {
            if (_index.State != IndexState.Ready || string.IsNullOrWhiteSpace(query))
                return "";
                
            QueryIntent intent = _queryProcessor.ClassifyQuery(query);
            var results = _hybridSearch.SearchWithIntent(query, topK * 2);
            
            if (results.Count == 0)
                return "";
                
            results = _reranker.Rerank(results, query, intent);
            results = _reranker.FilterByRelevance(results, MIN_RELEVANCE_SCORE);
            results = _reranker.Deduplicate(results);
            
            if (results.Count > topK)
                results = results.GetRange(0, topK);
                
            if (results.Count == 0)
                return "";
                
            var sb = new StringBuilder();
            sb.AppendLine("=== RELEVANT CODE FROM PROJECT ===");
            sb.AppendLine(BuildContext(results));
            sb.AppendLine("=== END OF PROJECT CONTEXT ===\n");
            
            Debug.Log($"[LocalAI] RAG context added: {results.Count} chunks");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Performs search-only query using enhanced pipeline.
        /// </summary>
        public List<SearchResult> SearchOnly(string query, int topK = DEFAULT_TOP_K)
        {
            if (_index.State != IndexState.Ready)
                return new List<SearchResult>();
                
            QueryIntent intent = _queryProcessor.ClassifyQuery(query);
            var results = _hybridSearch.SearchWithIntent(query, topK * 2);
            
            results = _reranker.Rerank(results, query, intent);
            results = _reranker.FilterByRelevance(results, MIN_RELEVANCE_SCORE);
            results = _reranker.Deduplicate(results);
            
            return results.Count > topK ? results.GetRange(0, topK) : results;
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
            
            // Header with type and relevance
            sb.AppendLine($"[{chunk.Type.ToUpper()}] {chunk.Name} (Relevance: {result.Score:P0})");
            
            // Signature prominently displayed
            if (!string.IsNullOrEmpty(chunk.Signature))
            {
                sb.AppendLine($"Signature: {chunk.Signature}");
            }
            
            // Location
            sb.AppendLine($"Location: {GetRelativePath(chunk.FilePath)}:{chunk.StartLine}-{chunk.EndLine}");
            
            // Documentation
            if (!string.IsNullOrEmpty(chunk.Summary))
            {
                sb.AppendLine($"Description: {chunk.Summary}");
            }
            
            // Code block
            sb.AppendLine("```csharp");
            
            // For methods, extract just the signature + body summary if too long
            string content = chunk.Content;
            if (content.Length > 600)
            {
                // Try to keep the first meaningful portion
                int cutPoint = Math.Min(600, content.Length);
                // Find last complete line
                int lastNewline = content.LastIndexOf('\n', cutPoint);
                if (lastNewline > 200)
                {
                    content = content.Substring(0, lastNewline) + "\n    // ... (implementation continues)";
                }
                else
                {
                    content = content.Substring(0, cutPoint) + "...";
                }
            }
            sb.AppendLine(content.Trim());
            
            sb.AppendLine("```");
            sb.AppendLine();
            
            return sb.ToString();
        }
        
        private string BuildRAGPrompt(string query, string context, List<SearchResult> results, QueryIntent intent)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("[INST] You are an expert Unity C# code analyst. Your task is to answer questions using ONLY the retrieved code context below.");
            sb.AppendLine();
            
            // Intent-specific system prompt
            string intentInstruction = intent switch
            {
                QueryIntent.Debug => "FOCUS: Identify bugs, null reference risks, race conditions, or logic errors. Suggest specific fixes with code.",
                QueryIntent.HowTo => "FOCUS: Provide step-by-step implementation guidance using patterns found in the codebase.",
                QueryIntent.Explain => "FOCUS: Explain the purpose, flow, and design of the code. Use the actual signatures and class names.",
                QueryIntent.FindClass => "FOCUS: Describe the class structure, its responsibilities, key methods, and how it relates to other components.",
                QueryIntent.FindMethod => "FOCUS: Explain what the method does, its parameters, return value, and any side effects.",
                QueryIntent.FindProperty => "FOCUS: Describe the property's purpose, type, and any getter/setter logic.",
                _ => "FOCUS: Provide accurate, specific answers referencing the actual code."
            };
            
            sb.AppendLine("STRICT RULES:");
            sb.AppendLine("1. ONLY cite code that appears in the context below - do NOT invent or guess API names");
            sb.AppendLine("2. Reference files and line numbers precisely: `FileName.cs:42`");
            sb.AppendLine("3. Quote method signatures exactly as shown");
            sb.AppendLine("4. If the context is insufficient, clearly state what information is missing");
            sb.AppendLine("5. Be concise - avoid generic programming advice");
            sb.AppendLine();
            sb.AppendLine(intentInstruction);
            sb.AppendLine();
            sb.AppendLine("========== RETRIEVED CODE CONTEXT ==========");
            sb.AppendLine(context);
            sb.AppendLine("========== END OF CONTEXT ==========");
            sb.AppendLine();
            sb.AppendLine($"QUESTION: {query}");
            sb.AppendLine();
            sb.AppendLine("Answer accurately using only the code above. Start with the most relevant information. [/INST]");
            
            return sb.ToString();
        }
        
        private string FormatSearchResultsOnly(string query, List<SearchResult> results)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"## Search Results for: \"{query}\"\n");
            sb.AppendLine("*(No LLM available for analysis - showing ranked results)*\n");
            
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
