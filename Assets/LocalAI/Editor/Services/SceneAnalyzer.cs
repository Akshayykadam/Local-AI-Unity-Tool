using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Linq;

namespace LocalAI.Editor.Services
{
    public static class SceneAnalyzer
    {
        public struct SceneStats
        {
            public int TotalObjects;
            public int RootObjects;
            public int TotalVertices;
            public int TotalTriangles;
            public int MaterialCount;
            public int TextureCount;
            public int RealtimeLights;
            public int BakedLights;
            public int ColliderCount;
            public int Rigidbodies;
        }

        public struct SceneIssue
        {
            public string ObjectName;
            public string Path;
            public string IssueType;
            public string Description;
            public string Severity; // Low, Medium, High
        }

        public static string AnalyzeCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return "No valid active scene.";

            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
            
            SceneStats stats = CollectStats(allObjects);
            List<SceneIssue> issues = FindIssues(allObjects);

            return FormatReport(scene.name, stats, issues);
        }

        private static SceneStats CollectStats(GameObject[] allObjects)
        {
            SceneStats stats = new SceneStats();
            stats.TotalObjects = allObjects.Length;
            stats.RootObjects = SceneManager.GetActiveScene().rootCount;
            
            HashSet<Material> uniqueMaterials = new HashSet<Material>();
            HashSet<Texture> uniqueTextures = new HashSet<Texture>();

            foreach (var go in allObjects)
            {
                // Mesh Stats
                if (go.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    stats.TotalVertices += mf.sharedMesh.vertexCount;
                    stats.TotalTriangles += mf.sharedMesh.triangles.Length / 3;
                }
                
                // Renderer Stats (Materials/Textures)
                if (go.TryGetComponent<Renderer>(out var rend))
                {
                    foreach (var mat in rend.sharedMaterials)
                    {
                        if (mat != null)
                        {
                            uniqueMaterials.Add(mat);
                            if (mat.mainTexture != null) uniqueTextures.Add(mat.mainTexture);
                        }
                    }
                }

                // Lights
                if (go.TryGetComponent<Light>(out var l))
                {
                    if (l.lightmapBakeType == LightmapBakeType.Realtime) stats.RealtimeLights++;
                    else stats.BakedLights++; // Mixed counts as baked for simplicity here or could be separate
                }
                
                // Physics
                if (go.GetComponent<Collider>()) stats.ColliderCount++;
                if (go.GetComponent<Rigidbody>()) stats.Rigidbodies++;
            }

            stats.MaterialCount = uniqueMaterials.Count;
            stats.TextureCount = uniqueTextures.Count;

            return stats;
        }

        private static List<SceneIssue> FindIssues(GameObject[] allObjects)
        {
            List<SceneIssue> issues = new List<SceneIssue>();

            foreach (var go in allObjects)
            {
                string path = GetPath(go.transform);

                // 1. Missing Scripts
                Component[] components = go.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null)
                    {
                        issues.Add(new SceneIssue 
                        {
                            ObjectName = go.name,
                            Path = path,
                            IssueType = "Missing Script",
                            Description = "GameObject has a missing script reference.",
                            Severity = "High"
                        });
                    }
                }

                // 2. Empty GameObject with no purpose (No components except Transform, no children)
                if (components.Length == 1 && go.transform.childCount == 0 && go.name != "Separators" && !go.name.StartsWith("---"))
                {
                    // Filter out likely organizational objects
                    issues.Add(new SceneIssue
                    {
                        ObjectName = go.name,
                        Path = path,
                        IssueType = "Empty GameObject",
                        Description = "Object has no components and no children. Potential leftover.",
                        Severity = "Low"
                    });
                }

                // 3. Collider without Rigidbody (Static Trigger risk if moving)
                // This is a simple heuristic; Unity handles static colliders well, but moving them is bad.
                // We'll just flag if it has a collider, no RB, but is NOT marked Static.
                if (go.GetComponent<Collider>() != null && go.GetComponent<Rigidbody>() == null && !go.isStatic)
                {
                     // Only warn if it might move. If it's effectively static but not marked static, it's a minor perf issue.
                     issues.Add(new SceneIssue
                     {
                         ObjectName = go.name,
                         Path = path,
                         IssueType = "Non-Static Collider check",
                         Description = "Collider on non-static object without Rigidbody. If this object moves, add a RB (Kinematic) for performance.",
                         Severity = "Low"
                     });
                }
                
                // 4. Mismatched Components: MeshRenderer without MeshFilter
                if (go.GetComponent<MeshRenderer>() != null && go.GetComponent<MeshFilter>() == null)
                {
                    issues.Add(new SceneIssue{
                        ObjectName = go.name,
                        Path = path,
                        IssueType = "Broken Renderer",
                        Description = "MeshRenderer exists without a MeshFilter.",
                        Severity = "Medium"
                    });
                }
            }

            // Limit issues list to prevent token overflow, can categorize later
            if (issues.Count > 50) 
            {
                issues = issues.Take(50).ToList();
                issues.Add(new SceneIssue { ObjectName = "SYSTEM", Path = "N/A", IssueType = "Limit Reached", Description = "Too many issues found. Showing first 50.", Severity = "Info" });
            }

            return issues;
        }

        private static string FormatReport(string sceneName, SceneStats stats, List<SceneIssue> issues)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# SCENE ANALYSIS REPORT: {sceneName}");
            sb.AppendLine("## STATISTICS");
            sb.AppendLine($"- Total Objects: {stats.TotalObjects}");
            sb.AppendLine($"- Vertices: {stats.TotalVertices:N0}");
            sb.AppendLine($"- Triangles: {stats.TotalTriangles:N0}");
            sb.AppendLine($"- Materials: {stats.MaterialCount}");
            sb.AppendLine($"- Textures: {stats.TextureCount}");
            sb.AppendLine($"- Realtime Lights: {stats.RealtimeLights}");
            sb.AppendLine($"- Baked Lights: {stats.BakedLights}");
            sb.AppendLine($"- Colliders: {stats.ColliderCount}");
            sb.AppendLine($"- Rigidbodies: {stats.Rigidbodies}");
            sb.AppendLine();
            
            sb.AppendLine("## DETECTED ISSUES");
            if (issues.Count == 0)
            {
                sb.AppendLine("No critical scene issues detected by heuristic scan.");
            }
            else
            {
                foreach (var issue in issues)
                {
                    sb.AppendLine($"- [{issue.Severity.ToUpper()}] **{issue.IssueType}** on `{issue.ObjectName}`");
                    sb.AppendLine($"  - Path: {issue.Path}");
                    sb.AppendLine($"  - Info: {issue.Description}");
                }
            }
            
            return sb.ToString();
        }

        private static string GetPath(Transform t)
        {
            if (t.parent == null) return "/" + t.name;
            return GetPath(t.parent) + "/" + t.name;
        }
    }
}
