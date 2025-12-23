using System;
using System.Collections.Generic;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Splits large code chunks into smaller pieces suitable for embedding.
    /// Ensures chunks don't exceed token limits while maintaining context.
    /// </summary>
    public class CodeChunker
    {
        // Approximate: 1 token ≈ 4 characters for code
        private const int CHARS_PER_TOKEN = 4;
        
        private readonly int _maxChunkTokens;
        private readonly int _overlapTokens;
        
        /// <summary>
        /// Creates a new CodeChunker with specified limits.
        /// </summary>
        /// <param name="maxChunkTokens">Maximum tokens per chunk (default: 512)</param>
        /// <param name="overlapTokens">Token overlap between chunks (default: 50)</param>
        public CodeChunker(int maxChunkTokens = 512, int overlapTokens = 50)
        {
            _maxChunkTokens = maxChunkTokens;
            _overlapTokens = overlapTokens;
        }
        
        /// <summary>
        /// Splits a code chunk into smaller sub-chunks if it exceeds the token limit.
        /// </summary>
        /// <param name="chunk">Original code chunk</param>
        /// <returns>List of sub-chunks (may be single item if small enough)</returns>
        public List<CodeChunk> SplitChunk(CodeChunk chunk)
        {
            var result = new List<CodeChunk>();
            
            int maxChars = _maxChunkTokens * CHARS_PER_TOKEN;
            int overlapChars = _overlapTokens * CHARS_PER_TOKEN;
            
            // If chunk is small enough, return as-is
            if (chunk.Content.Length <= maxChars)
            {
                result.Add(chunk);
                return result;
            }
            
            // Split into overlapping sub-chunks
            string content = chunk.Content;
            string[] lines = content.Split('\n');
            
            int currentStart = 0;
            int subChunkIndex = 0;
            
            while (currentStart < lines.Length)
            {
                // Find end position for this chunk
                int charCount = 0;
                int endLine = currentStart;
                
                for (int i = currentStart; i < lines.Length; i++)
                {
                    charCount += lines[i].Length + 1;
                    if (charCount > maxChars && i > currentStart)
                    {
                        break;
                    }
                    endLine = i;
                }
                
                // Extract sub-chunk content
                var subContent = new System.Text.StringBuilder();
                for (int i = currentStart; i <= endLine; i++)
                {
                    subContent.AppendLine(lines[i]);
                }
                
                // Calculate actual line numbers in original file
                int actualStartLine = chunk.StartLine + currentStart;
                int actualEndLine = chunk.StartLine + endLine;
                
                result.Add(new CodeChunk
                {
                    FilePath = chunk.FilePath,
                    StartLine = actualStartLine,
                    EndLine = actualEndLine,
                    Type = chunk.Type,
                    Name = subChunkIndex == 0 ? chunk.Name : $"{chunk.Name} (part {subChunkIndex + 1})",
                    Content = subContent.ToString(),
                    Summary = subChunkIndex == 0 ? chunk.Summary : "", // Only first chunk gets summary
                    ChunkId = $"{chunk.ChunkId}#{subChunkIndex}"
                });
                
                // Move to next chunk with overlap
                int overlapLines = EstimateOverlapLines(lines, currentStart, endLine, overlapChars);
                currentStart = endLine + 1 - overlapLines;
                
                // Prevent infinite loop
                if (currentStart <= (subChunkIndex > 0 ? currentStart : 0))
                {
                    currentStart = endLine + 1;
                }
                
                subChunkIndex++;
                
                // Safety limit
                if (subChunkIndex > 100)
                {
                    UnityEngine.Debug.LogWarning($"[SemanticSearch] Chunk limit reached for {chunk.Name}");
                    break;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Processes a list of chunks, splitting any that are too large.
        /// </summary>
        public List<CodeChunk> ProcessChunks(List<CodeChunk> chunks)
        {
            var result = new List<CodeChunk>();
            
            foreach (var chunk in chunks)
            {
                result.AddRange(SplitChunk(chunk));
            }
            
            return result;
        }
        
        private int EstimateOverlapLines(string[] lines, int start, int end, int targetChars)
        {
            int charCount = 0;
            int lineCount = 0;
            
            for (int i = end; i >= start && charCount < targetChars; i--)
            {
                charCount += lines[i].Length + 1;
                lineCount++;
            }
            
            return Math.Max(1, lineCount);
        }
        
        /// <summary>
        /// Estimates the token count for a given text.
        /// </summary>
        public int EstimateTokens(string text)
        {
            return text.Length / CHARS_PER_TOKEN;
        }
    }
}
