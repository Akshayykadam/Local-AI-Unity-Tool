using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Parses and applies AI-generated code to script files.
    /// </summary>
    public static class ScriptApplicator
    {
        private static readonly Regex CodeBlockPattern = new Regex(
            @"```(?:csharp|cs|C#)?\s*\n([\s\S]*?)```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Extracts code blocks from AI response text.
        /// </summary>
        public static List<CodeBlock> ExtractCodeBlocks(string text)
        {
            var blocks = new List<CodeBlock>();
            
            foreach (Match match in CodeBlockPattern.Matches(text))
            {
                string code = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    blocks.Add(new CodeBlock
                    {
                        Code = code,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        ClassName = ExtractClassName(code)
                    });
                }
            }
            
            return blocks;
        }

        /// <summary>
        /// Checks if text contains any code blocks.
        /// </summary>
        public static bool HasCodeBlocks(string text)
        {
            return CodeBlockPattern.IsMatch(text);
        }

        /// <summary>
        /// Extracts class name from code.
        /// </summary>
        private static string ExtractClassName(string code)
        {
            var classMatch = Regex.Match(code, @"class\s+(\w+)");
            return classMatch.Success ? classMatch.Groups[1].Value : null;
        }

        /// <summary>
        /// Creates a new script file with the given code.
        /// </summary>
        public static bool CreateScript(string code, string path)
        {
            try
            {
                // Validate path
                if (!path.StartsWith("Assets/"))
                {
                    Debug.LogError("[ScriptApplicator] Path must start with Assets/");
                    return false;
                }
                
                if (!path.EndsWith(".cs"))
                {
                    path += ".cs";
                }
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                string fullDir = Path.Combine(Application.dataPath, "..", directory);
                if (!Directory.Exists(fullDir))
                {
                    Directory.CreateDirectory(fullDir);
                }
                
                // Write file
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                File.WriteAllText(fullPath, code);
                
                AssetDatabase.Refresh();
                
                Debug.Log($"[ScriptApplicator] Created script: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptApplicator] Failed to create script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates an existing script file, creating a backup first.
        /// </summary>
        public static bool UpdateScript(string code, string path)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                
                if (!File.Exists(fullPath))
                {
                    return CreateScript(code, path);
                }
                
                // Create backup
                string backupPath = fullPath + ".backup";
                File.Copy(fullPath, backupPath, true);
                Debug.Log($"[ScriptApplicator] Created backup: {backupPath}");
                
                // Write new content
                File.WriteAllText(fullPath, code);
                
                AssetDatabase.Refresh();
                
                Debug.Log($"[ScriptApplicator] Updated script: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptApplicator] Failed to update script: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores a script from backup.
        /// </summary>
        public static bool RestoreFromBackup(string path)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                string backupPath = fullPath + ".backup";
                
                if (!File.Exists(backupPath))
                {
                    Debug.LogWarning($"[ScriptApplicator] No backup found for: {path}");
                    return false;
                }
                
                File.Copy(backupPath, fullPath, true);
                AssetDatabase.Refresh();
                
                Debug.Log($"[ScriptApplicator] Restored from backup: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptApplicator] Failed to restore: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows a dialog to create or update a script.
        /// </summary>
        public static void ShowApplyDialog(string code)
        {
            string className = ExtractClassName(code) ?? "NewScript";
            
            string path = EditorUtility.SaveFilePanel(
                "Save Script",
                "Assets/Scripts",
                className,
                "cs"
            );
            
            if (string.IsNullOrEmpty(path))
                return;
            
            // Convert to relative path
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }
            
            if (File.Exists(Path.Combine(Application.dataPath, "..", path)))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "File Exists",
                    $"Do you want to overwrite {Path.GetFileName(path)}?\nA backup will be created.",
                    "Overwrite",
                    "Cancel"
                );
                
                if (overwrite)
                {
                    UpdateScript(code, path);
                }
            }
            else
            {
                CreateScript(code, path);
            }
        }

        /// <summary>
        /// Gets suggested script path based on code content.
        /// </summary>
        public static string GetSuggestedPath(string code)
        {
            string className = ExtractClassName(code);
            if (string.IsNullOrEmpty(className))
                className = "NewScript";
            
            return $"Assets/Scripts/{className}.cs";
        }

        /// <summary>
        /// Validates C# code syntax (basic check).
        /// </summary>
        public static ValidationResult ValidateCode(string code)
        {
            var result = new ValidationResult { IsValid = true, Errors = new List<string>() };
            
            // Check for basic structure
            if (!code.Contains("class ") && !code.Contains("struct ") && !code.Contains("interface "))
            {
                result.Errors.Add("No class, struct, or interface definition found");
            }
            
            // Check brace matching
            int openBraces = 0;
            int closeBraces = 0;
            foreach (char c in code)
            {
                if (c == '{') openBraces++;
                if (c == '}') closeBraces++;
            }
            
            if (openBraces != closeBraces)
            {
                result.Errors.Add($"Unmatched braces: {openBraces} open, {closeBraces} close");
            }
            
            // Check for common issues
            if (code.Contains("using UnityEngine;") && !code.Contains("UnityEngine"))
            {
                // OK - standard using
            }
            
            result.IsValid = result.Errors.Count == 0;
            return result;
        }
    }

    /// <summary>
    /// Represents an extracted code block.
    /// </summary>
    public class CodeBlock
    {
        public string Code;
        public int StartIndex;
        public int EndIndex;
        public string ClassName;
    }

    /// <summary>
    /// Result of code validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid;
        public List<string> Errors;
    }
}
