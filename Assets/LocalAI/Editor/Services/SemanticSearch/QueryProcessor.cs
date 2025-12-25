using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Query intent classification for better search targeting.
    /// </summary>
    public enum QueryIntent
    {
        FindClass,      // Looking for a specific class
        FindMethod,     // Looking for a method/function
        FindProperty,   // Looking for a property/field
        Explain,        // Wants explanation of existing code
        HowTo,          // How to implement something
        Debug,          // Debugging/error related
        General         // General code search
    }

    /// <summary>
    /// Processes and enhances search queries for better RAG retrieval.
    /// Handles query expansion, keyword extraction, and intent classification.
    /// </summary>
    public class QueryProcessor
    {
        // Unity-specific terms to inject when relevant
        private static readonly Dictionary<string, string[]> UnityKeywordExpansions = new()
        {
            { "movement", new[] { "transform", "rigidbody", "velocity", "position", "translate", "move" } },
            { "input", new[] { "inputsystem", "getkey", "getaxis", "inputaction", "keyboard", "mouse" } },
            { "physics", new[] { "rigidbody", "collider", "raycast", "trigger", "collision", "force" } },
            { "animation", new[] { "animator", "animationclip", "mecanim", "transition", "blend" } },
            { "ui", new[] { "canvas", "button", "text", "image", "panel", "uitoolkit", "visualelement" } },
            { "audio", new[] { "audiosource", "audioclip", "mixer", "sound", "music" } },
            { "save", new[] { "playerprefs", "json", "serialize", "persist", "load", "data" } },
            { "network", new[] { "netcode", "multiplayer", "rpc", "sync", "client", "server" } },
            { "spawn", new[] { "instantiate", "prefab", "pool", "create", "destroy" } },
            { "camera", new[] { "cinemachine", "follow", "lookat", "viewport", "projection" } }
        };

        // C# code pattern keywords
        private static readonly string[] ClassIndicators = { "class", "interface", "struct", "enum", "type" };
        private static readonly string[] MethodIndicators = { "method", "function", "func", "void", "async", "call" };
        private static readonly string[] PropertyIndicators = { "property", "field", "variable", "get", "set" };
        private static readonly string[] DebugIndicators = { "error", "bug", "fix", "null", "exception", "crash", "issue" };

        /// <summary>
        /// Expands a query with related Unity/C# terms for better retrieval.
        /// </summary>
        public string ExpandQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return query;

            string lowerQuery = query.ToLowerInvariant();
            var expansions = new HashSet<string>();

            // Add Unity keyword expansions
            foreach (var kvp in UnityKeywordExpansions)
            {
                if (lowerQuery.Contains(kvp.Key))
                {
                    foreach (var expansion in kvp.Value)
                    {
                        if (!lowerQuery.Contains(expansion))
                        {
                            expansions.Add(expansion);
                        }
                    }
                }
            }

            // Add common Unity base classes if asking about components
            if (lowerQuery.Contains("component") || lowerQuery.Contains("script"))
            {
                expansions.Add("monobehaviour");
            }

            // Limit expansions to avoid query bloat
            var topExpansions = expansions.Take(5);
            
            if (!topExpansions.Any())
                return query;

            return $"{query} {string.Join(" ", topExpansions)}";
        }

        /// <summary>
        /// Extracts keywords from a query for keyword-based search.
        /// Filters out common stop words and returns meaningful terms.
        /// </summary>
        public List<string> ExtractKeywords(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();

            // Common stop words to filter
            var stopWords = new HashSet<string>
            {
                "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "could",
                "should", "may", "might", "can", "this", "that", "these", "those",
                "i", "you", "he", "she", "it", "we", "they", "what", "which", "who",
                "how", "when", "where", "why", "all", "each", "every", "both", "few",
                "more", "most", "other", "some", "such", "no", "not", "only", "own",
                "same", "so", "than", "too", "very", "just", "about", "into", "through",
                "for", "with", "from", "to", "of", "in", "on", "at", "by"
            };

            // Split on non-alphanumeric, filter, and normalize
            var words = Regex.Split(query.ToLowerInvariant(), @"[^a-z0-9]+")
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .ToList();

            // Also extract CamelCase or PascalCase identifiers
            var camelCaseMatches = Regex.Matches(query, @"[A-Z][a-z]+|[a-z]+");
            foreach (Match m in camelCaseMatches)
            {
                string word = m.Value.ToLowerInvariant();
                if (word.Length > 2 && !stopWords.Contains(word) && !words.Contains(word))
                {
                    words.Add(word);
                }
            }

            return words;
        }

        /// <summary>
        /// Classifies the intent of a query to help with result ranking.
        /// </summary>
        public QueryIntent ClassifyQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return QueryIntent.General;

            string lowerQuery = query.ToLowerInvariant();

            // Check for debug-related queries first (highest priority)
            if (DebugIndicators.Any(d => lowerQuery.Contains(d)))
                return QueryIntent.Debug;

            // Check for "how to" patterns
            if (lowerQuery.Contains("how to") || lowerQuery.Contains("how do") ||
                lowerQuery.Contains("implement") || lowerQuery.Contains("create"))
                return QueryIntent.HowTo;

            // Check for explanation requests
            if (lowerQuery.Contains("explain") || lowerQuery.Contains("what is") ||
                lowerQuery.Contains("what does") || lowerQuery.Contains("understand"))
                return QueryIntent.Explain;

            // Check for class-specific searches
            if (ClassIndicators.Any(c => lowerQuery.Contains(c)))
                return QueryIntent.FindClass;

            // Check for method-specific searches
            if (MethodIndicators.Any(m => lowerQuery.Contains(m)))
                return QueryIntent.FindMethod;

            // Check for property-specific searches
            if (PropertyIndicators.Any(p => lowerQuery.Contains(p)))
                return QueryIntent.FindProperty;

            return QueryIntent.General;
        }

        /// <summary>
        /// Generates an optimized search query combining expansion and core terms.
        /// </summary>
        public string OptimizeForSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return query;

            // Get expanded query
            string expanded = ExpandQuery(query);
            
            // Extract and prioritize keywords
            var keywords = ExtractKeywords(query);
            
            // For short queries, just return expanded
            if (keywords.Count <= 3)
                return expanded;

            // For longer queries, focus on key terms
            var priorityKeywords = keywords.Take(5);
            return $"{string.Join(" ", priorityKeywords)} {query}";
        }
    }
}
