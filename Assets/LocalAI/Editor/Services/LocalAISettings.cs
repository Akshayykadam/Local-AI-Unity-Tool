using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Available AI providers.
    /// </summary>
    public enum AIProvider
    {
        Local = 0,
        Gemini = 1,
        OpenAI = 2,
        Claude = 3
    }
    
    /// <summary>
    /// Stores and retrieves user preferences for the Local AI tool.
    /// Uses EditorPrefs for persistence across sessions.
    /// </summary>
    public static class LocalAISettings
    {
        private const string PREFIX = "LocalAI_";
        
        // Defaults
        public const uint DEFAULT_CONTEXT_SIZE = 4096;
        public const int DEFAULT_MAX_TOKENS = 512;
        
        // Provider options
        public static readonly string[] ProviderLabels = { "Local (Offline)", "Google Gemini", "OpenAI", "Anthropic Claude" };
        
        /// <summary>
        /// Currently selected AI provider.
        /// </summary>
        public static event System.Action<AIProvider> OnProviderChanged;

        /// <summary>
        /// Currently selected AI provider.
        /// </summary>
        public static AIProvider ActiveProvider
        {
            get => (AIProvider)EditorPrefs.GetInt(PREFIX + "ActiveProvider", 0);
            set
            {
                EditorPrefs.SetInt(PREFIX + "ActiveProvider", (int)value);
                Debug.Log($"[LocalAI] ActiveProvider SET to: {value} (int: {(int)value})");
                OnProviderChanged?.Invoke(value);
            }
        }
        
        /// <summary>
        /// Google Gemini API key.
        /// </summary>
        public static string GeminiApiKey
        {
            get => EditorPrefs.GetString(PREFIX + "GeminiApiKey", "");
            set => EditorPrefs.SetString(PREFIX + "GeminiApiKey", value);
        }

        // Gemini Models
        public static readonly string[] GeminiModelOptions = { "gemini-2.5-flash-lite" };
        public static readonly string[] GeminiModelLabels = { "Gemini 2.5 Flash Lite" };

        public static string GeminiModel
        {
            get 
            {
                string stored = EditorPrefs.GetString(PREFIX + "GeminiModel", "gemini-2.5-flash-lite");
                // Validate that stored value exists in options
                bool exists = false;
                foreach (var opt in GeminiModelOptions)
                {
                    if (opt == stored)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    // Fallback to default if stored value is invalid/removed
                    stored = GeminiModelOptions[0];
                    EditorPrefs.SetString(PREFIX + "GeminiModel", stored);
                }
                return stored;
            }
            set => EditorPrefs.SetString(PREFIX + "GeminiModel", value);
        }
        
        public static int GetGeminiModelIndex()
        {
            string current = GeminiModel;
            for (int i = 0; i < GeminiModelOptions.Length; i++)
            {
                if (GeminiModelOptions[i] == current) return i;
            }
            return 0; // Default
        }
        
        /// <summary>
        /// OpenAI API key.
        /// </summary>
        public static string OpenAIApiKey
        {
            get => EditorPrefs.GetString(PREFIX + "OpenAIApiKey", "");
            set => EditorPrefs.SetString(PREFIX + "OpenAIApiKey", value);
        }
        
        /// <summary>
        /// Anthropic Claude API key.
        /// </summary>
        public static string ClaudeApiKey
        {
            get => EditorPrefs.GetString(PREFIX + "ClaudeApiKey", "");
            set => EditorPrefs.SetString(PREFIX + "ClaudeApiKey", value);
        }
        
        /// <summary>
        /// Check if an API key is configured for the given provider.
        /// </summary>
        public static bool HasApiKey(AIProvider provider)
        {
            return provider switch
            {
                AIProvider.Gemini => !string.IsNullOrEmpty(GeminiApiKey),
                AIProvider.OpenAI => !string.IsNullOrEmpty(OpenAIApiKey),
                AIProvider.Claude => !string.IsNullOrEmpty(ClaudeApiKey),
                AIProvider.Local => true, // Local doesn't need API key
                _ => false
            };
        }
        
        /// <summary>
        /// Get API key for the given provider.
        /// </summary>
        public static string GetApiKey(AIProvider provider)
        {
            return provider switch
            {
                AIProvider.Gemini => GeminiApiKey,
                AIProvider.OpenAI => OpenAIApiKey,
                AIProvider.Claude => ClaudeApiKey,
                _ => ""
            };
        }
        
        // Context Size options
        public static readonly uint[] ContextSizeOptions = { 2048, 4096, 8192, 16384, 32768 };
        public static readonly string[] ContextSizeLabels = { "2K (Fast)", "4K (Standard)", "8K (Large)", "16K (Huge)", "32K (Massive)" };
        
        // Max Tokens options (Cloud providers like Gemini support higher limits)
        public static readonly int[] MaxTokensOptions = { 1024, 2048, 4096, 8192, 16384, 32768 };
        public static readonly string[] MaxTokensLabels = { "1K (Short)", "2K (Normal)", "4K (Long)", "8K (Extensive)", "16K (Large)", "32K (Max)" };
        
        public static uint ContextSize
        {
            get => (uint)EditorPrefs.GetInt(PREFIX + "ContextSize", (int)DEFAULT_CONTEXT_SIZE);
            set => EditorPrefs.SetInt(PREFIX + "ContextSize", (int)value);
        }
        
        public static int MaxTokens
        {
            get => EditorPrefs.GetInt(PREFIX + "MaxTokens", 4096); // Default to 4096 for longer outputs
            set => EditorPrefs.SetInt(PREFIX + "MaxTokens", value);
        }
        
        public static int GetContextSizeIndex()
        {
            uint current = ContextSize;
            for (int i = 0; i < ContextSizeOptions.Length; i++)
            {
                if (ContextSizeOptions[i] == current) return i;
            }
            return 1; // Default to 4K (Index 1)
        }
        
        public static int GetMaxTokensIndex()
        {
            int current = MaxTokens;
            for (int i = 0; i < MaxTokensOptions.Length; i++)
            {
                if (MaxTokensOptions[i] == current) return i;
            }
            return 2; // Default to 2048 (Index 2)
        }
        
        // ===== Semantic Search Settings =====
        
        public const int DEFAULT_MAX_INDEXED_FILES = 5000;
        public const int DEFAULT_MAX_CHUNK_SIZE = 512;
        
        /// <summary>
        /// Maximum number of files to index for semantic search.
        /// </summary>
        public static int MaxIndexedFiles
        {
            get => EditorPrefs.GetInt(PREFIX + "MaxIndexedFiles", DEFAULT_MAX_INDEXED_FILES);
            set => EditorPrefs.SetInt(PREFIX + "MaxIndexedFiles", value);
        }
        
        /// <summary>
        /// Maximum chunk size in tokens for semantic indexing.
        /// </summary>
        public static int MaxChunkSize
        {
            get => EditorPrefs.GetInt(PREFIX + "MaxChunkSize", DEFAULT_MAX_CHUNK_SIZE);
            set => EditorPrefs.SetInt(PREFIX + "MaxChunkSize", value);
        }
        
        /// <summary>
        /// Whether to automatically re-index when files change.
        /// </summary>
        public static bool AutoReindexOnChange
        {
            get => EditorPrefs.GetBool(PREFIX + "AutoReindexOnChange", false);
            set => EditorPrefs.SetBool(PREFIX + "AutoReindexOnChange", value);
        }
        
        /// <summary>
        /// Comma-separated list of folders to index (relative to project root).
        /// </summary>
        public static string IndexedFolders
        {
            get => EditorPrefs.GetString(PREFIX + "IndexedFolders", "Assets/");
            set => EditorPrefs.SetString(PREFIX + "IndexedFolders", value);
        }
        
        /// <summary>
        /// Gets the indexed folders as a list of full paths.
        /// </summary>
        public static System.Collections.Generic.List<string> GetIndexedFoldersList()
        {
            var result = new System.Collections.Generic.List<string>();
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            
            string[] folders = IndexedFolders.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string folder in folders)
            {
                string trimmed = folder.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                if (System.IO.Path.IsPathRooted(trimmed))
                {
                    result.Add(trimmed);
                }
                else
                {
                    result.Add(System.IO.Path.Combine(projectRoot, trimmed));
                }
            }
            
            // Default to Assets/ if empty
            if (result.Count == 0)
            {
                result.Add(Application.dataPath);
            }
            
            return result;
        }
        
        // ===== RAG (Retrieval-Augmented Generation) Settings =====
        
        /// <summary>
        /// Whether to use RAG for Chat tab queries.
        /// When enabled, relevant code from the project is retrieved and added to context.
        /// </summary>
        public static bool EnableRAG
        {
            get => EditorPrefs.GetBool(PREFIX + "EnableRAG", true);
            set => EditorPrefs.SetBool(PREFIX + "EnableRAG", value);
        }
        
        /// <summary>
        /// Number of code chunks to retrieve for RAG context.
        /// </summary>
        public static int RAGTopK
        {
            get => EditorPrefs.GetInt(PREFIX + "RAGTopK", 3);
            set => EditorPrefs.SetInt(PREFIX + "RAGTopK", Mathf.Clamp(value, 1, 10));
        }
        
        /// <summary>
        /// Minimum relevance score (0-1) for chunks to be included in RAG context.
        /// </summary>
        public static float RAGMinRelevance
        {
            get => EditorPrefs.GetFloat(PREFIX + "RAGMinRelevance", 0.25f);
            set => EditorPrefs.SetFloat(PREFIX + "RAGMinRelevance", Mathf.Clamp01(value));
        }
        
        // RAG TopK options for UI
        public static readonly int[] RAGTopKOptions = { 1, 2, 3, 5, 7, 10 };
        public static readonly string[] RAGTopKLabels = { "1 (Minimal)", "2 (Light)", "3 (Standard)", "5 (More)", "7 (Extensive)", "10 (Maximum)" };
        
        public static int GetRAGTopKIndex()
        {
            int current = RAGTopK;
            for (int i = 0; i < RAGTopKOptions.Length; i++)
            {
                if (RAGTopKOptions[i] == current) return i;
            }
            return 2; // Default to 3 (Index 2)
        }
    }
}
