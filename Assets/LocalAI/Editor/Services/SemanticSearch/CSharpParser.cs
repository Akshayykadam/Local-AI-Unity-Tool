using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Represents a chunk of code extracted from a C# file.
    /// </summary>
    [Serializable]
    public struct CodeChunk
    {
        public string FilePath;
        public int StartLine;
        public int EndLine;
        public string Type;        // "class", "method", "property", "field"
        public string Name;        // e.g., "PlayerController.Move"
        public string Content;     // Actual code content
        public string Summary;     // XML comment if present
        public string ChunkId;     // Unique identifier for this chunk
        
        public override string ToString() => $"{Type}: {Name} ({FilePath}:{StartLine}-{EndLine})";
    }

    /// <summary>
    /// Parses C# files to extract structured metadata for semantic indexing.
    /// Uses regex-based parsing for speed and simplicity (no Roslyn dependency).
    /// </summary>
    public class CSharpParser
    {
        // Regex patterns for C# constructs
        private static readonly Regex ClassPattern = new Regex(
            @"(?<summary>///\s*<summary>[\s\S]*?</summary>\s*)?(?<modifiers>(?:public|private|protected|internal|static|abstract|sealed|partial)\s+)*class\s+(?<name>\w+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex MethodPattern = new Regex(
            @"(?<summary>///\s*<summary>[\s\S]*?</summary>\s*)?(?<modifiers>(?:public|private|protected|internal|static|virtual|override|abstract|async)\s+)*(?<returnType>[\w<>\[\],\s]+)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:\{|=>)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex PropertyPattern = new Regex(
            @"(?<summary>///\s*<summary>[\s\S]*?</summary>\s*)?(?<modifiers>(?:public|private|protected|internal|static|virtual|override|abstract)\s+)*(?<type>[\w<>\[\],\s]+)\s+(?<name>\w+)\s*\{\s*(?:get|set)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex XmlSummaryExtract = new Regex(
            @"<summary>\s*([\s\S]*?)\s*</summary>",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses a C# file and extracts code chunks for indexing.
        /// </summary>
        /// <param name="filePath">Absolute path to the .cs file</param>
        /// <returns>List of extracted code chunks</returns>
        public List<CodeChunk> ParseFile(string filePath)
        {
            var chunks = new List<CodeChunk>();
            
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SemanticSearch] File not found: {filePath}");
                return chunks;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                string[] lines = content.Split('\n');
                
                // Extract current class context
                string currentClass = ExtractClassName(content);
                
                // Parse classes
                foreach (Match match in ClassPattern.Matches(content))
                {
                    var chunk = CreateChunkFromMatch(match, filePath, lines, "class", null);
                    if (chunk.HasValue) chunks.Add(chunk.Value);
                }
                
                // Parse methods
                foreach (Match match in MethodPattern.Matches(content))
                {
                    var chunk = CreateChunkFromMatch(match, filePath, lines, "method", currentClass);
                    if (chunk.HasValue) chunks.Add(chunk.Value);
                }
                
                // Parse properties
                foreach (Match match in PropertyPattern.Matches(content))
                {
                    var chunk = CreateChunkFromMatch(match, filePath, lines, "property", currentClass);
                    if (chunk.HasValue) chunks.Add(chunk.Value);
                }
                
                // If no specific constructs found, treat entire file as one chunk
                if (chunks.Count == 0 && content.Length > 0)
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        StartLine = 1,
                        EndLine = lines.Length,
                        Type = "file",
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Content = content,
                        Summary = "",
                        ChunkId = GenerateChunkId(filePath, 1, lines.Length)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Error parsing {filePath}: {ex.Message}");
            }

            return chunks;
        }

        private CodeChunk? CreateChunkFromMatch(Match match, string filePath, string[] lines, string type, string parentClass)
        {
            try
            {
                int charIndex = match.Index;
                int startLine = GetLineNumber(lines, charIndex);
                
                // Find the end of this construct (matching braces)
                int endLine = FindEndOfBlock(lines, startLine - 1);
                
                // Extract the name
                string name = match.Groups["name"].Value;
                if (!string.IsNullOrEmpty(parentClass) && type != "class")
                {
                    name = $"{parentClass}.{name}";
                }
                
                // Extract XML summary if present
                string summary = "";
                if (match.Groups["summary"].Success)
                {
                    var summaryMatch = XmlSummaryExtract.Match(match.Groups["summary"].Value);
                    if (summaryMatch.Success)
                    {
                        summary = CleanXmlComment(summaryMatch.Groups[1].Value);
                    }
                }
                
                // Extract content (lines from start to end)
                string content = ExtractLines(lines, startLine - 1, endLine - 1);
                
                return new CodeChunk
                {
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    Type = type,
                    Name = name,
                    Content = content,
                    Summary = summary,
                    ChunkId = GenerateChunkId(filePath, startLine, endLine)
                };
            }
            catch
            {
                return null;
            }
        }

        private string ExtractClassName(string content)
        {
            var match = ClassPattern.Match(content);
            return match.Success ? match.Groups["name"].Value : "";
        }

        private int GetLineNumber(string[] lines, int charIndex)
        {
            int charCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                charCount += lines[i].Length + 1; // +1 for newline
                if (charCount > charIndex) return i + 1;
            }
            return lines.Length;
        }

        private int FindEndOfBlock(string[] lines, int startLineIndex)
        {
            int braceCount = 0;
            bool foundFirstBrace = false;
            
            for (int i = startLineIndex; i < lines.Length; i++)
            {
                string line = lines[i];
                
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        braceCount++;
                        foundFirstBrace = true;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (foundFirstBrace && braceCount == 0)
                        {
                            return i + 1; // 1-indexed
                        }
                    }
                }
                
                // Handle expression-bodied members (=>)
                if (!foundFirstBrace && line.Contains("=>"))
                {
                    // Find the semicolon
                    for (int j = i; j < lines.Length; j++)
                    {
                        if (lines[j].Contains(";"))
                        {
                            return j + 1;
                        }
                    }
                }
            }
            
            // Fallback: return 10 lines after start or end of file
            return Math.Min(startLineIndex + 10, lines.Length);
        }

        private string ExtractLines(string[] lines, int startIndex, int endIndex)
        {
            startIndex = Math.Max(0, startIndex);
            endIndex = Math.Min(lines.Length - 1, endIndex);
            
            var result = new System.Text.StringBuilder();
            for (int i = startIndex; i <= endIndex; i++)
            {
                result.AppendLine(lines[i]);
            }
            return result.ToString();
        }

        private string CleanXmlComment(string comment)
        {
            // Remove leading /// and whitespace
            return Regex.Replace(comment, @"^\s*///?\s*", "", RegexOptions.Multiline).Trim();
        }

        private string GenerateChunkId(string filePath, int startLine, int endLine)
        {
            string relativePath = filePath.Replace("\\", "/");
            if (relativePath.Contains("Assets/"))
            {
                relativePath = relativePath.Substring(relativePath.IndexOf("Assets/"));
            }
            return $"{relativePath}:{startLine}-{endLine}";
        }
    }
}
