using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services.SemanticSearch
{
    /// <summary>
    /// Scans Unity project folders for C# files and detects changes.
    /// </summary>
    public class ProjectScanner
    {
        /// <summary>
        /// Info about a scanned file.
        /// </summary>
        public struct FileInfo
        {
            public string Path;
            public string Hash;
            public DateTime LastModified;
        }
        
        private readonly List<string> _excludePatterns;
        
        public ProjectScanner()
        {
            _excludePatterns = new List<string>
            {
                "/Editor/",
                "/Plugins/",
                "/ThirdParty/",
                "/TextMesh Pro/",
                "/.git/",
                "/Library/",
                "/Temp/",
                "/obj/",
                "/bin/"
            };
        }
        
        /// <summary>
        /// Scans specified folders for C# files.
        /// </summary>
        /// <param name="folders">List of folder paths to scan</param>
        /// <param name="maxFiles">Maximum number of files to return</param>
        /// <param name="progress">Progress callback (0-1)</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>List of file info with hashes</returns>
        public async Task<List<FileInfo>> ScanFoldersAsync(
            List<string> folders,
            int maxFiles = 5000,
            IProgress<float> progress = null,
            CancellationToken token = default)
        {
            var files = new List<FileInfo>();
            var allPaths = new List<string>();
            
            // Collect all .cs files
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                
                try
                {
                    var csFiles = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
                    allPaths.AddRange(csFiles);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SemanticSearch] Error scanning {folder}: {ex.Message}");
                }
            }
            
            // Filter excluded paths
            allPaths = allPaths
                .Where(p => !IsExcluded(p))
                .Take(maxFiles)
                .ToList();
            
            // Process files
            int total = allPaths.Count;
            for (int i = 0; i < total; i++)
            {
                if (token.IsCancellationRequested) break;
                
                string path = allPaths[i];
                
                try
                {
                    var info = new FileInfo
                    {
                        Path = path,
                        Hash = await ComputeFileHashAsync(path),
                        LastModified = File.GetLastWriteTimeUtc(path)
                    };
                    files.Add(info);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SemanticSearch] Error processing {path}: {ex.Message}");
                }
                
                progress?.Report((float)(i + 1) / total);
                
                // Yield to prevent blocking
                if (i % 50 == 0)
                {
                    await Task.Yield();
                }
            }
            
            return files;
        }
        
        /// <summary>
        /// Scans folders synchronously (for smaller operations).
        /// </summary>
        public List<FileInfo> ScanFolders(List<string> folders, int maxFiles = 5000)
        {
            return ScanFoldersAsync(folders, maxFiles).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Checks if a file path should be excluded from scanning.
        /// </summary>
        public bool IsExcluded(string path)
        {
            string normalized = path.Replace("\\", "/");
            return _excludePatterns.Any(pattern => normalized.Contains(pattern));
        }
        
        /// <summary>
        /// Adds a custom exclude pattern.
        /// </summary>
        public void AddExcludePattern(string pattern)
        {
            if (!_excludePatterns.Contains(pattern))
            {
                _excludePatterns.Add(pattern);
            }
        }
        
        /// <summary>
        /// Computes MD5 hash of a file's content.
        /// </summary>
        public async Task<string> ComputeFileHashAsync(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                }
                md5.TransformFinalBlock(buffer, 0, 0);
                
                return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        /// <summary>
        /// Computes MD5 hash synchronously.
        /// </summary>
        public string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        /// <summary>
        /// Gets the default folders to scan in a Unity project.
        /// </summary>
        public static List<string> GetDefaultFolders()
        {
            string assetsPath = Application.dataPath;
            return new List<string> { assetsPath };
        }
    }
}
