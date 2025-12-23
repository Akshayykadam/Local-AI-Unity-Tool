using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Service for generating NUnit unit tests from selected C# scripts.
    /// </summary>
    public class UnitTestGenerator
    {
        public struct MethodInfo
        {
            public string Name;
            public string ReturnType;
            public string Parameters;
            public string AccessModifier;
            public bool IsStatic;
        }

        private const string TEST_FOLDER_NAME = "Tests";
        private const string EDITOR_TEST_FOLDER = "Editor";

        /// <summary>
        /// Builds a specialized prompt for unit test generation.
        /// </summary>
        public string BuildTestPrompt(MonoScript script)
        {
            if (script == null)
                return "Error: No script selected. Please select a C# script in the Project window.";

            string path = AssetDatabase.GetAssetPath(script);
            string sourceCode = File.Exists(path) ? File.ReadAllText(path) : "";

            if (string.IsNullOrEmpty(sourceCode))
                return "Error: Could not read script contents.";

            string className = script.GetClass()?.Name ?? script.name;
            List<MethodInfo> methods = ExtractPublicMethods(sourceCode);
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Generate comprehensive NUnit unit tests for the following Unity C# class.");
            sb.AppendLine();
            sb.AppendLine($"**Class Name:** {className}");
            sb.AppendLine($"**Source File:** {path}");
            sb.AppendLine();
            
            if (methods.Count > 0)
            {
                sb.AppendLine("**Public Methods to Test:**");
                foreach (var method in methods)
                {
                    sb.AppendLine($"- {method.AccessModifier} {method.ReturnType} {method.Name}({method.Parameters})");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("**Source Code:**");
            sb.AppendLine("```csharp");
            
            if (sourceCode.Length > 3000)
            {
                sourceCode = sourceCode.Substring(0, 3000) + "\n// ... [truncated]";
            }
            sb.AppendLine(sourceCode);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("**Requirements:**");
            sb.AppendLine("1. Create a complete test class with proper namespace and using statements");
            sb.AppendLine("2. Use [TestFixture] and [Test] attributes");
            sb.AppendLine("3. Follow naming convention: MethodName_Scenario_ExpectedBehavior");
            sb.AppendLine("4. Include happy path, edge cases (null, empty, boundary values), and error cases");
            sb.AppendLine("5. Use Arrange-Act-Assert pattern with comments");
            sb.AppendLine("6. Make tests deterministic - no randomness or timing dependencies");
            
            return sb.ToString();
        }

        /// <summary>
        /// Determines the appropriate test file path for a given source script.
        /// </summary>
        public string GetTestFilePath(MonoScript script)
        {
            if (script == null) return null;

            string sourcePath = AssetDatabase.GetAssetPath(script);
            string sourceDir = Path.GetDirectoryName(sourcePath);
            string scriptName = script.name;

            string relativePath = "";
            if (sourceDir.Contains("Scripts"))
            {
                int scriptsIndex = sourceDir.IndexOf("Scripts", StringComparison.OrdinalIgnoreCase);
                if (scriptsIndex >= 0 && scriptsIndex + 7 < sourceDir.Length)
                {
                    relativePath = sourceDir.Substring(scriptsIndex + 7).TrimStart('/', '\\');
                }
            }

            string testFolder = Path.Combine("Assets", TEST_FOLDER_NAME, EDITOR_TEST_FOLDER, relativePath);
            string testFileName = $"{scriptName}Tests.cs";
            
            return Path.Combine(testFolder, testFileName);
        }

        /// <summary>
        /// Extracts public method signatures from C# source code.
        /// </summary>
        private List<MethodInfo> ExtractPublicMethods(string sourceCode)
        {
            var methods = new List<MethodInfo>();
            string pattern = @"(public|protected)\s+(static\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(([^)]*)\)";
            
            var matches = Regex.Matches(sourceCode, pattern);
            
            foreach (Match match in matches)
            {
                string methodName = match.Groups[4].Value;
                if (IsLifecycleMethod(methodName)) continue;
                if (methodName.StartsWith("get_") || methodName.StartsWith("set_")) continue;
                
                methods.Add(new MethodInfo
                {
                    AccessModifier = match.Groups[1].Value,
                    IsStatic = !string.IsNullOrEmpty(match.Groups[2].Value),
                    ReturnType = match.Groups[3].Value,
                    Name = methodName,
                    Parameters = match.Groups[5].Value.Trim()
                });
            }
            
            return methods;
        }

        private bool IsLifecycleMethod(string methodName)
        {
            string[] lifecycleMethods = {
                "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
                "OnEnable", "OnDisable", "OnDestroy", "OnValidate",
                "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
                "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay",
                "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
                "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D",
                "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D",
                "OnMouseEnter", "OnMouseExit", "OnMouseDown", "OnMouseUp",
                "OnBecameVisible", "OnBecameInvisible"
            };
            
            return Array.IndexOf(lifecycleMethods, methodName) >= 0;
        }

        /// <summary>
        /// Validates that a script is selected and suitable for test generation.
        /// </summary>
        public bool ValidateSelection(out MonoScript script, out string error)
        {
            script = null;
            error = null;

            UnityEngine.Object[] selectedObjects = Selection.objects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                error = "No script selected. Please select a C# script in the Project window.";
                return false;
            }

            foreach (var obj in selectedObjects)
            {
                if (obj is MonoScript ms)
                {
                    script = ms;
                    return true;
                }
            }

            error = "No C# script found in selection. Please select a .cs file.";
            return false;
        }
    }
}
