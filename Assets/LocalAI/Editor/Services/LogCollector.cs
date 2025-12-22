using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Helper to extract logs from the Unity Console via Reflection.
    /// Accesses the internal UnityEditor.LogEntries class.
    /// </summary>
    public static class LogCollector
    {
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryMethod;
        private static object _logEntryContainer;
        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;

            try
            {
                Assembly unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
                Type logEntriesType = unityEditorAssembly.GetType("UnityEditor.LogEntries");
                Type logEntryType = unityEditorAssembly.GetType("UnityEditor.LogEntry");

                if (logEntriesType != null && logEntryType != null)
                {
                    _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                    _getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                    _logEntryContainer = Activator.CreateInstance(logEntryType);
                    _initialized = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalAI] LogCollector reflection failed: {e.Message}");
            }
        }

        public static string GetCapturedLogs(int maxErrors = 5)
        {
            if (!_initialized) Initialize();
            if (!_initialized) return "[Log Collector Failed to Initialize]";

            try
            {
                int count = (int)_getCountMethod.Invoke(null, null);
                if (count == 0) return "";

                StringBuilder sb = new StringBuilder();
                int errorsFound = 0;
                
                // Iterate backwards to find recent errors
                for (int i = count - 1; i >= 0; i--)
                {
                    if (errorsFound >= maxErrors) break;
                    
                    // Retrieve entry
                    _getEntryMethod.Invoke(null, new object[] { i, _logEntryContainer });
                    
                    // Read fields via reflection or dynamic
                    // UnityEditor.LogEntry has fields: message, file, line, mode, etc.
                    
                    // Optimization: Cast to dynamic to avoid verbose reflection
                    /* 
                       Note: Since LogEntry is internal, we can't cast to it directly. 
                       We use reflection on the container instance.
                    */
                    
                    int mode = (int)_logEntryContainer.GetType().GetField("mode").GetValue(_logEntryContainer);
                    
                    // Check if Error (Mode.Error = 1, Assert = 2, Log = 4, etc.)
                   // Simplified check: usually odd numbers are errors/asserts in Unity's bitmask, but let's check basic flags
                   // Console flags: Error=1, Assert=2, Fatal=16, AssetImportError=64, ScriptingError=128
                   
                    bool isError = (mode & (1 | 2 | 16 | 64 | 128)) != 0;
                    
                    if (isError)
                    {
                        string message = (string)_logEntryContainer.GetType().GetField("message").GetValue(_logEntryContainer);
                        string file = (string)_logEntryContainer.GetType().GetField("file").GetValue(_logEntryContainer);
                        int line = (int)_logEntryContainer.GetType().GetField("line").GetValue(_logEntryContainer);
                        
                        // Clean up message (first line usually sufficient for ID, but full message needed for context)
                        // If message is super long, truncate?
                        if (message.Length > 500) message = message.Substring(0, 500) + "...";

                        sb.AppendLine($"[Error] {message}");
                        if (!string.IsNullOrEmpty(file))
                        {
                            sb.AppendLine($"Code Location: {file}:{line}");
                        }
                        sb.AppendLine("---");
                        errorsFound++;
                    }
                }
                
                if (sb.Length > 0)
                {
                    return "[Console Errors (Recent)]:\n" + sb.ToString();
                }
            }
            catch (Exception e)
            {
                return $"[Error reading logs: {e.Message}]";
            }
            
            return "";
        }
    }
}
