using System;
using System.Collections.Generic;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Pre-defined command templates for common Unity setups.
    /// </summary>
    public static class CommandTemplates
    {
        public static readonly List<CommandTemplate> Templates = new List<CommandTemplate>
        {
            // Player Templates
            new CommandTemplate
            {
                Name = "FPS Player",
                Description = "First-person player with camera and controller",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Capsule, "Player", new Vector3(0, 1, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "CharacterController", "Player"),
                }
            },
            new CommandTemplate
            {
                Name = "Third Person Player",
                Description = "Third-person character setup",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Capsule, "Player", new Vector3(0, 1, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "Rigidbody", "Player"),
                    new TemplateAction(TemplateActionType.AddComponent, "CapsuleCollider", "Player"),
                }
            },
            
            // Environment Templates
            new CommandTemplate
            {
                Name = "Ground Plane",
                Description = "Basic ground with collider",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Plane, "Ground", Vector3.zero),
                }
            },
            new CommandTemplate
            {
                Name = "Physics Cube",
                Description = "Cube with physics enabled",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Cube, "PhysicsCube", new Vector3(0, 5, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "Rigidbody", "PhysicsCube"),
                }
            },
            new CommandTemplate
            {
                Name = "Bouncy Ball",
                Description = "Sphere with bouncy physics",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Sphere, "Ball", new Vector3(0, 5, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "Rigidbody", "Ball"),
                }
            },
            
            // Lighting Templates
            new CommandTemplate
            {
                Name = "Point Light",
                Description = "Point light for local illumination",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreateEmpty, null, "PointLight", new Vector3(0, 3, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "Light", "PointLight"),
                }
            },
            
            // Enemy Templates
            new CommandTemplate
            {
                Name = "Basic Enemy",
                Description = "Enemy with physics",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Capsule, "Enemy", new Vector3(5, 1, 5)),
                    new TemplateAction(TemplateActionType.AddComponent, "Rigidbody", "Enemy"),
                    new TemplateAction(TemplateActionType.AddComponent, "CapsuleCollider", "Enemy"),
                }
            },
            
            // Trigger Templates
            new CommandTemplate
            {
                Name = "Trigger Zone",
                Description = "Invisible trigger area",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreatePrimitive, PrimitiveType.Cube, "TriggerZone", new Vector3(0, 1, 0)),
                    new TemplateAction(TemplateActionType.AddComponent, "BoxCollider", "TriggerZone"),
                }
            },
            
            // Audio Templates
            new CommandTemplate
            {
                Name = "Audio Source",
                Description = "3D audio source",
                Actions = new TemplateAction[]
                {
                    new TemplateAction(TemplateActionType.CreateEmpty, null, "AudioPlayer", Vector3.zero),
                    new TemplateAction(TemplateActionType.AddComponent, "AudioSource", "AudioPlayer"),
                }
            },
        };

        /// <summary>
        /// Gets template by name.
        /// </summary>
        public static CommandTemplate GetTemplate(string name)
        {
            return Templates.Find(t => t.Name == name);
        }

        /// <summary>
        /// Executes all actions in a template.
        /// </summary>
        public static int ExecuteTemplate(CommandTemplate template)
        {
            int executed = 0;
            Dictionary<string, GameObject> createdObjects = new Dictionary<string, GameObject>();
            
            foreach (var action in template.Actions)
            {
                try
                {
                    switch (action.Type)
                    {
                        case TemplateActionType.CreatePrimitive:
                            var go = ActionExecutor.CreateGameObject(
                                action.TargetName, 
                                action.PrimitiveType, 
                                action.Position
                            );
                            if (go != null)
                            {
                                createdObjects[action.TargetName] = go;
                                executed++;
                            }
                            break;
                            
                        case TemplateActionType.CreateEmpty:
                            var empty = ActionExecutor.CreateGameObject(
                                action.TargetName, 
                                null, 
                                action.Position
                            );
                            if (empty != null)
                            {
                                createdObjects[action.TargetName] = empty;
                                executed++;
                            }
                            break;
                            
                        case TemplateActionType.AddComponent:
                            GameObject target = null;
                            if (createdObjects.TryGetValue(action.TargetName, out var cached))
                            {
                                target = cached;
                            }
                            else
                            {
                                target = GameObject.Find(action.TargetName);
                            }
                            
                            if (target != null)
                            {
                                ActionExecutor.AddComponent(target, action.ComponentName);
                                executed++;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CommandTemplates] Action failed: {ex.Message}");
                }
            }
            
            return executed;
        }
    }

    public class CommandTemplate
    {
        public string Name;
        public string Description;
        public TemplateAction[] Actions;
        
        // Legacy property for backwards compatibility
        public string[] Commands => null;
    }

    public class TemplateAction
    {
        public TemplateActionType Type;
        public PrimitiveType? PrimitiveType;
        public string ComponentName;
        public string TargetName;
        public Vector3 Position;

        public TemplateAction(TemplateActionType type, PrimitiveType? primitiveType, string targetName, Vector3 position)
        {
            Type = type;
            PrimitiveType = primitiveType;
            TargetName = targetName;
            Position = position;
        }

        public TemplateAction(TemplateActionType type, string componentName, string targetName)
        {
            Type = type;
            ComponentName = componentName;
            TargetName = targetName;
        }
    }

    public enum TemplateActionType
    {
        CreatePrimitive,
        CreateEmpty,
        AddComponent,
        SetProperty
    }


    /// <summary>
    /// Provides smart action suggestions based on current selection.
    /// </summary>
    public static class SmartSuggestions
    {
        /// <summary>
        /// Gets suggested actions based on selected GameObject.
        /// </summary>
        public static List<ActionSuggestion> GetSuggestions(GameObject selected)
        {
            var suggestions = new List<ActionSuggestion>();
            
            if (selected == null)
            {
                // No selection - suggest creation
                suggestions.Add(new ActionSuggestion("Create Cube", "Create Cube at (0, 1, 0)", SuggestionCategory.Create));
                suggestions.Add(new ActionSuggestion("Create Sphere", "Create Sphere at (0, 1, 0)", SuggestionCategory.Create));
                suggestions.Add(new ActionSuggestion("Create Empty", "Create empty named NewObject", SuggestionCategory.Create));
                return suggestions;
            }

            // Check what components are missing
            bool hasRigidbody = selected.GetComponent<Rigidbody>() != null;
            bool hasCollider = selected.GetComponent<Collider>() != null;
            bool hasRenderer = selected.GetComponent<Renderer>() != null;
            bool hasLight = selected.GetComponent<Light>() != null;
            bool hasAudioSource = selected.GetComponent<AudioSource>() != null;

            // Physics suggestions
            if (!hasRigidbody && hasRenderer)
            {
                suggestions.Add(new ActionSuggestion("Add Rigidbody", $"Add Rigidbody to {selected.name}", SuggestionCategory.Physics));
            }
            if (!hasCollider && hasRenderer)
            {
                suggestions.Add(new ActionSuggestion("Add Collider", $"Add BoxCollider to {selected.name}", SuggestionCategory.Physics));
            }
            if (hasRigidbody)
            {
                suggestions.Add(new ActionSuggestion("Make Kinematic", $"Set isKinematic to true on {selected.name}", SuggestionCategory.Physics));
            }

            // Renderer suggestions
            if (hasRenderer)
            {
                suggestions.Add(new ActionSuggestion("Red Material", $"Create red material for {selected.name}", SuggestionCategory.Material));
                suggestions.Add(new ActionSuggestion("Blue Material", $"Create blue material for {selected.name}", SuggestionCategory.Material));
            }

            // Light suggestions
            if (hasLight)
            {
                suggestions.Add(new ActionSuggestion("Increase Intensity", "Set intensity to 2", SuggestionCategory.Light));
                suggestions.Add(new ActionSuggestion("Change to Point", "Set type to Point", SuggestionCategory.Light));
            }
            else if (!hasRenderer)
            {
                suggestions.Add(new ActionSuggestion("Add Light", $"Add Light to {selected.name}", SuggestionCategory.Light));
            }

            // Audio suggestions
            if (!hasAudioSource)
            {
                suggestions.Add(new ActionSuggestion("Add AudioSource", $"Add AudioSource to {selected.name}", SuggestionCategory.Audio));
            }

            // Transform suggestions
            suggestions.Add(new ActionSuggestion("Reset Position", $"Move {selected.name} to (0, 0, 0)", SuggestionCategory.Transform));
            suggestions.Add(new ActionSuggestion("Move Up", $"Move {selected.name} to (0, 5, 0)", SuggestionCategory.Transform));

            // Prefab suggestion
            suggestions.Add(new ActionSuggestion("Make Prefab", $"Create prefab from {selected.name}", SuggestionCategory.Prefab));

            // Duplicate
            suggestions.Add(new ActionSuggestion("Duplicate", $"Create {selected.name} at current position", SuggestionCategory.Create));

            return suggestions;
        }
    }

    public class ActionSuggestion
    {
        public string Label;
        public string Command;
        public SuggestionCategory Category;

        public ActionSuggestion(string label, string command, SuggestionCategory category)
        {
            Label = label;
            Command = command;
            Category = category;
        }
    }

    public enum SuggestionCategory
    {
        Create,
        Physics,
        Material,
        Light,
        Audio,
        Transform,
        Prefab
    }
}
