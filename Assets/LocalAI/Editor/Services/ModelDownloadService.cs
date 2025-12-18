using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Security.Cryptography;
using System.Diagnostics;

namespace LocalAI.Editor.Services
{
    public struct DownloadProgress
    {
        public float Percentage;
        public long BytesDownloaded;
        public long TotalBytes;
        public double SpeedMBps; // MB/s
        
        public string GetDisplayText()
        {
            double downloadedMB = BytesDownloaded / (1024.0 * 1024.0);
            double totalMB = TotalBytes / (1024.0 * 1024.0);
            return $"Downloading... {Percentage:P0} | {downloadedMB:F1} / {totalMB:F0} MB | {SpeedMBps:F1} MB/s";
        }
    }

    public class ModelDownloadService
    {
        private static HttpClient _httpClient;

        static ModelDownloadService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromHours(2);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LocalAI-Unity/1.0");
        }

        public async Task<bool> DownloadFileAsync(string url, string destinationPath, IProgress<DownloadProgress> progress, CancellationToken token)
        {
            string tempPath = destinationPath + ".tmp";
            long existingLength = 0;

            if (File.Exists(tempPath))
            {
                existingLength = new FileInfo(tempPath).Length;
                UnityEngine.Debug.Log($"[LocalAI] Resuming download from {existingLength / (1024.0 * 1024.0):F1} MB");
            }

            try
            {
                UnityEngine.Debug.Log($"[LocalAI] Starting download from: {url}");
                
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (existingLength > 0)
                    {
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                    }

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        UnityEngine.Debug.Log($"[LocalAI] Response status: {response.StatusCode}");
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                            {
                                if (File.Exists(tempPath))
                                {
                                    MoveToFinal(tempPath, destinationPath);
                                    progress?.Report(new DownloadProgress { Percentage = 1f, BytesDownloaded = existingLength, TotalBytes = existingLength, SpeedMBps = 0 });
                                    return true;
                                }
                            }
                            
                            UnityEngine.Debug.LogError($"[LocalAI] Download failed: {response.StatusCode} - {response.ReasonPhrase}");
                            return false;
                        }

                        long? contentLength = response.Content.Headers.ContentLength;
                        long totalDownloadSize = (contentLength ?? 0L) + existingLength;

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempPath, existingLength > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            var buffer = new byte[81920];
                            long totalRead = existingLength;
                            int bytesRead;
                            
                            // For speed calculation
                            var stopwatch = Stopwatch.StartNew();
                            long bytesThisSecond = 0;
                            double currentSpeed = 0;
                            DateTime lastSpeedUpdate = DateTime.Now;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                                totalRead += bytesRead;
                                bytesThisSecond += bytesRead;

                                // Update speed every second
                                double elapsed = (DateTime.Now - lastSpeedUpdate).TotalSeconds;
                                if (elapsed >= 1.0)
                                {
                                    currentSpeed = (bytesThisSecond / elapsed) / (1024.0 * 1024.0); // MB/s
                                    bytesThisSecond = 0;
                                    lastSpeedUpdate = DateTime.Now;
                                    
                                    // Report progress
                                    float pct = totalDownloadSize > 0 ? (float)totalRead / totalDownloadSize : 0;
                                    progress?.Report(new DownloadProgress
                                    {
                                        Percentage = pct,
                                        BytesDownloaded = totalRead,
                                        TotalBytes = totalDownloadSize,
                                        SpeedMBps = currentSpeed
                                    });
                                }
                            }
                            
                            // Final update
                            progress?.Report(new DownloadProgress
                            {
                                Percentage = 1f,
                                BytesDownloaded = totalRead,
                                TotalBytes = totalDownloadSize,
                                SpeedMBps = currentSpeed
                            });
                        }
                    }
                }

                UnityEngine.Debug.Log($"[LocalAI] Download complete, moving to final location");
                MoveToFinal(tempPath, destinationPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                UnityEngine.Debug.Log("[LocalAI] Download cancelled by user.");
                return false;
            }
            catch (HttpRequestException ex)
            {
                UnityEngine.Debug.LogError($"[LocalAI] Network error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalAI] Download error: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        private void MoveToFinal(string tempPath, string finalPath)
        {
            if (!File.Exists(tempPath))
            {
                UnityEngine.Debug.LogWarning("[LocalAI] Temp file not found, cannot move.");
                return;
            }
            
            if (File.Exists(finalPath)) 
            {
                File.Delete(finalPath);
            }
            
            File.Move(tempPath, finalPath);
            UnityEngine.Debug.Log($"[LocalAI] Model saved to: {finalPath}");
        }

        public async Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256)
        {
            if (!File.Exists(filePath)) return false;

            return await Task.Run(() =>
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return hashString.Equals(expectedSha256.ToLowerInvariant());
                }
            });
        }
    }
}
