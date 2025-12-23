using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Executes Unity Editor actions with full Undo support.
    /// Enables AI to perform actions directly in the scene.
    /// </summary>
    public static class ActionExecutor
    {
        #region GameObject Operations

        /// <summary>
        /// Creates a new GameObject.
        /// </summary>
        public static GameObject CreateGameObject(string name, PrimitiveType? primitiveType = null, 
            Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
        {
            GameObject go;
            
            if (primitiveType.HasValue)
            {
                go = GameObject.CreatePrimitive(primitiveType.Value);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }
            
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            
            if (position.HasValue)
                go.transform.position = position.Value;
            if (rotation.HasValue)
                go.transform.eulerAngles = rotation.Value;
            if (scale.HasValue)
                go.transform.localScale = scale.Value;
            
            Selection.activeGameObject = go;
            Debug.Log($"[ActionExecutor] Created GameObject: {name}");
            
            return go;
        }

        /// <summary>
        /// Creates a GameObject as child of parent.
        /// </summary>
        public static GameObject CreateChildGameObject(string name, Transform parent, 
            PrimitiveType? primitiveType = null)
        {
            var go = CreateGameObject(name, primitiveType);
            
            Undo.SetTransformParent(go.transform, parent, $"Parent {name} to {parent.name}");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            
            return go;
        }

        /// <summary>
        /// Deletes a GameObject.
        /// </summary>
        public static void DeleteGameObject(GameObject go)
        {
            if (go == null) return;
            
            Undo.DestroyObjectImmediate(go);
            Debug.Log($"[ActionExecutor] Deleted GameObject: {go.name}");
        }

        /// <summary>
        /// Finds a GameObject by name.
        /// </summary>
        public static GameObject FindGameObject(string name)
        {
            return GameObject.Find(name);
        }

        #endregion

        #region Component Operations

        /// <summary>
        /// Adds a component to a GameObject.
        /// </summary>
        public static T AddComponent<T>(GameObject go) where T : Component
        {
            if (go == null)
            {
                Debug.LogWarning("[ActionExecutor] Cannot add component to null GameObject");
                return null;
            }
            
            var component = Undo.AddComponent<T>(go);
            Debug.Log($"[ActionExecutor] Added {typeof(T).Name} to {go.name}");
            
            return component;
        }

        /// <summary>
        /// Adds a component by type name.
        /// </summary>
        public static Component AddComponent(GameObject go, string typeName)
        {
            if (go == null) return null;
            
            Type type = GetComponentType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[ActionExecutor] Unknown component type: {typeName}");
                return null;
            }
            
            var component = Undo.AddComponent(go, type);
            Debug.Log($"[ActionExecutor] Added {typeName} to {go.name}");
            
            return component;
        }

        /// <summary>
        /// Removes a component from a GameObject.
        /// </summary>
        public static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return;
            
            var component = go.GetComponent<T>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
                Debug.Log($"[ActionExecutor] Removed {typeof(T).Name} from {go.name}");
            }
        }

        /// <summary>
        /// Removes a component by type name.
        /// </summary>
        public static void RemoveComponent(GameObject go, string typeName)
        {
            if (go == null) return;
            
            Type type = GetComponentType(typeName);
            if (type == null) return;
            
            var component = go.GetComponent(type);
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
                Debug.Log($"[ActionExecutor] Removed {typeName} from {go.name}");
            }
        }

        private static Type GetComponentType(string typeName)
        {
            // Try Unity common types
            Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;
            
            // Try full namespace
            type = Type.GetType(typeName);
            if (type != null) return type;
            
            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
                
                type = assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }
            
            return null;
        }

        #endregion

        #region Transform Operations

        /// <summary>
        /// Sets transform properties.
        /// </summary>
        public static void SetTransform(GameObject go, Vector3? position = null, 
            Vector3? rotation = null, Vector3? scale = null)
        {
            if (go == null) return;
            
            Undo.RecordObject(go.transform, $"Modify Transform {go.name}");
            
            if (position.HasValue)
                go.transform.position = position.Value;
            if (rotation.HasValue)
                go.transform.eulerAngles = rotation.Value;
            if (scale.HasValue)
                go.transform.localScale = scale.Value;
            
            Debug.Log($"[ActionExecutor] Modified transform of {go.name}");
        }

        /// <summary>
        /// Sets local transform properties.
        /// </summary>
        public static void SetLocalTransform(GameObject go, Vector3? position = null, 
            Vector3? rotation = null, Vector3? scale = null)
        {
            if (go == null) return;
            
            Undo.RecordObject(go.transform, $"Modify Transform {go.name}");
            
            if (position.HasValue)
                go.transform.localPosition = position.Value;
            if (rotation.HasValue)
                go.transform.localEulerAngles = rotation.Value;
            if (scale.HasValue)
                go.transform.localScale = scale.Value;
        }

        #endregion

        #region Material Operations

        /// <summary>
        /// Creates a new material.
        /// </summary>
        public static Material CreateMaterial(string name, Color? color = null, string shaderName = null)
        {
            Shader shader = Shader.Find(shaderName ?? "Standard");
            if (shader == null)
                shader = Shader.Find("Standard");
            
            var material = new Material(shader);
            material.name = name;
            
            if (color.HasValue)
            {
                material.color = color.Value;
            }
            
            Debug.Log($"[ActionExecutor] Created material: {name}");
            return material;
        }

        /// <summary>
        /// Creates and saves a material as asset.
        /// </summary>
        public static Material CreateMaterialAsset(string name, string folder, Color? color = null, string shaderName = null)
        {
            var material = CreateMaterial(name, color, shaderName);
            
            string path = $"{folder}/{name}.mat";
            
            // Ensure directory exists
            string fullDir = Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", path));
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);
            
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[ActionExecutor] Saved material asset: {path}");
            return material;
        }

        /// <summary>
        /// Assigns a material to a renderer.
        /// </summary>
        public static void AssignMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null) return;
            
            Undo.RecordObject(renderer, $"Assign Material to {renderer.gameObject.name}");
            renderer.sharedMaterial = material;
            
            // Force refresh
            EditorUtility.SetDirty(renderer);
            SceneView.RepaintAll();
            
            Debug.Log($"[ActionExecutor] Assigned {material.name} to {renderer.gameObject.name}");
        }

        /// <summary>
        /// Assigns a material to a GameObject's renderer.
        /// </summary>
        public static void AssignMaterial(GameObject go, Material material)
        {
            if (go == null) return;
            
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                AssignMaterial(renderer, material);
            }
        }

        /// <summary>
        /// Sets color on a material.
        /// </summary>
        public static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            
            Undo.RecordObject(material, $"Set Material Color");
            material.color = color;
        }

        #endregion

        #region Prefab Operations

        /// <summary>
        /// Creates a prefab from a GameObject.
        /// </summary>
        public static GameObject CreatePrefab(GameObject source, string path)
        {
            if (source == null) return null;
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(Path.Combine(Application.dataPath, "..", directory)))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", directory));
            }
            
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Debug.Log($"[ActionExecutor] Created prefab: {path}");
            
            return prefab;
        }

        /// <summary>
        /// Instantiates a prefab.
        /// </summary>
        public static GameObject InstantiatePrefab(string prefabPath, Vector3? position = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ActionExecutor] Prefab not found: {prefabPath}");
                return null;
            }
            
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");
            
            if (position.HasValue)
                instance.transform.position = position.Value;
            
            Selection.activeGameObject = instance;
            Debug.Log($"[ActionExecutor] Instantiated prefab: {prefab.name}");
            
            return instance;
        }

        #endregion

        #region Selection Operations

        /// <summary>
        /// Gets the currently selected GameObject.
        /// </summary>
        public static GameObject GetSelectedGameObject()
        {
            return Selection.activeGameObject;
        }

        /// <summary>
        /// Selects a GameObject.
        /// </summary>
        public static void Select(GameObject go)
        {
            Selection.activeGameObject = go;
        }

        #endregion

        #region Property Operations

        /// <summary>
        /// Sets a property on a component using reflection.
        /// </summary>
        public static bool SetProperty(Component component, string propertyName, object value)
        {
            if (component == null) return false;
            
            var type = component.GetType();
            var property = type.GetProperty(propertyName);
            
            if (property != null && property.CanWrite)
            {
                Undo.RecordObject(component, $"Set {propertyName}");
                property.SetValue(component, value);
                Debug.Log($"[ActionExecutor] Set {component.GetType().Name}.{propertyName} = {value}");
                return true;
            }
            
            var field = type.GetField(propertyName);
            if (field != null)
            {
                Undo.RecordObject(component, $"Set {propertyName}");
                field.SetValue(component, value);
                Debug.Log($"[ActionExecutor] Set {component.GetType().Name}.{propertyName} = {value}");
                return true;
            }
            
            Debug.LogWarning($"[ActionExecutor] Property not found: {propertyName}");
            return false;
        }

        #endregion
    }
}
