using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Security.Cryptography;

namespace LocalAI.Editor.Services
{
    public class ModelDownloadService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<bool> DownloadFileAsync(string url, string destinationPath, IProgress<float> progress, CancellationToken token)
        {
            string tempPath = destinationPath + ".tmp";
            long existingLength = 0;

            if (File.Exists(tempPath))
            {
                existingLength = new FileInfo(tempPath).Length;
            }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (existingLength > 0)
                    {
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                    }

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            // If range not satisfiable (e.g. download complete), check size
                            if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                            {
                                // Assumption: file is done. Verify checksum later.
                                MoveToFinal(tempPath, destinationPath);
                                progress?.Report(1.0f);
                                return true;
                            }
                            
                            Debug.LogError($"[LocalAI] Download failed: {response.StatusCode}");
                            return false;
                        }

                        long totalBytes = response.Content.Headers.ContentLength ?? 0L;
                        long totalDownloadSize = totalBytes + existingLength;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = existingLength;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                                totalRead += bytesRead;

                                if (totalDownloadSize > 0)
                                {
                                    progress?.Report((float)totalRead / totalDownloadSize);
                                }
                            }
                        }
                    }
                }

                MoveToFinal(tempPath, destinationPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[LocalAI] Download cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] Download error: {ex.Message}");
                return false;
            }
        }

        private void MoveToFinal(string tempPath, string finalPath)
        {
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
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
