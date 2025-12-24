using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEditor;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// AI-powered action generation service.
    /// Sends requests to AI and parses structured action responses.
    /// </summary>
    public class AIActionService
    {
        private IInferenceService _inferenceService;
        
        private const string SYSTEM_PROMPT = @"You are a Unity scene builder assistant. Given a user request, output ONLY a JSON array of actions to create the requested scene setup.

Valid action types:
1. CreatePrimitive: Create a primitive GameObject
   { ""action"": ""CreatePrimitive"", ""type"": ""Cube|Sphere|Capsule|Cylinder|Plane"", ""name"": ""ObjectName"", ""position"": [x, y, z] }

2. CreateEmpty: Create an empty GameObject
   { ""action"": ""CreateEmpty"", ""name"": ""ObjectName"", ""position"": [x, y, z] }

3. AddComponent: Add a component to an existing object
   { ""action"": ""AddComponent"", ""target"": ""ObjectName"", ""component"": ""ComponentType"" }

4. SetParent: Set parent-child relationship
   { ""action"": ""SetParent"", ""child"": ""ChildName"", ""parent"": ""ParentName"" }

5. SetTransform: Modify position/rotation/scale
   { ""action"": ""SetTransform"", ""target"": ""ObjectName"", ""position"": [x,y,z], ""rotation"": [x,y,z], ""scale"": [x,y,z] }

6. SetMaterial: Apply a colored material
   { ""action"": ""SetMaterial"", ""target"": ""ObjectName"", ""color"": ""red|green|blue|yellow|white|black"" }

7. CreateScript: Create a C# script (provide full working code)
   { ""action"": ""CreateScript"", ""name"": ""ScriptName"", ""code"": ""using UnityEngine;..."" }

IMPORTANT RULES:
- Output ONLY valid JSON array, no explanations
- Create objects before referencing them
- Use realistic positions (player at y=1, objects above ground)
- For physics objects, add Rigidbody
- For triggers, add Collider and set isTrigger note in name
- Keep scripts simple and functional

Example request: ""Create a red bouncing ball""
Example response:
[
  { ""action"": ""CreatePrimitive"", ""type"": ""Sphere"", ""name"": ""Ball"", ""position"": [0, 5, 0] },
  { ""action"": ""AddComponent"", ""target"": ""Ball"", ""component"": ""Rigidbody"" },
  { ""action"": ""SetMaterial"", ""target"": ""Ball"", ""color"": ""red"" }
]";

        public AIActionService()
        {
            // Will be set when request is made
        }

        /// <summary>
        /// Sends a request to AI and returns list of actions to execute.
        /// </summary>
        public async void GenerateActions(string userRequest, Action<List<AIAction>> onComplete, Action<string> onError)
        {
            // Get current inference service based on settings
            _inferenceService = GetInferenceService();
            
            if (_inferenceService == null || !_inferenceService.IsReady)
            {
                onError?.Invoke("No AI provider configured. Please set up in Settings tab.");
                return;
            }

            string prompt = $"{SYSTEM_PROMPT}\n\nUser request: \"{userRequest}\"\n\nOutput JSON array:";
            
            try
            {
                var responseBuilder = new StringBuilder();
                var progress = new Progress<string>(token => {
                    responseBuilder.Append(token);
                });
                
                await _inferenceService.StartInferenceAsync(prompt, progress, CancellationToken.None);
                
                // Wait for any pending Progress callbacks to complete
                await System.Threading.Tasks.Task.Delay(100);
                
                string response = responseBuilder.ToString();
                
                // Strip status messages from the start
                if (response.StartsWith("[Connecting"))
                {
                    int jsonStart = response.IndexOf("\n[");
                    if (jsonStart > 0)
                        response = response.Substring(jsonStart + 1);
                    else
                    {
                        jsonStart = response.IndexOf("[{");
                        if (jsonStart > 0)
                            response = response.Substring(jsonStart);
                    }
                }
                
                Debug.Log($"[AIActionService] Final response ({response.Length} chars):\n{response}");
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    onError?.Invoke("AI returned empty response. Try a simpler request.");
                    return;
                }
                
                var actions = ParseActionsFromResponse(response);
                
                if (actions.Count == 0)
                {
                    Debug.LogWarning($"[AIActionService] No actions parsed from response. Raw:\n{response}");
                }
                
                onComplete?.Invoke(actions);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIActionService] Failed: {ex.Message}");
                onError?.Invoke($"AI error: {ex.Message}");
            }
        }

        private IInferenceService GetInferenceService()
        {
            switch (LocalAISettings.ActiveProvider)
            {
                case AIProvider.Gemini:
                    return new GeminiInferenceService();
                case AIProvider.OpenAI:
                    return new OpenAIInferenceService();
                case AIProvider.Claude:
                    return new ClaudeInferenceService();
                case AIProvider.Local:
                default:
                    var localService = new InferenceService();
                    var modelManager = new ModelManager();
                    localService.SetModelPath(modelManager.GetModelPath());
                    return localService;
            }
        }

        /// <summary>
        /// Parses JSON actions from AI response.
        /// </summary>
        private List<AIAction> ParseActionsFromResponse(string response)
        {
            var actions = new List<AIAction>();
            
            // Extract JSON array from response (AI might include extra text)
            string jsonArray = ExtractJsonArray(response);
            if (string.IsNullOrEmpty(jsonArray))
            {
                throw new Exception("No JSON array found in response");
            }

            // Parse using simple JSON parser (Unity's JsonUtility doesn't handle arrays well)
            var actionStrings = SplitJsonArray(jsonArray);
            
            foreach (var actionJson in actionStrings)
            {
                var action = ParseSingleAction(actionJson);
                if (action != null)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        private string ExtractJsonArray(string text)
        {
            // Find the first [ and last ]
            int start = text.IndexOf('[');
            int end = text.LastIndexOf(']');
            
            if (start >= 0 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }
            return null;
        }

        private List<string> SplitJsonArray(string jsonArray)
        {
            var items = new List<string>();
            int depth = 0;
            int itemStart = -1;
            
            for (int i = 0; i < jsonArray.Length; i++)
            {
                char c = jsonArray[i];
                
                if (c == '{')
                {
                    if (depth == 0) itemStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && itemStart >= 0)
                    {
                        items.Add(jsonArray.Substring(itemStart, i - itemStart + 1));
                        itemStart = -1;
                    }
                }
            }
            
            return items;
        }

        private AIAction ParseSingleAction(string json)
        {
            var action = new AIAction();
            
            // Parse action type
            action.ActionType = ExtractStringValue(json, "action");
            
            switch (action.ActionType)
            {
                case "CreatePrimitive":
                    action.PrimitiveType = ExtractStringValue(json, "type");
                    action.Name = ExtractStringValue(json, "name");
                    action.Position = ExtractVector3(json, "position");
                    break;
                    
                case "CreateEmpty":
                    action.Name = ExtractStringValue(json, "name");
                    action.Position = ExtractVector3(json, "position");
                    break;
                    
                case "AddComponent":
                    action.Target = ExtractStringValue(json, "target");
                    action.Component = ExtractStringValue(json, "component");
                    break;
                    
                case "SetParent":
                    action.Child = ExtractStringValue(json, "child");
                    action.Parent = ExtractStringValue(json, "parent");
                    break;
                    
                case "SetTransform":
                    action.Target = ExtractStringValue(json, "target");
                    action.Position = ExtractVector3(json, "position");
                    action.Rotation = ExtractVector3(json, "rotation");
                    action.Scale = ExtractVector3(json, "scale");
                    break;
                    
                case "SetMaterial":
                    action.Target = ExtractStringValue(json, "target");
                    action.Color = ExtractStringValue(json, "color");
                    break;
                    
                case "CreateScript":
                    action.Name = ExtractStringValue(json, "name");
                    action.Code = ExtractStringValue(json, "code");
                    break;
            }
            
            return action;
        }

        private string ExtractStringValue(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]*?)\"", RegexOptions.Singleline);
            return match.Success ? UnescapeJson(match.Groups[1].Value) : null;
        }

        private Vector3? ExtractVector3(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\\[\\s*(-?[\\d.]+)\\s*,\\s*(-?[\\d.]+)\\s*,\\s*(-?[\\d.]+)\\s*\\]");
            if (match.Success)
            {
                return new Vector3(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value)
                );
            }
            return null;
        }

        private string UnescapeJson(string value)
        {
            return value
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        /// <summary>
        /// Executes a list of AI actions.
        /// </summary>
        public static int ExecuteActions(List<AIAction> actions)
        {
            int executed = 0;
            var createdObjects = new Dictionary<string, GameObject>();

            foreach (var action in actions)
            {
                try
                {
                    if (ExecuteAction(action, createdObjects))
                    {
                        executed++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIActionService] Action failed: {action.ActionType} - {ex.Message}");
                }
            }

            return executed;
        }

        private static bool ExecuteAction(AIAction action, Dictionary<string, GameObject> createdObjects)
        {
            switch (action.ActionType)
            {
                case "CreatePrimitive":
                    PrimitiveType? primType = GetPrimitiveType(action.PrimitiveType);
                    var go = ActionExecutor.CreateGameObject(action.Name, primType, action.Position);
                    if (go != null) createdObjects[action.Name] = go;
                    return go != null;

                case "CreateEmpty":
                    var empty = ActionExecutor.CreateGameObject(action.Name, null, action.Position);
                    if (empty != null) createdObjects[action.Name] = empty;
                    return empty != null;

                case "AddComponent":
                    var target = FindObject(action.Target, createdObjects);
                    if (target != null)
                    {
                        ActionExecutor.AddComponent(target, action.Component);
                        return true;
                    }
                    return false;

                case "SetParent":
                    var child = FindObject(action.Child, createdObjects);
                    var parent = FindObject(action.Parent, createdObjects);
                    if (child != null && parent != null)
                    {
                        Undo.SetTransformParent(child.transform, parent.transform, $"Parent {action.Child} to {action.Parent}");
                        child.transform.localPosition = Vector3.zero;
                        return true;
                    }
                    return false;

                case "SetTransform":
                    var transformTarget = FindObject(action.Target, createdObjects);
                    if (transformTarget != null)
                    {
                        ActionExecutor.SetTransform(transformTarget, action.Position, action.Rotation, action.Scale);
                        return true;
                    }
                    return false;

                case "SetMaterial":
                    var matTarget = FindObject(action.Target, createdObjects);
                    if (matTarget != null)
                    {
                        Color color = GetColorFromName(action.Color);
                        var material = ActionExecutor.CreateMaterial($"{action.Color}Material", color);
                        ActionExecutor.AssignMaterial(matTarget, material);
                        return true;
                    }
                    return false;

                case "CreateScript":
                    string path = $"Assets/Scripts/{action.Name}.cs";
                    return ScriptApplicator.CreateScript(action.Code, path);

                default:
                    Debug.LogWarning($"[AIActionService] Unknown action type: {action.ActionType}");
                    return false;
            }
        }

        private static GameObject FindObject(string name, Dictionary<string, GameObject> created)
        {
            if (created.TryGetValue(name, out var obj))
                return obj;
            return GameObject.Find(name);
        }

        private static PrimitiveType? GetPrimitiveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            
            switch (typeName.ToLower())
            {
                case "cube": return PrimitiveType.Cube;
                case "sphere": return PrimitiveType.Sphere;
                case "capsule": return PrimitiveType.Capsule;
                case "cylinder": return PrimitiveType.Cylinder;
                case "plane": return PrimitiveType.Plane;
                case "quad": return PrimitiveType.Quad;
                default: return null;
            }
        }

        private static Color GetColorFromName(string colorName)
        {
            if (string.IsNullOrEmpty(colorName)) return Color.white;
            
            switch (colorName.ToLower())
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "white": return Color.white;
                case "black": return Color.black;
                case "gray": case "grey": return Color.gray;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "orange": return new Color(1f, 0.5f, 0f);
                case "purple": return new Color(0.5f, 0f, 0.5f);
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// Represents a single AI-generated action.
    /// </summary>
    public class AIAction
    {
        public string ActionType;
        
        // CreatePrimitive / CreateEmpty
        public string PrimitiveType;
        public string Name;
        public Vector3? Position;
        
        // AddComponent
        public string Target;
        public string Component;
        
        // SetParent
        public string Child;
        public string Parent;
        
        // SetTransform
        public Vector3? Rotation;
        public Vector3? Scale;
        
        // SetMaterial
        public string Color;
        
        // CreateScript
        public string Code;

        public string GetDescription()
        {
            switch (ActionType)
            {
                case "CreatePrimitive":
                    return Position.HasValue 
                        ? $"Create {PrimitiveType} \"{Name}\" at {Position}"
                        : $"Create {PrimitiveType} \"{Name}\"";
                case "CreateEmpty":
                    return $"Create empty \"{Name}\"";
                case "AddComponent":
                    return $"Add {Component} to {Target}";
                case "SetParent":
                    return $"Set {Child} as child of {Parent}";
                case "SetTransform":
                    return $"Set transform of {Target}";
                case "SetMaterial":
                    return $"Apply {Color} material to {Target}";
                case "CreateScript":
                    return $"Create script {Name}.cs";
                default:
                    return ActionType;
            }
        }
    }
}
