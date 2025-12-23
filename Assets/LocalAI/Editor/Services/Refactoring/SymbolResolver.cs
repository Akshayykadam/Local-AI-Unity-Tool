using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LocalAI.Editor.Services.Refactoring
{
    /// <summary>
    /// Resolves symbols and builds relationship graphs across the project.
    /// </summary>
    public class SymbolResolver
    {
        private readonly CodeAnalyzer _analyzer;
        private Dictionary<string, CodeSymbol> _symbolTable;
        private Dictionary<string, List<string>> _callGraph;
        private Dictionary<string, List<SymbolReference>> _referenceCache;
        private string[] _projectFiles;
        
        public SymbolResolver()
        {
            _analyzer = new CodeAnalyzer();
            _symbolTable = new Dictionary<string, CodeSymbol>();
            _callGraph = new Dictionary<string, List<string>>();
            _referenceCache = new Dictionary<string, List<SymbolReference>>();
        }

        /// <summary>
        /// Scans project and builds symbol table.
        /// </summary>
        public void BuildSymbolTable(string projectPath)
        {
            _symbolTable.Clear();
            _callGraph.Clear();
            _referenceCache.Clear();
            
            _projectFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/Editor/") || f.Contains("/LocalAI/"))
                .ToArray();
            
            foreach (var file in _projectFiles)
            {
                var symbols = _analyzer.AnalyzeFile(file);
                foreach (var symbol in symbols)
                {
                    string key = GetSymbolKey(symbol);
                    _symbolTable[key] = symbol;
                }
            }
            
            Debug.Log($"[SymbolResolver] Built symbol table with {_symbolTable.Count} symbols from {_projectFiles.Length} files");
        }

        /// <summary>
        /// Gets a unique key for a symbol.
        /// </summary>
        public string GetSymbolKey(CodeSymbol symbol)
        {
            if (!string.IsNullOrEmpty(symbol.ParentClass))
                return $"{symbol.ParentClass}.{symbol.Name}";
            return symbol.Name;
        }

        /// <summary>
        /// Finds a symbol by name.
        /// </summary>
        public CodeSymbol FindSymbol(string name)
        {
            // Try exact match
            if (_symbolTable.TryGetValue(name, out var symbol))
                return symbol;
            
            // Try partial match (just the name without class)
            foreach (var kvp in _symbolTable)
            {
                if (kvp.Key.EndsWith($".{name}") || kvp.Value.Name == name)
                    return kvp.Value;
            }
            
            return null;
        }

        /// <summary>
        /// Finds all symbols matching a pattern.
        /// </summary>
        public List<CodeSymbol> SearchSymbols(string pattern, SymbolType? type = null)
        {
            var results = new List<CodeSymbol>();
            string lowerPattern = pattern.ToLower();
            
            foreach (var kvp in _symbolTable)
            {
                if (type.HasValue && kvp.Value.Type != type.Value)
                    continue;
                
                if (kvp.Value.Name.ToLower().Contains(lowerPattern))
                    results.Add(kvp.Value);
            }
            
            return results.OrderBy(s => s.Name).ToList();
        }

        /// <summary>
        /// Gets all references to a symbol.
        /// </summary>
        public List<SymbolReference> GetReferences(string symbolName)
        {
            if (_referenceCache.TryGetValue(symbolName, out var cached))
                return cached;
            
            var references = _analyzer.FindReferences(symbolName, _projectFiles);
            _referenceCache[symbolName] = references;
            return references;
        }

        /// <summary>
        /// Gets call hierarchy for a method.
        /// </summary>
        public CallHierarchy GetCallHierarchy(CodeSymbol method, int maxDepth = 3)
        {
            var hierarchy = new CallHierarchy
            {
                Symbol = method,
                Callers = new List<CallHierarchy>(),
                Callees = new List<CallHierarchy>()
            };

            if (method.Type != SymbolType.Method)
                return hierarchy;

            // Find callers (who calls this method)
            var references = GetReferences(method.Name);
            foreach (var reference in references)
            {
                // Skip the definition itself
                if (reference.FilePath == method.FilePath && reference.Line == method.StartLine)
                    continue;
                
                // Find the containing method
                var containingMethod = FindContainingMethod(reference.FilePath, reference.Line);
                if (containingMethod != null && containingMethod.Name != method.Name)
                {
                    hierarchy.Callers.Add(new CallHierarchy
                    {
                        Symbol = containingMethod,
                        Callers = new List<CallHierarchy>(),
                        Callees = new List<CallHierarchy>()
                    });
                }
            }

            // Find callees (what this method calls)
            if (!string.IsNullOrEmpty(method.Content))
            {
                foreach (var kvp in _symbolTable)
                {
                    if (kvp.Value.Type == SymbolType.Method && 
                        kvp.Value.Name != method.Name &&
                        method.Content.Contains(kvp.Value.Name + "("))
                    {
                        hierarchy.Callees.Add(new CallHierarchy
                        {
                            Symbol = kvp.Value,
                            Callers = new List<CallHierarchy>(),
                            Callees = new List<CallHierarchy>()
                        });
                    }
                }
            }

            return hierarchy;
        }

        /// <summary>
        /// Finds the method containing a specific line.
        /// </summary>
        public CodeSymbol FindContainingMethod(string filePath, int line)
        {
            foreach (var kvp in _symbolTable)
            {
                var symbol = kvp.Value;
                if (symbol.Type == SymbolType.Method &&
                    symbol.FilePath == filePath &&
                    symbol.StartLine <= line &&
                    symbol.EndLine >= line)
                {
                    return symbol;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all symbols in a file.
        /// </summary>
        public List<CodeSymbol> GetSymbolsInFile(string filePath)
        {
            return _symbolTable.Values
                .Where(s => s.FilePath == filePath)
                .OrderBy(s => s.StartLine)
                .ToList();
        }

        /// <summary>
        /// Gets all methods of a class.
        /// </summary>
        public List<CodeSymbol> GetClassMethods(string className)
        {
            return _symbolTable.Values
                .Where(s => s.ParentClass == className && s.Type == SymbolType.Method)
                .OrderBy(s => s.Name)
                .ToList();
        }

        /// <summary>
        /// Gets all fields of a class.
        /// </summary>
        public List<CodeSymbol> GetClassFields(string className)
        {
            return _symbolTable.Values
                .Where(s => s.ParentClass == className && s.Type == SymbolType.Field)
                .OrderBy(s => s.Name)
                .ToList();
        }

        public string[] ProjectFiles => _projectFiles;
        public int SymbolCount => _symbolTable.Count;
    }

    /// <summary>
    /// Represents a call hierarchy tree.
    /// </summary>
    public class CallHierarchy
    {
        public CodeSymbol Symbol;
        public List<CallHierarchy> Callers;
        public List<CallHierarchy> Callees;
        
        public string Format(int indent = 0)
        {
            var sb = new System.Text.StringBuilder();
            string prefix = new string(' ', indent * 2);
            
            sb.AppendLine($"{prefix}{Symbol.ParentClass}.{Symbol.Name}");
            
            if (Callers.Count > 0)
            {
                sb.AppendLine($"{prefix}  Called by:");
                foreach (var caller in Callers)
                {
                    sb.AppendLine($"{prefix}    - {caller.Symbol.ParentClass}.{caller.Symbol.Name}");
                }
            }
            
            if (Callees.Count > 0)
            {
                sb.AppendLine($"{prefix}  Calls:");
                foreach (var callee in Callees)
                {
                    sb.AppendLine($"{prefix}    - {callee.Symbol.ParentClass}.{callee.Symbol.Name}");
                }
            }
            
            return sb.ToString();
        }
    }
}
