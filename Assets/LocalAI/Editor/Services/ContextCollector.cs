using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    public struct ContextData
    {
        public string FullText;
        public int TotalChars;
        public int MaxChars;
        public bool IsTruncated;
        public List<string> Warnings;
    }

    public class ContextCollector
    {
        private const int MAX_CHARS_PER_FILE = 2000;
        
        // Dynamic limit calculation
        private int GetMaxTotalContext()
        {
            // Calculate safe budget: ContextSize - MaxResponse - Reserve
            int contextSize = (int)LocalAISettings.ContextSize;
            int maxResponse = LocalAISettings.MaxTokens;
            int systemReserve = 500; // System instructions + overhead
            
            int availableTokens = contextSize - maxResponse - systemReserve;
            
            // Safety floor: If settings are aggressive (e.g. 2K context, 2K response), 
            // force at least 500 tokens for input to avoid total blockage (though it might still truncate heavily)
            if (availableTokens < 500) availableTokens = 500;
            
            // Convert to chars (Approx 3.5 chars per token is standard, using 3 for safety)
            return availableTokens * 3;
        }

        public ContextData CollectContext(bool includeLogs = false)
        {
            int maxLimit = GetMaxTotalContext();
            
            var data = new ContextData
            {
                MaxChars = maxLimit,
                Warnings = new List<string>()
            };
            
            StringBuilder sb = new StringBuilder();
            
            // 0. Append Logs (if requested)
            if (includeLogs)
            {
                string logs = LogCollector.GetCapturedLogs();
                if (!string.IsNullOrEmpty(logs))
                {
                    sb.AppendLine(logs);
                    sb.AppendLine("---");
                }
            }
            
            // 1. Project Assets (Scripts, Prefabs, Folders)
            Object[] selectedObjects = Selection.objects;
            
            // Prioritize Active GameObject if in Hierarchy and NOT in Project
            GameObject activeGO = Selection.activeGameObject;
            bool isHierarchySelection = activeGO != null && !AssetDatabase.Contains(activeGO);

            if (isHierarchySelection)
            {
                sb.AppendLine("[Selection Source: Hierarchy]");
                ExtractGameObject(activeGO, sb);
            }
            else if (selectedObjects != null && selectedObjects.Length > 0)
            {
                sb.AppendLine("[Selection Source: Project Assets]");
                foreach (Object obj in selectedObjects)
                {
                    if (sb.Length >= maxLimit) 
                    {
                        data.IsTruncated = true;
                        data.Warnings.Add("Max context limit reached");
                        break;
                    }
                    
                    string path = AssetDatabase.GetAssetPath(obj);
                    
                    if (obj is MonoScript script)
                    {
                        ExtractScript(script, path, sb, data.Warnings);
                    }
                    else if (obj is GameObject prefab)
                    {
                        ExtractPrefab(prefab, sb);
                    }
                    else if (AssetDatabase.IsValidFolder(path))
                    {
                        ExtractFolder(path, sb);
                    }
                    else
                    {
                        sb.AppendLine($"[Asset]: {obj.name} ({obj.GetType().Name})");
                    }
                    
                    sb.AppendLine("---");
                }
            }
            else
            {
                sb.AppendLine("[No Active Selection]");
            }
            
            // 2. Check Limits
            if (sb.Length > maxLimit)
            {
                data.IsTruncated = true;
                data.Warnings.Add("Total context truncated");
                string truncated = sb.ToString().Substring(0, maxLimit);
                data.FullText = truncated + "\n... [Context Truncated]";
            }
            else
            {
                data.FullText = sb.ToString();
            }

            data.TotalChars = data.FullText.Length;
            return data;
        }

        private void ExtractGameObject(GameObject go, StringBuilder sb)
        {
            sb.AppendLine($"GameObject: {go.name}");
            sb.AppendLine($"Hierarchy Path: {GetHierarchyPath(go.transform)}");
            sb.AppendLine($"Tag: {go.tag}, Layer: {LayerMask.LayerToName(go.layer)}");
            sb.AppendLine($"Active: {go.activeInHierarchy}");
            
            sb.AppendLine("[Components]:");
            Component[] components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue; // Missing script
                sb.AppendLine($"- {comp.GetType().Name}");
                
                // Extract public fields for MonoBehaviours (shallow)
                if (comp is MonoBehaviour mb)
                {
                    ExtractSerializedFields(mb, sb);
                }
            }
        }

        private void ExtractScript(MonoScript script, string path, StringBuilder sb, List<string> warnings)
        {
            sb.AppendLine($"Script: {script.name} ({path})");
            sb.AppendLine("```csharp");
            
            if (File.Exists(path))
            {
                try 
                {
                    string content = File.ReadAllText(path);
                    if (content.Length > MAX_CHARS_PER_FILE)
                    {
                        content = content.Substring(0, MAX_CHARS_PER_FILE) + "\n// ... [Code Truncated]";
                        warnings.Add($"{script.name} truncated (>2000 chars)");
                    }
                    sb.AppendLine(content);
                }
                catch
                {
                    sb.AppendLine("// Error reading file");
                }
            }
            sb.AppendLine("```");
        }

        private void ExtractSerializedFields(MonoBehaviour mb, StringBuilder sb)
        {
            SerializedObject so = new SerializedObject(mb);
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            
            int fieldCount = 0;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false; // Don't allow deep recursion
                if (prop.name == "m_Script") continue;
                
                string val = GetPropertyValue(prop);
                sb.AppendLine($"  - {prop.displayName}: {val}");
                
                fieldCount++;
                if (fieldCount > 10) 
                {
                    sb.AppendLine("  - ... (more fields hidden)");
                    break;
                }
            }
        }
        
        private string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F2");
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue ? prop.objectReferenceValue.name : "null";
                default: return $"[{prop.propertyType}]";
            }
        }

        private void ExtractPrefab(GameObject prefab, StringBuilder sb)
        {
            sb.AppendLine($"Prefab: {prefab.name}");
            sb.AppendLine("Root Components:");
            foreach (var comp in prefab.GetComponents<Component>())
            {
                if (comp != null) sb.AppendLine($"- {comp.GetType().Name}");
            }
        }


        private void ExtractFolder(string path, StringBuilder sb)
        {
            sb.AppendLine($"Folder: {path}");
            sb.AppendLine("Contents:");
            string[] files = Directory.GetFiles(path);
            foreach (string file in files.Take(10))
            {
                if (file.EndsWith(".meta")) continue;
                 sb.AppendLine($"- {Path.GetFileName(file)}");
            }
            if (files.Length > 10) sb.AppendLine("- ...");
        }

        private string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
