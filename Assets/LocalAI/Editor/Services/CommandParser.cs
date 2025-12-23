using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Parses natural language commands from AI responses and converts them to executable actions.
    /// </summary>
    public static class CommandParser
    {
        // Command patterns
        private static readonly CommandPattern[] Patterns = new[]
        {
            // Create GameObject patterns
            new CommandPattern(
                ActionType.CreateGameObject,
                @"[Cc]reate\s+(?:a\s+)?(?:new\s+)?(\w+)(?:\s+(?:at|at position|position)\s*\(?\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)?)?",
                new[] { "primitive", "x", "y", "z" }
            ),
            new CommandPattern(
                ActionType.CreateGameObject,
                @"[Aa]dd\s+(?:a\s+)?(?:new\s+)?(\w+)\s+(?:to\s+)?(?:the\s+)?scene",
                new[] { "primitive" }
            ),
            
            // Add component patterns
            new CommandPattern(
                ActionType.AddComponent,
                @"[Aa]dd\s+(?:a\s+)?(\w+)\s+(?:component\s+)?(?:to\s+)?(?:the\s+)?(?:selected|(\w+))?",
                new[] { "component", "target" }
            ),
            new CommandPattern(
                ActionType.AddComponent,
                @"[Aa]ttach\s+(?:a\s+)?(\w+)\s+(?:to\s+)?(\w+)?",
                new[] { "component", "target" }
            ),
            
            // Remove component patterns
            new CommandPattern(
                ActionType.RemoveComponent,
                @"[Rr]emove\s+(?:the\s+)?(\w+)\s+(?:component\s+)?(?:from\s+)?(?:the\s+)?(\w+)?",
                new[] { "component", "target" }
            ),
            
            // Delete GameObject patterns
            new CommandPattern(
                ActionType.DeleteGameObject,
                @"[Dd]elete\s+(?:the\s+)?(\w+)(?:\s+object)?",
                new[] { "target" }
            ),
            new CommandPattern(
                ActionType.DeleteGameObject,
                @"[Rr]emove\s+(?:the\s+)?(\w+)\s+(?:from\s+)?(?:the\s+)?scene",
                new[] { "target" }
            ),
            
            // Set property patterns
            new CommandPattern(
                ActionType.SetProperty,
                @"[Ss]et\s+(?:the\s+)?(\w+)\s+(?:to|=)\s+(-?[\d.]+)",
                new[] { "property", "value" }
            ),
            new CommandPattern(
                ActionType.SetProperty,
                @"[Cc]hange\s+(?:the\s+)?(\w+)\s+(?:to|=)\s+(-?[\d.]+)",
                new[] { "property", "value" }
            ),
            
            // Material patterns
            new CommandPattern(
                ActionType.CreateMaterial,
                @"[Cc]reate\s+(?:a\s+)?(?:new\s+)?(\w+)\s+material",
                new[] { "color" }
            ),
            new CommandPattern(
                ActionType.SetMaterialColor,
                @"[Ss]et\s+(?:the\s+)?(?:material\s+)?color\s+(?:to\s+)?(\w+)",
                new[] { "color" }
            ),
            new CommandPattern(
                ActionType.SetMaterialColor,
                @"[Mm]ake\s+(?:it|the\s+\w+)\s+(\w+)",
                new[] { "color" }
            ),
            
            // Transform patterns
            new CommandPattern(
                ActionType.SetPosition,
                @"[Mm]ove\s+(?:the\s+)?(?:(\w+)\s+)?(?:to\s+)?\(?\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)?",
                new[] { "target", "x", "y", "z" }
            ),
            new CommandPattern(
                ActionType.SetScale,
                @"[Ss]cale\s+(?:the\s+)?(?:(\w+)\s+)?(?:to\s+)?\(?\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)?",
                new[] { "target", "x", "y", "z" }
            ),
            new CommandPattern(
                ActionType.SetRotation,
                @"[Rr]otate\s+(?:the\s+)?(?:(\w+)\s+)?(?:to\s+)?\(?\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)?",
                new[] { "target", "x", "y", "z" }
            ),
        };

        // Color name mapping
        private static readonly Dictionary<string, Color> ColorNames = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", Color.red },
            { "green", Color.green },
            { "blue", Color.blue },
            { "yellow", Color.yellow },
            { "white", Color.white },
            { "black", Color.black },
            { "gray", Color.gray },
            { "grey", Color.gray },
            { "cyan", Color.cyan },
            { "magenta", Color.magenta },
            { "orange", new Color(1f, 0.5f, 0f) },
            { "purple", new Color(0.5f, 0f, 0.5f) },
            { "pink", new Color(1f, 0.75f, 0.8f) },
            { "brown", new Color(0.6f, 0.3f, 0f) },
        };

        // Primitive type mapping
        private static readonly Dictionary<string, PrimitiveType> PrimitiveNames = new Dictionary<string, PrimitiveType>(StringComparer.OrdinalIgnoreCase)
        {
            { "cube", PrimitiveType.Cube },
            { "sphere", PrimitiveType.Sphere },
            { "capsule", PrimitiveType.Capsule },
            { "cylinder", PrimitiveType.Cylinder },
            { "plane", PrimitiveType.Plane },
            { "quad", PrimitiveType.Quad },
        };

        /// <summary>
        /// Parses text and extracts actionable commands.
        /// </summary>
        public static List<ActionCommand> ParseCommands(string text)
        {
            var commands = new List<ActionCommand>();
            
            // Split into sentences
            var sentences = Regex.Split(text, @"[.!?\n]");
            
            foreach (var sentence in sentences)
            {
                if (string.IsNullOrWhiteSpace(sentence)) continue;
                
                foreach (var pattern in Patterns)
                {
                    var match = pattern.Regex.Match(sentence);
                    if (match.Success)
                    {
                        var command = CreateCommand(pattern, match);
                        if (command != null)
                        {
                            commands.Add(command);
                            break; // One command per sentence
                        }
                    }
                }
            }
            
            return commands;
        }

        /// <summary>
        /// Checks if text contains any actionable commands.
        /// </summary>
        public static bool HasCommands(string text)
        {
            return ParseCommands(text).Count > 0;
        }

        private static ActionCommand CreateCommand(CommandPattern pattern, Match match)
        {
            var command = new ActionCommand
            {
                Type = pattern.Type,
                Parameters = new Dictionary<string, object>(),
                RawText = match.Value
            };

            // Extract parameters
            for (int i = 0; i < pattern.ParameterNames.Length && i + 1 < match.Groups.Count; i++)
            {
                string value = match.Groups[i + 1].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    command.Parameters[pattern.ParameterNames[i]] = value.Trim();
                }
            }

            // Post-process based on type
            ProcessCommand(command);

            return command;
        }

        private static void ProcessCommand(ActionCommand command)
        {
            switch (command.Type)
            {
                case ActionType.CreateGameObject:
                    if (command.Parameters.TryGetValue("primitive", out var primitive))
                    {
                        string primName = primitive.ToString();
                        if (PrimitiveNames.TryGetValue(primName, out var primType))
                        {
                            command.Parameters["primitiveType"] = primType;
                        }
                        command.Parameters["name"] = primName;
                    }
                    
                    // Parse position if present
                    if (command.Parameters.TryGetValue("x", out var x) &&
                        command.Parameters.TryGetValue("y", out var y) &&
                        command.Parameters.TryGetValue("z", out var z))
                    {
                        command.Parameters["position"] = new Vector3(
                            float.Parse(x.ToString()),
                            float.Parse(y.ToString()),
                            float.Parse(z.ToString())
                        );
                    }
                    break;
                    
                case ActionType.CreateMaterial:
                case ActionType.SetMaterialColor:
                    if (command.Parameters.TryGetValue("color", out var colorName))
                    {
                        if (ColorNames.TryGetValue(colorName.ToString(), out var color))
                        {
                            command.Parameters["colorValue"] = color;
                        }
                    }
                    break;
                    
                case ActionType.SetPosition:
                case ActionType.SetScale:
                case ActionType.SetRotation:
                    if (command.Parameters.TryGetValue("x", out var px) &&
                        command.Parameters.TryGetValue("y", out var py) &&
                        command.Parameters.TryGetValue("z", out var pz))
                    {
                        command.Parameters["vector"] = new Vector3(
                            float.Parse(px.ToString()),
                            float.Parse(py.ToString()),
                            float.Parse(pz.ToString())
                        );
                    }
                    break;
            }
        }

        /// <summary>
        /// Executes a parsed command.
        /// </summary>
        public static bool ExecuteCommand(ActionCommand command)
        {
            try
            {
                switch (command.Type)
                {
                    case ActionType.CreateGameObject:
                        return ExecuteCreateGameObject(command);
                    case ActionType.AddComponent:
                        return ExecuteAddComponent(command);
                    case ActionType.RemoveComponent:
                        return ExecuteRemoveComponent(command);
                    case ActionType.DeleteGameObject:
                        return ExecuteDeleteGameObject(command);
                    case ActionType.CreateMaterial:
                        return ExecuteCreateMaterial(command);
                    case ActionType.SetMaterialColor:
                        return ExecuteSetMaterialColor(command);
                    case ActionType.SetPosition:
                        return ExecuteSetTransform(command, TransformType.Position);
                    case ActionType.SetRotation:
                        return ExecuteSetTransform(command, TransformType.Rotation);
                    case ActionType.SetScale:
                        return ExecuteSetTransform(command, TransformType.Scale);
                    default:
                        Debug.LogWarning($"[CommandParser] Unhandled command type: {command.Type}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandParser] Failed to execute command: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes all commands in a list.
        /// </summary>
        public static int ExecuteCommands(List<ActionCommand> commands)
        {
            int executed = 0;
            foreach (var command in commands)
            {
                if (ExecuteCommand(command))
                    executed++;
            }
            return executed;
        }

        #region Command Executors

        private static bool ExecuteCreateGameObject(ActionCommand cmd)
        {
            string name = cmd.Parameters.GetValueOrDefault("name")?.ToString() ?? "NewObject";
            PrimitiveType? primType = cmd.Parameters.TryGetValue("primitiveType", out var pt) 
                ? (PrimitiveType)pt : null;
            Vector3? position = cmd.Parameters.TryGetValue("position", out var pos) 
                ? (Vector3)pos : null;
            
            ActionExecutor.CreateGameObject(name, primType, position);
            return true;
        }

        private static bool ExecuteAddComponent(ActionCommand cmd)
        {
            string componentName = cmd.Parameters.GetValueOrDefault("component")?.ToString();
            string targetName = cmd.Parameters.GetValueOrDefault("target")?.ToString();
            
            if (string.IsNullOrEmpty(componentName)) return false;
            
            GameObject target = string.IsNullOrEmpty(targetName) || targetName.ToLower() == "selected"
                ? ActionExecutor.GetSelectedGameObject()
                : ActionExecutor.FindGameObject(targetName);
            
            if (target == null)
            {
                Debug.LogWarning($"[CommandParser] Target not found: {targetName}");
                return false;
            }
            
            ActionExecutor.AddComponent(target, componentName);
            return true;
        }

        private static bool ExecuteRemoveComponent(ActionCommand cmd)
        {
            string componentName = cmd.Parameters.GetValueOrDefault("component")?.ToString();
            string targetName = cmd.Parameters.GetValueOrDefault("target")?.ToString();
            
            if (string.IsNullOrEmpty(componentName)) return false;
            
            GameObject target = string.IsNullOrEmpty(targetName)
                ? ActionExecutor.GetSelectedGameObject()
                : ActionExecutor.FindGameObject(targetName);
            
            if (target == null) return false;
            
            ActionExecutor.RemoveComponent(target, componentName);
            return true;
        }

        private static bool ExecuteDeleteGameObject(ActionCommand cmd)
        {
            string targetName = cmd.Parameters.GetValueOrDefault("target")?.ToString();
            if (string.IsNullOrEmpty(targetName)) return false;
            
            GameObject target = ActionExecutor.FindGameObject(targetName);
            if (target == null) return false;
            
            ActionExecutor.DeleteGameObject(target);
            return true;
        }

        private static bool ExecuteCreateMaterial(ActionCommand cmd)
        {
            string colorName = cmd.Parameters.GetValueOrDefault("color")?.ToString() ?? "white";
            Color color = cmd.Parameters.TryGetValue("colorValue", out var cv) 
                ? (Color)cv : Color.white;
            
            var material = ActionExecutor.CreateMaterial($"{colorName}Material", color);
            
            // Apply to selected if has renderer
            var selected = ActionExecutor.GetSelectedGameObject();
            if (selected != null)
            {
                ActionExecutor.AssignMaterial(selected, material);
            }
            
            return true;
        }

        private static bool ExecuteSetMaterialColor(ActionCommand cmd)
        {
            if (!cmd.Parameters.TryGetValue("colorValue", out var colorObj)) return false;
            Color color = (Color)colorObj;
            
            var selected = ActionExecutor.GetSelectedGameObject();
            if (selected == null) return false;
            
            var renderer = selected.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return false;
            
            ActionExecutor.SetMaterialColor(renderer.sharedMaterial, color);
            return true;
        }

        private enum TransformType { Position, Rotation, Scale }

        private static bool ExecuteSetTransform(ActionCommand cmd, TransformType type)
        {
            if (!cmd.Parameters.TryGetValue("vector", out var vecObj)) return false;
            Vector3 vector = (Vector3)vecObj;
            
            string targetName = cmd.Parameters.GetValueOrDefault("target")?.ToString();
            GameObject target = string.IsNullOrEmpty(targetName)
                ? ActionExecutor.GetSelectedGameObject()
                : ActionExecutor.FindGameObject(targetName);
            
            if (target == null) return false;
            
            switch (type)
            {
                case TransformType.Position:
                    ActionExecutor.SetTransform(target, position: vector);
                    break;
                case TransformType.Rotation:
                    ActionExecutor.SetTransform(target, rotation: vector);
                    break;
                case TransformType.Scale:
                    ActionExecutor.SetTransform(target, scale: vector);
                    break;
            }
            
            return true;
        }

        #endregion
    }

    #region Types

    public enum ActionType
    {
        CreateGameObject,
        DeleteGameObject,
        AddComponent,
        RemoveComponent,
        SetProperty,
        CreateMaterial,
        SetMaterialColor,
        SetPosition,
        SetRotation,
        SetScale,
    }

    public class ActionCommand
    {
        public ActionType Type;
        public Dictionary<string, object> Parameters;
        public string RawText;

        public override string ToString()
        {
            return $"{Type}: {RawText}";
        }

        public string GetDescription()
        {
            switch (Type)
            {
                case ActionType.CreateGameObject:
                    string name = Parameters.GetValueOrDefault("name")?.ToString() ?? "Object";
                    return Parameters.TryGetValue("position", out var pos)
                        ? $"Create {name} at {pos}"
                        : $"Create {name}";
                case ActionType.AddComponent:
                    return $"Add {Parameters.GetValueOrDefault("component")} to {Parameters.GetValueOrDefault("target") ?? "selected"}";
                case ActionType.DeleteGameObject:
                    return $"Delete {Parameters.GetValueOrDefault("target")}";
                case ActionType.CreateMaterial:
                    return $"Create {Parameters.GetValueOrDefault("color")} material";
                case ActionType.SetMaterialColor:
                    return $"Set color to {Parameters.GetValueOrDefault("color")}";
                default:
                    return RawText;
            }
        }
    }

    internal class CommandPattern
    {
        public ActionType Type;
        public Regex Regex;
        public string[] ParameterNames;

        public CommandPattern(ActionType type, string pattern, string[] paramNames)
        {
            Type = type;
            Regex = new Regex(pattern, RegexOptions.Compiled);
            ParameterNames = paramNames;
        }
    }

    #endregion

    #region Extensions

    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            return dict.TryGetValue(key, out var value) ? value : default;
        }
    }

    #endregion
}
