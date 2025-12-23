using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LocalAI.Editor.Services.Refactoring
{
    /// <summary>
    /// Checks safety constraints before refactoring operations.
    /// </summary>
    public class RefactoringSafetyChecker
    {
        // Unity methods that should never be renamed
        private static readonly HashSet<string> UnityMagicMethods = new HashSet<string>
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable", "OnDestroy",
            "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
            "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D",
            "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay",
            "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D",
            "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit", "OnMouseOver", "OnMouseDrag",
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnValidate", "Reset", "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus",
            "OnBecameVisible", "OnBecameInvisible",
            "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage", "OnRenderObject",
            "OnAnimatorMove", "OnAnimatorIK",
            "OnControllerColliderHit", "OnJointBreak", "OnJointBreak2D",
            "OnParticleCollision", "OnParticleTrigger", "OnParticleSystemStopped",
            "OnTransformParentChanged", "OnTransformChildrenChanged",
            "OnWillRenderObject", "OnServerInitialized", "OnConnectedToServer",
            "OnPlayerConnected", "OnPlayerDisconnected", "OnDisconnectedFromServer"
        };

        // Patterns that indicate string-based method calls
        private static readonly string[] StringMethodCallPatterns = new[]
        {
            "Invoke(\"",
            "InvokeRepeating(\"",
            "CancelInvoke(\"",
            "SendMessage(\"",
            "SendMessageUpwards(\"",
            "BroadcastMessage(\"",
            "StartCoroutine(\"",
            "StopCoroutine(\""
        };

        /// <summary>
        /// Analyzes a refactoring operation and returns safety assessment.
        /// </summary>
        public SafetyReport CheckSafety(CodeSymbol symbol, RefactoringType operation, string newName = null)
        {
            var report = new SafetyReport
            {
                Symbol = symbol,
                Operation = operation,
                RiskLevel = RiskLevel.Low,
                Warnings = new List<string>(),
                Blockers = new List<string>()
            };

            // Check Unity lifecycle methods
            if (operation == RefactoringType.Rename && symbol.IsUnityLifecycle)
            {
                report.RiskLevel = RiskLevel.High;
                report.Blockers.Add($"'{symbol.Name}' is a Unity lifecycle method. Renaming will break Unity's automatic invocation.");
            }

            // Check serialized fields
            if (symbol.IsSerializedField && operation == RefactoringType.Rename)
            {
                report.RiskLevel = RiskLevel.High;
                report.Warnings.Add($"'{symbol.Name}' is a [SerializeField]. Renaming will break existing serialized data in scenes and prefabs.");
            }

            // Check for string-based method calls
            if (symbol.Type == SymbolType.Method && operation == RefactoringType.Rename)
            {
                var stringRefs = FindStringBasedReferences(symbol);
                if (stringRefs.Count > 0)
                {
                    report.RiskLevel = RiskLevel.Medium;
                    report.Warnings.Add($"Found {stringRefs.Count} potential string-based call(s) to '{symbol.Name}' (Invoke, SendMessage, etc.)");
                }
            }

            // Check public API
            if (symbol.Modifiers.Contains("public") && operation == RefactoringType.Rename)
            {
                if (report.RiskLevel < RiskLevel.Medium)
                    report.RiskLevel = RiskLevel.Medium;
                report.Warnings.Add($"'{symbol.Name}' is public. External code may depend on this name.");
            }

            // Check for reflection usage in the file
            if (HasReflectionUsage(symbol.FilePath))
            {
                report.Warnings.Add("This file uses reflection. Refactoring may break runtime lookups.");
            }

            return report;
        }

        /// <summary>
        /// Finds string-based method references across project.
        /// </summary>
        public List<StringMethodReference> FindStringBasedReferences(CodeSymbol method)
        {
            var references = new List<StringMethodReference>();
            
            string projectPath = Path.Combine(Application.dataPath);
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (var file in csFiles)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    string[] lines = content.Split('\n');
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        foreach (var pattern in StringMethodCallPatterns)
                        {
                            if (line.Contains(pattern) && line.Contains($"\"{method.Name}\""))
                            {
                                references.Add(new StringMethodReference
                                {
                                    FilePath = file,
                                    Line = i + 1,
                                    CallType = pattern.TrimEnd('(', '"'),
                                    Context = line.Trim()
                                });
                            }
                        }
                    }
                }
                catch { /* Skip unreadable files */ }
            }
            
            return references;
        }

        /// <summary>
        /// Checks if a file uses reflection.
        /// </summary>
        public bool HasReflectionUsage(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                return content.Contains("System.Reflection") ||
                       content.Contains("GetType()") ||
                       content.Contains("typeof(") ||
                       content.Contains(".GetMethod(") ||
                       content.Contains(".GetField(") ||
                       content.Contains(".GetProperty(");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that a new name is valid C# identifier.
        /// </summary>
        public bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            
            // Must start with letter or underscore
            if (!char.IsLetter(name[0]) && name[0] != '_') return false;
            
            // Rest must be letters, digits, or underscores
            return name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }
    }

    public enum RefactoringType
    {
        Rename,
        ExtractMethod,
        InlineMethod,
        SimplifyLogic,
        RemoveUnused
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High
    }

    public class SafetyReport
    {
        public CodeSymbol Symbol;
        public RefactoringType Operation;
        public RiskLevel RiskLevel;
        public List<string> Warnings;
        public List<string> Blockers;
        
        public bool CanProceed => Blockers.Count == 0;
        
        public string GetSummary()
        {
            if (Blockers.Count > 0)
                return $"BLOCKED: {Blockers[0]}";
            if (Warnings.Count > 0)
                return $"Risk: {RiskLevel} - {Warnings.Count} warning(s)";
            return $"Risk: {RiskLevel} - Safe to proceed";
        }
    }

    public class StringMethodReference
    {
        public string FilePath;
        public int Line;
        public string CallType;
        public string Context;
    }
}
