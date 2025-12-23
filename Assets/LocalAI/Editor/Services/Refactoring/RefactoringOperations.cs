using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services.Refactoring
{
    /// <summary>
    /// Base class for refactoring operations.
    /// </summary>
    public abstract class RefactoringOperation
    {
        public CodeSymbol TargetSymbol { get; protected set; }
        public SafetyReport SafetyReport { get; protected set; }
        public List<FileChange> Changes { get; protected set; }
        
        protected readonly SymbolResolver _resolver;
        protected readonly RefactoringSafetyChecker _safetyChecker;
        
        protected RefactoringOperation(SymbolResolver resolver)
        {
            _resolver = resolver;
            _safetyChecker = new RefactoringSafetyChecker();
            Changes = new List<FileChange>();
        }

        /// <summary>
        /// Prepares the refactoring and returns a preview.
        /// </summary>
        public abstract RefactoringPreview Prepare();

        /// <summary>
        /// Applies the refactoring changes.
        /// </summary>
        public virtual bool Apply()
        {
            if (Changes.Count == 0)
            {
                Debug.LogWarning("[Refactoring] No changes to apply");
                return false;
            }

            // Create backups
            var backups = new Dictionary<string, string>();
            foreach (var change in Changes)
            {
                if (!backups.ContainsKey(change.FilePath))
                {
                    backups[change.FilePath] = File.ReadAllText(change.FilePath);
                }
            }

            try
            {
                // Group changes by file
                var changesByFile = new Dictionary<string, List<FileChange>>();
                foreach (var change in Changes)
                {
                    if (!changesByFile.ContainsKey(change.FilePath))
                        changesByFile[change.FilePath] = new List<FileChange>();
                    changesByFile[change.FilePath].Add(change);
                }

                // Apply changes file by file
                foreach (var kvp in changesByFile)
                {
                    ApplyChangesToFile(kvp.Key, kvp.Value);
                }

                AssetDatabase.Refresh();
                Debug.Log($"[Refactoring] Applied {Changes.Count} changes to {changesByFile.Count} files");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Refactoring] Failed to apply changes: {ex.Message}");
                
                // Restore backups
                foreach (var kvp in backups)
                {
                    File.WriteAllText(kvp.Key, kvp.Value);
                }
                
                return false;
            }
        }

        private void ApplyChangesToFile(string filePath, List<FileChange> changes)
        {
            string content = File.ReadAllText(filePath);
            
            // Sort changes by position, descending (apply from end to start)
            changes.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));
            
            foreach (var change in changes)
            {
                if (change.ChangeType == ChangeType.Replace)
                {
                    content = content.Substring(0, change.StartIndex) + 
                              change.NewText + 
                              content.Substring(change.EndIndex);
                }
                else if (change.ChangeType == ChangeType.Insert)
                {
                    content = content.Insert(change.StartIndex, change.NewText);
                }
                else if (change.ChangeType == ChangeType.Delete)
                {
                    content = content.Remove(change.StartIndex, change.EndIndex - change.StartIndex);
                }
            }
            
            File.WriteAllText(filePath, content);
        }
    }

    /// <summary>
    /// Rename refactoring operation.
    /// </summary>
    public class RenameOperation : RefactoringOperation
    {
        private readonly string _newName;
        
        public RenameOperation(SymbolResolver resolver, CodeSymbol symbol, string newName) : base(resolver)
        {
            TargetSymbol = symbol;
            _newName = newName;
        }

        public override RefactoringPreview Prepare()
        {
            Changes.Clear();
            
            // Safety check
            SafetyReport = _safetyChecker.CheckSafety(TargetSymbol, RefactoringType.Rename, _newName);
            
            if (!_safetyChecker.IsValidIdentifier(_newName))
            {
                SafetyReport.Blockers.Add($"'{_newName}' is not a valid C# identifier");
            }
            
            var preview = new RefactoringPreview
            {
                Operation = RefactoringType.Rename,
                TargetSymbol = TargetSymbol,
                SafetyReport = SafetyReport,
                FilesAffected = new List<string>(),
                DiffPreviews = new List<DiffPreview>()
            };
            
            if (!SafetyReport.CanProceed)
            {
                return preview;
            }

            // Find all references
            var references = _resolver.GetReferences(TargetSymbol.Name);
            
            // Build file content cache
            var fileContents = new Dictionary<string, string>();
            var filesToChange = new HashSet<string>();
            filesToChange.Add(TargetSymbol.FilePath);
            
            foreach (var reference in references)
            {
                filesToChange.Add(reference.FilePath);
            }
            
            foreach (var file in filesToChange)
            {
                fileContents[file] = File.ReadAllText(file);
            }
            
            // Find and create changes for each file
            foreach (var file in filesToChange)
            {
                string content = fileContents[file];
                string originalContent = content;
                
                // Find all occurrences of the symbol name as a whole word
                var pattern = new Regex($@"\b{Regex.Escape(TargetSymbol.Name)}\b");
                var matches = pattern.Matches(content);
                
                foreach (Match match in matches)
                {
                    Changes.Add(new FileChange
                    {
                        FilePath = file,
                        ChangeType = ChangeType.Replace,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        OldText = TargetSymbol.Name,
                        NewText = _newName
                    });
                }
                
                // Generate diff preview
                string newContent = pattern.Replace(content, _newName);
                if (newContent != originalContent)
                {
                    preview.FilesAffected.Add(file);
                    preview.DiffPreviews.Add(new DiffPreview
                    {
                        FilePath = file,
                        OriginalContent = originalContent,
                        NewContent = newContent
                    });
                }
            }
            
            preview.TotalChanges = Changes.Count;
            return preview;
        }
    }

    /// <summary>
    /// Extract method refactoring operation.
    /// </summary>
    public class ExtractMethodOperation : RefactoringOperation
    {
        private readonly string _selectedCode;
        private readonly string _newMethodName;
        private readonly int _insertAfterLine;
        
        public ExtractMethodOperation(SymbolResolver resolver, CodeSymbol containingMethod, 
            string selectedCode, string newMethodName) : base(resolver)
        {
            TargetSymbol = containingMethod;
            _selectedCode = selectedCode;
            _newMethodName = newMethodName;
            _insertAfterLine = containingMethod.EndLine;
        }

        public override RefactoringPreview Prepare()
        {
            Changes.Clear();
            
            SafetyReport = _safetyChecker.CheckSafety(TargetSymbol, RefactoringType.ExtractMethod);
            
            var preview = new RefactoringPreview
            {
                Operation = RefactoringType.ExtractMethod,
                TargetSymbol = TargetSymbol,
                SafetyReport = SafetyReport,
                FilesAffected = new List<string> { TargetSymbol.FilePath },
                DiffPreviews = new List<DiffPreview>()
            };

            if (!_safetyChecker.IsValidIdentifier(_newMethodName))
            {
                SafetyReport.Blockers.Add($"'{_newMethodName}' is not a valid method name");
                return preview;
            }

            string content = File.ReadAllText(TargetSymbol.FilePath);
            string[] lines = content.Split('\n');
            
            // Detect indent of original code
            string firstLine = _selectedCode.TrimStart();
            int originalIndent = _selectedCode.Length - _selectedCode.TrimStart().Length;
            string baseIndent = new string(' ', 8); // Standard method indent
            
            // Create new method
            var newMethod = new StringBuilder();
            newMethod.AppendLine();
            newMethod.AppendLine($"{baseIndent}private void {_newMethodName}()");
            newMethod.AppendLine($"{baseIndent}{{");
            
            // Re-indent selected code
            string[] selectedLines = _selectedCode.Split('\n');
            foreach (var line in selectedLines)
            {
                string trimmed = line.TrimStart();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    newMethod.AppendLine($"{baseIndent}    {trimmed}");
                }
            }
            
            newMethod.AppendLine($"{baseIndent}}}");
            
            // Find insertion point (after the containing method)
            int insertIndex = 0;
            for (int i = 0; i < _insertAfterLine && i < lines.Length; i++)
            {
                insertIndex += lines[i].Length + 1;
            }
            
            Changes.Add(new FileChange
            {
                FilePath = TargetSymbol.FilePath,
                ChangeType = ChangeType.Insert,
                StartIndex = insertIndex,
                NewText = newMethod.ToString()
            });
            
            // Replace selected code with method call
            int selectionStart = content.IndexOf(_selectedCode);
            if (selectionStart >= 0)
            {
                Changes.Add(new FileChange
                {
                    FilePath = TargetSymbol.FilePath,
                    ChangeType = ChangeType.Replace,
                    StartIndex = selectionStart,
                    EndIndex = selectionStart + _selectedCode.Length,
                    OldText = _selectedCode,
                    NewText = $"{new string(' ', originalIndent)}{_newMethodName}();"
                });
            }
            
            // Generate preview
            string newContent = content;
            if (selectionStart >= 0)
            {
                newContent = newContent.Remove(selectionStart, _selectedCode.Length);
                newContent = newContent.Insert(selectionStart, $"{new string(' ', originalIndent)}{_newMethodName}();");
            }
            newContent = newContent.Insert(insertIndex, newMethod.ToString());
            
            preview.DiffPreviews.Add(new DiffPreview
            {
                FilePath = TargetSymbol.FilePath,
                OriginalContent = content,
                NewContent = newContent
            });
            
            preview.TotalChanges = Changes.Count;
            return preview;
        }
    }

    #region Supporting Types

    public class FileChange
    {
        public string FilePath;
        public ChangeType ChangeType;
        public int StartIndex;
        public int EndIndex;
        public string OldText;
        public string NewText;
    }

    public enum ChangeType
    {
        Replace,
        Insert,
        Delete
    }

    public class RefactoringPreview
    {
        public RefactoringType Operation;
        public CodeSymbol TargetSymbol;
        public SafetyReport SafetyReport;
        public List<string> FilesAffected;
        public List<DiffPreview> DiffPreviews;
        public int TotalChanges;
        
        public string GetSummary()
        {
            return $"{Operation} '{TargetSymbol.Name}' - {FilesAffected.Count} file(s), {TotalChanges} change(s)";
        }
    }

    public class DiffPreview
    {
        public string FilePath;
        public string OriginalContent;
        public string NewContent;
        
        /// <summary>
        /// Gets a simple unified diff for display.
        /// </summary>
        public string GetSimpleDiff(int contextLines = 3)
        {
            var sb = new StringBuilder();
            string[] oldLines = OriginalContent.Split('\n');
            string[] newLines = NewContent.Split('\n');
            
            sb.AppendLine($"--- {Path.GetFileName(FilePath)} (original)");
            sb.AppendLine($"+++ {Path.GetFileName(FilePath)} (modified)");
            
            // Simple line-by-line comparison
            int maxLines = Math.Max(oldLines.Length, newLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string oldLine = i < oldLines.Length ? oldLines[i] : "";
                string newLine = i < newLines.Length ? newLines[i] : "";
                
                if (oldLine != newLine)
                {
                    if (i < oldLines.Length)
                        sb.AppendLine($"-{oldLine}");
                    if (i < newLines.Length)
                        sb.AppendLine($"+{newLine}");
                }
            }
            
            return sb.ToString();
        }
    }

    #endregion
}
