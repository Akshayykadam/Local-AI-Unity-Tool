using System;
using System.Runtime.InteropServices;
using System.Text;
using LocalAI.Runtime.Native;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Generates embeddings for code chunks using the local LLM.
    /// Supports both llama.cpp embedding mode and fallback TF-IDF.
    /// </summary>
    public class EmbeddingService : IDisposable
    {
        private const int EMBEDDING_DIM = 384; // Default for small models
        
        private IntPtr _model = IntPtr.Zero;
        private IntPtr _ctx = IntPtr.Zero;
        private bool _useSimpleEmbeddings = true; // Fallback mode
        private int _embeddingDim;
        
        public int EmbeddingDimension => _embeddingDim;
        public bool IsInitialized => _useSimpleEmbeddings || _ctx != IntPtr.Zero;
        
        public EmbeddingService()
        {
            _embeddingDim = EMBEDDING_DIM;
        }
        
        /// <summary>
        /// Initializes the embedding service with a model file.
        /// If no embedding model is available, falls back to simple embeddings.
        /// </summary>
        public bool Initialize(string embeddingModelPath = null)
        {
            // For now, we use simple embeddings (TF-IDF style)
            // Future: Support llama.cpp embedding models
            
            if (string.IsNullOrEmpty(embeddingModelPath) || !System.IO.File.Exists(embeddingModelPath))
            {
                Debug.Log("[SemanticSearch] Using simple keyword-based embeddings (no embedding model)");
                _useSimpleEmbeddings = true;
                return true;
            }
            
            // TODO: Initialize llama.cpp with embedding model
            // This would use llama_get_embeddings() after tokenization and decode
            _useSimpleEmbeddings = true;
            return true;
        }
        
        /// <summary>
        /// Generates an embedding vector for the given text.
        /// </summary>
        public float[] GenerateEmbedding(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new float[_embeddingDim];
            }
            
            if (_useSimpleEmbeddings)
            {
                return GenerateSimpleEmbedding(text);
            }
            
            return GenerateLLMEmbedding(text);
        }
        
        /// <summary>
        /// Generates a simple keyword-based embedding using term frequency.
        /// This is a lightweight alternative when no embedding model is available.
        /// </summary>
        private float[] GenerateSimpleEmbedding(string text)
        {
            float[] embedding = new float[_embeddingDim];
            
            // Normalize text
            text = text.ToLowerInvariant();
            
            // Extract code-relevant tokens
            var tokens = ExtractCodeTokens(text);
            
            // Generate feature vector using hash-based projection
            foreach (var token in tokens)
            {
                // Use stable hash to map token to embedding dimensions
                int hash = GetStableHash(token);
                
                // Project to multiple dimensions for better distribution
                for (int i = 0; i < 8; i++)
                {
                    int dim = Math.Abs((hash + i * 31) % _embeddingDim);
                    float sign = ((hash >> i) & 1) == 0 ? 1f : -1f;
                    embedding[dim] += sign * 0.1f;
                }
            }
            
            // Add structural features
            AddStructuralFeatures(embedding, text);
            
            // Normalize
            NormalizeVector(embedding);
            
            return embedding;
        }
        
        /// <summary>
        /// Generates embeddings using llama.cpp (when available).
        /// </summary>
        private float[] GenerateLLMEmbedding(string text)
        {
            // TODO: Implement llama.cpp embedding extraction
            // 1. Tokenize text
            // 2. Decode tokens
            // 3. Call llama_get_embeddings()
            // 4. Extract and return embedding vector
            
            // Fallback to simple for now
            return GenerateSimpleEmbedding(text);
        }
        
        private string[] ExtractCodeTokens(string text)
        {
            // Split by non-alphanumeric characters
            var parts = text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '(', ')', '{', '}', '[', ']', ';', ':', '<', '>', '=', '+', '-', '*', '/', '&', '|', '!', '?', '"', '\'' }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            var tokens = new System.Collections.Generic.List<string>();
            
            foreach (var part in parts)
            {
                // Skip very short or very long tokens
                if (part.Length < 2 || part.Length > 50) continue;
                
                // Skip common C# keywords and noise
                if (IsCommonKeyword(part)) continue;
                
                tokens.Add(part);
                
                // Also add camelCase/PascalCase split parts
                var subParts = SplitCamelCase(part);
                tokens.AddRange(subParts);
            }
            
            return tokens.ToArray();
        }
        
        private string[] SplitCamelCase(string input)
        {
            var result = new System.Collections.Generic.List<string>();
            var current = new StringBuilder();
            
            foreach (char c in input)
            {
                if (char.IsUpper(c) && current.Length > 0)
                {
                    result.Add(current.ToString().ToLowerInvariant());
                    current.Clear();
                }
                current.Append(c);
            }
            
            if (current.Length > 1)
            {
                result.Add(current.ToString().ToLowerInvariant());
            }
            
            return result.ToArray();
        }
        
        private bool IsCommonKeyword(string token)
        {
            var keywords = new[] { 
                "public", "private", "protected", "internal", "static", "void", "return",
                "class", "struct", "interface", "namespace", "using", "new", "this",
                "if", "else", "for", "foreach", "while", "do", "switch", "case", "break",
                "try", "catch", "finally", "throw", "var", "int", "float", "bool", "string",
                "true", "false", "null", "async", "await", "get", "set", "value"
            };
            
            foreach (var kw in keywords)
            {
                if (token == kw) return true;
            }
            return false;
        }
        
        private void AddStructuralFeatures(float[] embedding, string text)
        {
            // Add features based on code structure
            int dim = 0;
            
            // Method indicators
            if (text.Contains("void ") || text.Contains("return "))
                embedding[(dim++) % _embeddingDim] += 0.2f;
                
            // Class indicators
            if (text.Contains("class ") || text.Contains("struct "))
                embedding[(dim++) % _embeddingDim] += 0.2f;
                
            // Unity-specific indicators
            if (text.Contains("MonoBehaviour") || text.Contains("GameObject"))
                embedding[(dim + 10) % _embeddingDim] += 0.3f;
            if (text.Contains("Update") || text.Contains("FixedUpdate"))
                embedding[(dim + 11) % _embeddingDim] += 0.3f;
            if (text.Contains("Start") || text.Contains("Awake"))
                embedding[(dim + 12) % _embeddingDim] += 0.3f;
            if (text.Contains("Coroutine") || text.Contains("yield"))
                embedding[(dim + 13) % _embeddingDim] += 0.3f;
            if (text.Contains("Transform") || text.Contains("Vector3"))
                embedding[(dim + 14) % _embeddingDim] += 0.3f;
            if (text.Contains("Rigidbody") || text.Contains("Physics"))
                embedding[(dim + 15) % _embeddingDim] += 0.3f;
        }
        
        private int GetStableHash(string str)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in str)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
        
        private void NormalizeVector(float[] vector)
        {
            float magnitude = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                magnitude += vector[i] * vector[i];
            }
            
            magnitude = (float)Math.Sqrt(magnitude);
            
            if (magnitude > 0.0001f)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= magnitude;
                }
            }
        }
        
        /// <summary>
        /// Computes cosine similarity between two embedding vectors.
        /// </summary>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;
            
            float dot = 0, magA = 0, magB = 0;
            
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            
            float magnitude = (float)(Math.Sqrt(magA) * Math.Sqrt(magB));
            return magnitude > 0.0001f ? dot / magnitude : 0;
        }
        
        public void Dispose()
        {
            if (_ctx != IntPtr.Zero)
            {
                // Free context if using LLM embeddings
                _ctx = IntPtr.Zero;
            }
            if (_model != IntPtr.Zero)
            {
                _model = IntPtr.Zero;
            }
        }
    }
}
