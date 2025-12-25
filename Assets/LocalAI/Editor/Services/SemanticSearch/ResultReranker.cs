using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Re-ranks search results using multiple relevance signals
    /// beyond pure semantic similarity.
    /// </summary>
    public class ResultReranker
    {
        private readonly QueryProcessor _queryProcessor;
        
        // Reranking weights
        private const float SEMANTIC_SCORE_WEIGHT = 0.5f;
        private const float KEYWORD_DENSITY_WEIGHT = 0.2f;
        private const float STRUCTURE_MATCH_WEIGHT = 0.15f;
        private const float RECENCY_WEIGHT = 0.1f;
        private const float CODE_QUALITY_WEIGHT = 0.05f;
        
        public ResultReranker()
        {
            _queryProcessor = new QueryProcessor();
        }
        
        /// <summary>
        /// Reranks results using multiple signals.
        /// </summary>
        public List<SearchResult> Rerank(
            List<SearchResult> results, 
            string originalQuery, 
            QueryIntent intent = QueryIntent.General)
        {
            if (results == null || results.Count == 0)
                return results ?? new List<SearchResult>();
                
            var keywords = _queryProcessor.ExtractKeywords(originalQuery);
            var rerankedResults = new List<SearchResult>();
            
            foreach (var result in results)
            {
                float finalScore = CalculateCombinedScore(result, keywords, intent);
                
                rerankedResults.Add(new SearchResult
                {
                    ChunkId = result.ChunkId,
                    Score = finalScore,
                    Chunk = result.Chunk
                });
            }
            
            return rerankedResults
                .OrderByDescending(r => r.Score)
                .ToList();
        }
        
        /// <summary>
        /// Calculates combined relevance score using multiple signals.
        /// </summary>
        private float CalculateCombinedScore(SearchResult result, List<string> keywords, QueryIntent intent)
        {
            float semanticScore = result.Score;
            float keywordDensity = CalculateKeywordDensity(result.Chunk, keywords);
            float structureMatch = CalculateStructureMatch(result.Chunk, intent);
            float recencyBonus = CalculateRecencyBonus(result.Chunk);
            float codeQuality = EstimateCodeQuality(result.Chunk);
            
            // Weighted combination
            float combinedScore = 
                (semanticScore * SEMANTIC_SCORE_WEIGHT) +
                (keywordDensity * KEYWORD_DENSITY_WEIGHT) +
                (structureMatch * STRUCTURE_MATCH_WEIGHT) +
                (recencyBonus * RECENCY_WEIGHT) +
                (codeQuality * CODE_QUALITY_WEIGHT);
                
            return combinedScore;
        }
        
        /// <summary>
        /// Calculates keyword density in the chunk content.
        /// </summary>
        private float CalculateKeywordDensity(CodeChunk chunk, List<string> keywords)
        {
            if (keywords.Count == 0)
                return 0.5f; // Neutral score
                
            string text = $"{chunk.Name} {chunk.Summary} {chunk.Content}".ToLowerInvariant();
            int totalLength = text.Length;
            
            if (totalLength == 0)
                return 0f;
                
            int matchedChars = 0;
            foreach (var keyword in keywords)
            {
                string lowerKeyword = keyword.ToLowerInvariant();
                int index = 0;
                while ((index = text.IndexOf(lowerKeyword, index)) != -1)
                {
                    matchedChars += lowerKeyword.Length;
                    index += lowerKeyword.Length;
                }
            }
            
            // Normalize to 0-1 range (cap at 10% density as maximum)
            float density = (float)matchedChars / totalLength;
            return Math.Min(density * 10f, 1f);
        }
        
        /// <summary>
        /// Calculates how well the chunk type matches the query intent.
        /// </summary>
        private float CalculateStructureMatch(CodeChunk chunk, QueryIntent intent)
        {
            return intent switch
            {
                QueryIntent.FindClass => chunk.Type == "class" ? 1f : 0.3f,
                QueryIntent.FindMethod => chunk.Type == "method" ? 1f : 0.3f,
                QueryIntent.FindProperty => chunk.Type == "property" ? 1f : 0.3f,
                QueryIntent.Debug => HasErrorHandling(chunk) ? 0.8f : 0.4f,
                QueryIntent.HowTo => chunk.Type == "method" ? 0.7f : 0.5f,
                QueryIntent.Explain => !string.IsNullOrEmpty(chunk.Summary) ? 0.8f : 0.5f,
                _ => 0.5f // Neutral for general queries
            };
        }
        
        /// <summary>
        /// Calculates recency bonus based on file modification time.
        /// </summary>
        private float CalculateRecencyBonus(CodeChunk chunk)
        {
            try
            {
                if (!File.Exists(chunk.FilePath))
                    return 0.5f;
                    
                var lastModified = File.GetLastWriteTimeUtc(chunk.FilePath);
                var age = DateTime.UtcNow - lastModified;
                
                // Files modified in last 7 days get full bonus
                // Bonus decreases linearly over 90 days
                if (age.TotalDays <= 7)
                    return 1f;
                if (age.TotalDays >= 90)
                    return 0.3f;
                    
                return 1f - ((float)(age.TotalDays - 7) / 83f * 0.7f);
            }
            catch
            {
                return 0.5f; // Neutral on error
            }
        }
        
        /// <summary>
        /// Estimates code quality based on documentation and structure.
        /// </summary>
        private float EstimateCodeQuality(CodeChunk chunk)
        {
            float score = 0f;
            
            // Has XML documentation
            if (!string.IsNullOrEmpty(chunk.Summary))
                score += 0.4f;
                
            // Reasonable code length (not too short, not too long)
            int lines = chunk.EndLine - chunk.StartLine;
            if (lines >= 5 && lines <= 100)
                score += 0.3f;
            else if (lines > 0)
                score += 0.1f;
                
            // Has meaningful name (not just single letter or generic)
            if (!string.IsNullOrEmpty(chunk.Name) && chunk.Name.Length > 3)
                score += 0.3f;
                
            return Math.Min(score, 1f);
        }
        
        /// <summary>
        /// Checks if chunk contains error handling code.
        /// </summary>
        private bool HasErrorHandling(CodeChunk chunk)
        {
            string content = chunk.Content;
            return content.Contains("catch") ||
                   content.Contains("try") ||
                   content.Contains("Exception") ||
                   content.Contains("Debug.LogError") ||
                   content.Contains("throw");
        }
        
        /// <summary>
        /// Filters results below a minimum relevance threshold.
        /// </summary>
        public List<SearchResult> FilterByRelevance(List<SearchResult> results, float minScore = 0.3f)
        {
            return results.Where(r => r.Score >= minScore).ToList();
        }
        
        /// <summary>
        /// Removes duplicate or very similar chunks.
        /// </summary>
        public List<SearchResult> Deduplicate(List<SearchResult> results)
        {
            var seen = new HashSet<string>();
            var deduplicated = new List<SearchResult>();
            
            foreach (var result in results)
            {
                // Create a signature from file + approximate location
                string signature = $"{Path.GetFileName(result.Chunk.FilePath)}:{result.Chunk.StartLine / 10}";
                
                if (!seen.Contains(signature))
                {
                    seen.Add(signature);
                    deduplicated.Add(result);
                }
            }
            
            return deduplicated;
        }
    }
}
