using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
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
        
        // Context Size options
        public static readonly uint[] ContextSizeOptions = { 1024, 2048, 4096, 8192 };
        public static readonly string[] ContextSizeLabels = { "1K (Low RAM)", "2K (Normal)", "4K (Recommended)", "8K (Max)" };
        
        // Max Tokens options  
        public static readonly int[] MaxTokensOptions = { 128, 256, 512, 1024 };
        public static readonly string[] MaxTokensLabels = { "128 (Quick)", "256 (Normal)", "512 (Detailed)", "1024 (Very Long)" };
        
        public static uint ContextSize
        {
            get => (uint)EditorPrefs.GetInt(PREFIX + "ContextSize", (int)DEFAULT_CONTEXT_SIZE);
            set => EditorPrefs.SetInt(PREFIX + "ContextSize", (int)value);
        }
        
        public static int MaxTokens
        {
            get => EditorPrefs.GetInt(PREFIX + "MaxTokens", DEFAULT_MAX_TOKENS);
            set => EditorPrefs.SetInt(PREFIX + "MaxTokens", value);
        }
        
        public static int GetContextSizeIndex()
        {
            uint current = ContextSize;
            for (int i = 0; i < ContextSizeOptions.Length; i++)
            {
                if (ContextSizeOptions[i] == current) return i;
            }
            return 2; // Default to 4K
        }
        
        public static int GetMaxTokensIndex()
        {
            int current = MaxTokens;
            for (int i = 0; i < MaxTokensOptions.Length; i++)
            {
                if (MaxTokensOptions[i] == current) return i;
            }
            return 2; // Default to 512
        }
    }
}
