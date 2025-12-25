using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Combines semantic (vector) search with keyword (BM25-style) search
    /// for improved retrieval quality.
    /// </summary>
    public class HybridSearchService
    {
        private readonly SemanticIndex _semanticIndex;
        private readonly QueryProcessor _queryProcessor;
        
        // Search weights (configurable)
        private const float SEMANTIC_WEIGHT = 0.7f;
        private const float KEYWORD_WEIGHT = 0.3f;
        
        public HybridSearchService(SemanticIndex semanticIndex)
        {
            _semanticIndex = semanticIndex;
            _queryProcessor = new QueryProcessor();
        }
        
        /// <summary>
        /// Performs hybrid search combining semantic and keyword matching.
        /// </summary>
        /// <param name="query">Natural language query</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>Merged and ranked search results</returns>
        public List<SearchResult> Search(string query, int topK = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SearchResult>();
                
            if (_semanticIndex.State != IndexState.Ready)
            {
                Debug.LogWarning("[HybridSearch] Semantic index not ready");
                return new List<SearchResult>();
            }
            
            // Expand query for better semantic search
            string expandedQuery = _queryProcessor.ExpandQuery(query);
            
            // Extract keywords for keyword search
            List<string> keywords = _queryProcessor.ExtractKeywords(query);
            
            // Get semantic results (more than topK to allow for filtering)
            int fetchCount = Math.Min(topK * 2, 20);
            List<SearchResult> semanticResults = _semanticIndex.Query(expandedQuery, fetchCount);
            
            // Apply keyword boost to results
            var boostedResults = ApplyKeywordBoost(semanticResults, keywords);
            
            // Sort by combined score and take topK
            return boostedResults
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();
        }
        
        /// <summary>
        /// Applies keyword matching boost to semantic search results.
        /// </summary>
        private List<SearchResult> ApplyKeywordBoost(List<SearchResult> results, List<string> keywords)
        {
            if (keywords.Count == 0)
                return results;
                
            var boostedResults = new List<SearchResult>();
            
            foreach (var result in results)
            {
                float keywordScore = CalculateKeywordScore(result.Chunk, keywords);
                
                // Combine scores: semantic * weight + keyword * weight
                float combinedScore = (result.Score * SEMANTIC_WEIGHT) + (keywordScore * KEYWORD_WEIGHT);
                
                boostedResults.Add(new SearchResult
                {
                    ChunkId = result.ChunkId,
                    Score = combinedScore,
                    Chunk = result.Chunk
                });
            }
            
            return boostedResults;
        }
        
        /// <summary>
        /// Calculates keyword match score for a chunk.
        /// Returns a value between 0 and 1.
        /// </summary>
        private float CalculateKeywordScore(CodeChunk chunk, List<string> keywords)
        {
            if (keywords.Count == 0)
                return 0f;
            
            // Combine searchable text
            string searchText = $"{chunk.Name} {chunk.Summary} {chunk.Content}".ToLowerInvariant();
            
            int matchCount = 0;
            float weightedMatches = 0f;
            
            foreach (var keyword in keywords)
            {
                string lowerKeyword = keyword.ToLowerInvariant();
                
                // Check for exact matches in different fields (with different weights)
                if (chunk.Name.ToLowerInvariant().Contains(lowerKeyword))
                {
                    // Name match is most important
                    weightedMatches += 1.0f;
                    matchCount++;
                }
                else if (!string.IsNullOrEmpty(chunk.Summary) && 
                         chunk.Summary.ToLowerInvariant().Contains(lowerKeyword))
                {
                    // Summary match is second
                    weightedMatches += 0.7f;
                    matchCount++;
                }
                else if (chunk.Content.ToLowerInvariant().Contains(lowerKeyword))
                {
                    // Content match
                    weightedMatches += 0.4f;
                    matchCount++;
                }
            }
            
            // Normalize by number of keywords
            float coverage = (float)matchCount / keywords.Count;
            float avgWeight = matchCount > 0 ? weightedMatches / matchCount : 0f;
            
            // Combined keyword score
            return coverage * avgWeight;
        }
        
        /// <summary>
        /// Performs search with query intent awareness.
        /// </summary>
        public List<SearchResult> SearchWithIntent(string query, int topK = 10)
        {
            QueryIntent intent = _queryProcessor.ClassifyQuery(query);
            
            // Get base results
            var results = Search(query, topK * 2);
            
            // Filter/boost based on intent
            results = FilterByIntent(results, intent);
            
            return results.Take(topK).ToList();
        }
        
        /// <summary>
        /// Filters and boosts results based on query intent.
        /// </summary>
        private List<SearchResult> FilterByIntent(List<SearchResult> results, QueryIntent intent)
        {
            return intent switch
            {
                QueryIntent.FindClass => results
                    .OrderByDescending(r => r.Chunk.Type == "class" ? r.Score * 1.5f : r.Score)
                    .ToList(),
                    
                QueryIntent.FindMethod => results
                    .OrderByDescending(r => r.Chunk.Type == "method" ? r.Score * 1.5f : r.Score)
                    .ToList(),
                    
                QueryIntent.FindProperty => results
                    .OrderByDescending(r => r.Chunk.Type == "property" ? r.Score * 1.5f : r.Score)
                    .ToList(),
                    
                QueryIntent.Debug => results
                    .OrderByDescending(r => 
                    {
                        // Boost error handling code
                        bool hasErrorHandling = r.Chunk.Content.Contains("catch") || 
                                               r.Chunk.Content.Contains("Exception") ||
                                               r.Chunk.Content.Contains("Debug.Log");
                        return hasErrorHandling ? r.Score * 1.3f : r.Score;
                    })
                    .ToList(),
                    
                _ => results
            };
        }
    }
}
