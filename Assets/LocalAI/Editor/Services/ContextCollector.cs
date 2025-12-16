using System.Text;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    public class ContextCollector
    {
        public string CollectContext()
        {
            StringBuilder sb = new StringBuilder();

            // 1. Selection
            var selection = Selection.activeGameObject;
            if (selection != null)
            {
                sb.AppendLine($"[Selected Object]: {selection.name}");
                sb.AppendLine("[Components]:");
                foreach (var component in selection.GetComponents<Component>())
                {
                    if (component == null) continue;
                    sb.AppendLine($"- {component.GetType().Name}");
                }
                
                // If it has a script, maybe read it? (Out of scope for simple version, but good for context)
            }
            else
            {
                sb.AppendLine("[No Object Selected]");
            }

            // 2. Console Logs (Last 5 errors/warnings if any)
            // Note: Getting full console history requires Reflection. 
            // For simplicity/safety in this version, we will just use a placeholder or hook into logMessageReceived if we want *future* logs.
            // But usually users want to ask about *past* errors.
            // Let's implement a simple reflective fetch if possible, or just skip for now to stick to "Stability".
            // "ContextCollector gathers data" -> "Explain Error" mode implies we have the error.
            
            // To be robust without fragile reflection, we might rely on the user copying the error, 
            // OR we rely on a log listener that we attach when the window is open.
            
            return sb.ToString();
        }

        // Optional: Call this from "active" mode
        public string GetSelectedError()
        {
             // Reflection wrapper for LogEntries usually goes here. 
             // Skipping for strict "don't crash" constraint unless needed. 
             // We'll return a placeholder instructions.
             return "Error context collection requires attaching a listener or manual input.";
        }
    }
}
