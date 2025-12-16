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
        // Using release b7423 (Dec 2025)
        private const string BASE_URL = "https://github.com/ggerganov/llama.cpp/releases/download/b7423/";
        
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
                        throw new Exception($"Failed to download: {response.StatusCode}");
                    }

                    using (var s = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(tempZipPath, FileMode.Create))
                    {
                        await s.CopyToAsync(fs);
                    }
                }

                EditorUtility.DisplayProgressBar("Installing", "Extracting library...", 0.5f);

                if (Directory.Exists(targetFolder)) Directory.Delete(targetFolder, true);
                Directory.CreateDirectory(targetFolder);

                // Extract specific file
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    // llama.cpp zip structure puts libs in build/bin or directly root? 
                    // Usually directly in root or 'lib' folder in recent builds.
                    // We look for libllama.dylib / llama.dll
                    
                    bool found = false;
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name == libName)
                        {
                            string destPath = Path.Combine(targetFolder, libName);
                            entry.ExtractToFile(destPath, true);
                            found = true;
                            
                            // Enable Editor Reload of Plugins
                            var importer = AssetImporter.GetAtPath($"Assets/LocalAI/Plugins/{platform}/{libName}"); // Path relative to project
                            // Not strictly needed if we refresh
                            break;
                        }
                    }

                    if (!found)
                    {
                        // Fallback: look for generic name?
                         throw new Exception($"Could not find {libName} in the downloaded archive.");
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
    }
}
