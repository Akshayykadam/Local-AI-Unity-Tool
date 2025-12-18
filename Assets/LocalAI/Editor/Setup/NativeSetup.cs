using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace LocalAI.Editor.Setup
{
    public class NativeSetup
    {
        // Using release b7423 (Dec 2025) - Updated to ggml-org to allow direct download
        private const string BASE_URL = "https://github.com/ggml-org/llama.cpp/releases/download/b7423/";
        
        // MacOS ARM64 (Apple Silicon)
        private const string MAC_ARM64_FILE = "llama-b7423-bin-macos-arm64.zip";
        // Windows x64 (AVX2)
        private const string WIN_X64_FILE = "llama-b7423-bin-win-avx2-x64.zip";

        [MenuItem("Tools/Local AI/Install Native Libraries", false, 1)]
        public static async void InstallLibraries()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Cannot install libraries while playing.", "OK");
                return;
            }

            string platform = "unknown";
            string fileName = "";
            string libName = "";
            string targetFolder = Path.Combine(Application.dataPath, "LocalAI/Plugins");

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                platform = "macOS-arm64"; // Assuming M1/M2/M3 for modern setup, fallback to x64 if needed manually
                fileName = MAC_ARM64_FILE;
                libName = "libllama.dylib";
                targetFolder = Path.Combine(targetFolder, "macOS");
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                platform = "Windows-x64";
                fileName = WIN_X64_FILE;
                libName = "llama.dll";
                targetFolder = Path.Combine(targetFolder, "Windows");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Unsupported Platform for auto-install.", "OK");
                return;
            }

            string downloadUrl = BASE_URL + fileName;
            string tempZipPath = Path.Combine(Application.temporaryCachePath, fileName);

            try
            {
                EditorUtility.DisplayProgressBar("Downloading Native Library", $"Fetching {fileName}...", 0f);

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Failed to download: {response.StatusCode} - {response.ReasonPhrase}");
                    }

                    using (var s = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(tempZipPath, FileMode.Create))
                    {
                        await s.CopyToAsync(fs);
                    }
                }
                
                // VALIDATION: Check if it's a valid zip
                if (!IsValidZip(tempZipPath))
                {
                    throw new Exception("Downloaded file is not a valid ZIP archive. It might be a redirect page or corrupted.");
                }

                EditorUtility.DisplayProgressBar("Installing", "Extracting library...", 0.5f);

                if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
                Directory.CreateDirectory(targetFolder);

                // Extract all relevant libraries (llama, ggml, etc)
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    bool foundMainLib = false;
                    foreach (var entry in archive.Entries)
                    {
                        // Skip directories and extremely small files (empty/symlinks < 1KB)
                        if (entry.Length < 10 * 1024) continue; // Lowered to 10KB to capture small shims if any
                        
                        string ext = Path.GetExtension(entry.Name).ToLower();
                        if ((Application.platform == RuntimePlatform.OSXEditor && ext == ".dylib") ||
                            (Application.platform == RuntimePlatform.WindowsEditor && ext == ".dll"))
                        {
                            // It's a library!
                            
                            // Determine target name
                            string targetName = entry.Name;
                            
                            // If it's the main llama lib, force the canonical name for P/Invoke
                            if (entry.Name == libName || entry.Name.StartsWith("libllama") || entry.Name.StartsWith("llama"))
                            {
                                // Check if it's the main lib (contains 'llama' but not 'adapter' or other plugins if any)
                                // Standard release: libllama.dylib or llama.dll
                                // We'll save a copy as the canonical name
                                string mainPath = Path.Combine(targetFolder, libName);
                                entry.ExtractToFile(mainPath, true);
                                foundMainLib = true;
                                Debug.Log($"[LocalAI] Extracted main lib: {mainPath}");
                            }
                            
                            // ALWAYS extract with original name too (for dependencies like libggml looking for each other)
                            string originalPath = Path.Combine(targetFolder, entry.Name);
                            if (!File.Exists(originalPath)) // Don't overwrite if we just did it above (though ExtractToFile(true) handles it)
                            {
                                entry.ExtractToFile(originalPath, true);
                                Debug.Log($"[LocalAI] Extracted dependency: {originalPath}");
                            }
                        }
                    }

                    if (!foundMainLib)
                    {
                        // Fail if we couldn't find the main binary
                         throw new Exception($"Could not find valid {libName} (binary > 100KB) in the downloaded archive.");
                    }
                }

                // POST-PROCESSING: Fix dependencies for ALL libggml components
                // The zip contains versioned files (e.g. libggml-cpu.0.9.4.dylib)
                // But binaries are linked against the major version (e.g. libggml-cpu.0.dylib)
                if (platform.Contains("macOS"))
                {
                    // 1. Handle the base libggml.0.dylib (alias from libggml-base)
                    // 1. Handle the base libggml.0.dylib (alias from generic libggml if possible, else base)
                    string baseExpected = Path.Combine(targetFolder, "libggml.0.dylib");
                    if (!File.Exists(baseExpected))
                    {
                        // Priority 1: The main 'libggml.x.y.z.dylib'. This contains the backend registry!
                        // "libggml.*.dylib" matches "libggml.0.9.4.dylib" but NOT "libggml-base..."
                        var mainCandidates = Directory.GetFiles(targetFolder, "libggml.*.dylib");
                        if (mainCandidates.Length > 0)
                        {
                            Debug.Log($"[LocalAI] Creating base alias (main): {mainCandidates[0]} -> {baseExpected}");
                            File.Copy(mainCandidates[0], baseExpected);
                        }
                        else 
                        {
                            // Priority 2: Fallback to base if main is missing (unlikely for full builds)
                            var candidates = Directory.GetFiles(targetFolder, "libggml-base*.dylib");
                            if (candidates.Length > 0)
                            {
                                Debug.Log($"[LocalAI] Creating base alias (base-fallback): {candidates[0]} -> {baseExpected}");
                                File.Copy(candidates[0], baseExpected);
                            }
                        }
                    }

                    // 2. Handle all other components (cpu, metal, rpc, etc.)
                    // Pattern: libggml-{backend}.X.Y.Z.dylib -> libggml-{backend}.X.dylib
                    var allLibs = Directory.GetFiles(targetFolder, "libggml-*.dylib");
                    foreach (var libPath in allLibs)
                    {
                        string libFileName = Path.GetFileName(libPath);
                        // Example: libggml-cpu.0.9.4.dylib
                        // We want: libggml-cpu.0.dylib
                        
                        // heuristic: replace the full version suffix with just the major version
                        // Match .0.9.4.dylib -> .0.dylib
                        if (libFileName.Contains(".0.9.4.dylib")) // current version specific
                        {
                             string aliasName = libFileName.Replace(".0.9.4.dylib", ".0.dylib");
                             string aliasPath = Path.Combine(targetFolder, aliasName);
                             if (!File.Exists(aliasPath))
                             {
                                 Debug.Log($"[LocalAI] Creating component alias: {libFileName} -> {aliasName}");
                                 File.Copy(libPath, aliasPath);
                             }
                        }
                        // Fallback generic heuristic if version changes in future
                        else if (System.Text.RegularExpressions.Regex.IsMatch(libFileName, @"\.(\d+)\.\d+\.\d+\.dylib$"))
                        {
                            string aliasName = System.Text.RegularExpressions.Regex.Replace(libFileName, @"\.(\d+)\.\d+\.\d+\.dylib$", ".$1.dylib");
                            string aliasPath = Path.Combine(targetFolder, aliasName);
                            if (!File.Exists(aliasPath) && aliasName != libFileName)
                            {
                                Debug.Log($"[LocalAI] Creating component alias (regex): {libFileName} -> {aliasName}");
                                 File.Copy(libPath, aliasPath);
                            }
                        }
                    }
                    
                    // 3. SPECIAL CASE: libggml-blas
                    // Sometimes missing from the zip but required by linker. 
                    // On macOS, Accelerate is used, so we can often alias base/cpu to blas (shim).
                    string blasExpected = Path.Combine(targetFolder, "libggml-blas.0.dylib");
                    if (!File.Exists(blasExpected))
                    {
                         // Alias base/ggml.0.dylib to blas
                         string baseLib = Path.Combine(targetFolder, "libggml.0.dylib");
                         if (File.Exists(baseLib))
                         {
                             Debug.Log($"[LocalAI] Creating fallback alias for BLAS: {baseLib} -> {blasExpected}");
                             File.Copy(baseLib, blasExpected);
                         }
                    }
                }

                EditorUtility.DisplayProgressBar("Installing", "Refreshing Asset Database...", 0.9f);
                AssetDatabase.Refresh();
                
                // Configure Plugin settings if needed (enable for Editor)
                // usually Unity auto-enables for Editor if in Plugins folder.

                EditorUtility.DisplayDialog("Success", $"Native library installed to {targetFolder}", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] Install failed: {ex.Message}");
                EditorUtility.DisplayDialog("Installation Failed", ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
            }
        }

        private static bool IsValidZip(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;

            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var header = new byte[4];
                    if (fs.Read(header, 0, 4) < 4) return false;
                    // ZIP magic bytes: PK\x03\x04
                    return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
