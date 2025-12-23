using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LocalAI.Editor.Services.Refactoring
{
    /// <summary>
    /// Represents a parsed symbol from C# code.
    /// </summary>
    public class CodeSymbol
    {
        public string Name;
        public SymbolType Type;
        public string FilePath;
        public int StartLine;
        public int EndLine;
        public string ParentClass;
        public string Signature;
        public string Content;
        public List<string> Modifiers = new List<string>();
        public List<string> Attributes = new List<string>();
        public bool IsUnityLifecycle;
        public bool IsSerializedField;
    }

    public enum SymbolType
    {
        Class,
        Struct,
        Interface,
        Enum,
        Method,
        Field,
        Property,
        Event
    }

    /// <summary>
    /// Parses C# files to extract symbols and structure.
    /// Uses regex-based parsing for offline operation.
    /// </summary>
    public class CodeAnalyzer
    {
        // Unity lifecycle methods
        private static readonly HashSet<string> UnityLifecycleMethods = new HashSet<string>
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable", "OnDestroy",
            "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
            "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D",
            "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay",
            "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D",
            "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit",
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnValidate", "Reset", "OnApplicationQuit", "OnApplicationPause"
        };

        // Regex patterns
        private static readonly Regex ClassPattern = new Regex(
            @"(?<attrs>(?:\[[^\]]+\]\s*)*)\s*(?<mods>(?:public|private|protected|internal|abstract|sealed|static|partial)\s+)*class\s+(?<name>\w+)(?:<[^>]+>)?(?:\s*:\s*(?<bases>[^{]+))?\s*\{",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex MethodPattern = new Regex(
            @"(?<attrs>(?:\[[^\]]+\]\s*)*)\s*(?<mods>(?:public|private|protected|internal|virtual|override|abstract|static|async)\s+)*(?<ret>[\w<>\[\],\s]+?)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:where[^{]+)?\s*\{",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex FieldPattern = new Regex(
            @"(?<attrs>(?:\[[^\]]+\]\s*)*)\s*(?<mods>(?:public|private|protected|internal|static|readonly|const)\s+)*(?<type>[\w<>\[\],\s]+?)\s+(?<name>\w+)\s*(?:=\s*[^;]+)?;",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex PropertyPattern = new Regex(
            @"(?<attrs>(?:\[[^\]]+\]\s*)*)\s*(?<mods>(?:public|private|protected|internal|virtual|override|abstract|static)\s+)*(?<type>[\w<>\[\],\s]+?)\s+(?<name>\w+)\s*\{\s*(?:get|set)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex SerializeFieldPattern = new Regex(
            @"\[SerializeField\]",
            RegexOptions.Compiled);

        /// <summary>
        /// Analyzes a C# file and extracts all symbols.
        /// </summary>
        public List<CodeSymbol> AnalyzeFile(string filePath)
        {
            var symbols = new List<CodeSymbol>();
            
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[CodeAnalyzer] File not found: {filePath}");
                return symbols;
            }

            try
            {
                string content = File.ReadAllText(filePath);
                string[] lines = content.Split('\n');

                // Extract classes
                symbols.AddRange(ExtractClasses(content, lines, filePath));
                
                // Extract methods within classes
                symbols.AddRange(ExtractMethods(content, lines, filePath));
                
                // Extract fields
                symbols.AddRange(ExtractFields(content, lines, filePath));
                
                // Extract properties
                symbols.AddRange(ExtractProperties(content, lines, filePath));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CodeAnalyzer] Error analyzing {filePath}: {ex.Message}");
            }

            return symbols;
        }

        private List<CodeSymbol> ExtractClasses(string content, string[] lines, string filePath)
        {
            var symbols = new List<CodeSymbol>();
            
            foreach (Match match in ClassPattern.Matches(content))
            {
                int startLine = GetLineNumber(content, match.Index);
                int endLine = FindClosingBrace(lines, startLine);
                
                var symbol = new CodeSymbol
                {
                    Name = match.Groups["name"].Value,
                    Type = SymbolType.Class,
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    Signature = match.Value.TrimEnd('{').Trim(),
                    Modifiers = ParseModifiers(match.Groups["mods"].Value),
                    Attributes = ParseAttributes(match.Groups["attrs"].Value)
                };
                
                symbols.Add(symbol);
            }
            
            return symbols;
        }

        private List<CodeSymbol> ExtractMethods(string content, string[] lines, string filePath)
        {
            var symbols = new List<CodeSymbol>();
            string currentClass = null;
            
            // Find current class context
            var classMatch = ClassPattern.Match(content);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups["name"].Value;
            }
            
            foreach (Match match in MethodPattern.Matches(content))
            {
                string name = match.Groups["name"].Value;
                int startLine = GetLineNumber(content, match.Index);
                int endLine = FindClosingBrace(lines, startLine);
                
                var symbol = new CodeSymbol
                {
                    Name = name,
                    Type = SymbolType.Method,
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = endLine,
                    ParentClass = currentClass,
                    Signature = $"{match.Groups["ret"].Value.Trim()} {name}({match.Groups["params"].Value})",
                    Modifiers = ParseModifiers(match.Groups["mods"].Value),
                    Attributes = ParseAttributes(match.Groups["attrs"].Value),
                    IsUnityLifecycle = UnityLifecycleMethods.Contains(name)
                };
                
                // Extract method body
                if (startLine > 0 && endLine > startLine && endLine <= lines.Length)
                {
                    symbol.Content = string.Join("\n", lines, startLine - 1, endLine - startLine + 1);
                }
                
                symbols.Add(symbol);
            }
            
            return symbols;
        }

        private List<CodeSymbol> ExtractFields(string content, string[] lines, string filePath)
        {
            var symbols = new List<CodeSymbol>();
            string currentClass = null;
            
            var classMatch = ClassPattern.Match(content);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups["name"].Value;
            }
            
            foreach (Match match in FieldPattern.Matches(content))
            {
                // Skip if it looks like a local variable (inside a method)
                int lineNum = GetLineNumber(content, match.Index);
                string line = lines[lineNum - 1];
                
                // Simple heuristic: skip if deeply indented (likely local var)
                int indent = line.Length - line.TrimStart().Length;
                if (indent > 12) continue;
                
                string attrs = match.Groups["attrs"].Value;
                bool isSerializedField = SerializeFieldPattern.IsMatch(attrs);
                
                var symbol = new CodeSymbol
                {
                    Name = match.Groups["name"].Value,
                    Type = SymbolType.Field,
                    FilePath = filePath,
                    StartLine = lineNum,
                    EndLine = lineNum,
                    ParentClass = currentClass,
                    Signature = $"{match.Groups["type"].Value.Trim()} {match.Groups["name"].Value}",
                    Modifiers = ParseModifiers(match.Groups["mods"].Value),
                    Attributes = ParseAttributes(attrs),
                    IsSerializedField = isSerializedField
                };
                
                symbols.Add(symbol);
            }
            
            return symbols;
        }

        private List<CodeSymbol> ExtractProperties(string content, string[] lines, string filePath)
        {
            var symbols = new List<CodeSymbol>();
            string currentClass = null;
            
            var classMatch = ClassPattern.Match(content);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups["name"].Value;
            }
            
            foreach (Match match in PropertyPattern.Matches(content))
            {
                int startLine = GetLineNumber(content, match.Index);
                
                var symbol = new CodeSymbol
                {
                    Name = match.Groups["name"].Value,
                    Type = SymbolType.Property,
                    FilePath = filePath,
                    StartLine = startLine,
                    EndLine = startLine, // Simplified - would need brace matching for full body
                    ParentClass = currentClass,
                    Signature = $"{match.Groups["type"].Value.Trim()} {match.Groups["name"].Value}",
                    Modifiers = ParseModifiers(match.Groups["mods"].Value),
                    Attributes = ParseAttributes(match.Groups["attrs"].Value)
                };
                
                symbols.Add(symbol);
            }
            
            return symbols;
        }

        /// <summary>
        /// Finds all references to a symbol across the project.
        /// </summary>
        public List<SymbolReference> FindReferences(string symbolName, string[] projectFiles)
        {
            var references = new List<SymbolReference>();
            var pattern = new Regex($@"\b{Regex.Escape(symbolName)}\b", RegexOptions.Compiled);
            
            foreach (string file in projectFiles)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    string[] lines = content.Split('\n');
                    
                    foreach (Match match in pattern.Matches(content))
                    {
                        int lineNum = GetLineNumber(content, match.Index);
                        
                        references.Add(new SymbolReference
                        {
                            FilePath = file,
                            Line = lineNum,
                            Column = match.Index - content.LastIndexOf('\n', match.Index) - 1,
                            Context = lines[lineNum - 1].Trim()
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CodeAnalyzer] Error scanning {file}: {ex.Message}");
                }
            }
            
            return references;
        }

        private int GetLineNumber(string content, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < content.Length; i++)
            {
                if (content[i] == '\n') line++;
            }
            return line;
        }

        private int FindClosingBrace(string[] lines, int startLine)
        {
            int braceCount = 0;
            bool started = false;
            
            for (int i = startLine - 1; i < lines.Length; i++)
            {
                string line = lines[i];
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        braceCount++;
                        started = true;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (started && braceCount == 0)
                        {
                            return i + 1;
                        }
                    }
                }
            }
            
            return startLine;
        }

        private List<string> ParseModifiers(string mods)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(mods)) return result;
            
            var parts = mods.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            result.AddRange(parts);
            return result;
        }

        private List<string> ParseAttributes(string attrs)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(attrs)) return result;
            
            var matches = Regex.Matches(attrs, @"\[([^\]]+)\]");
            foreach (Match m in matches)
            {
                result.Add(m.Groups[1].Value);
            }
            return result;
        }
    }

    /// <summary>
    /// Represents a reference to a symbol in code.
    /// </summary>
    public class SymbolReference
    {
        public string FilePath;
        public int Line;
        public int Column;
        public string Context;
    }
}
