using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Anthropic Claude API inference service.
    /// </summary>
    public class ClaudeInferenceService : IInferenceService
    {
        private const string API_ENDPOINT = "https://api.anthropic.com/v1/messages";
        private const string MODEL = "claude-3-haiku-20240307";
        private const string API_VERSION = "2023-06-01";
        private static readonly HttpClient _httpClient = new HttpClient();
        
        public string DisplayName => "Anthropic Claude";
        
        public bool IsReady => LocalAISettings.HasApiKey(AIProvider.Claude);
        
        public async Task StartInferenceAsync(string prompt, IProgress<string> progress, CancellationToken token)
        {
            string apiKey = LocalAISettings.ClaudeApiKey;
            
            if (string.IsNullOrEmpty(apiKey))
            {
                progress?.Report("[Error] Claude API key not configured. Go to Settings to add your API key.\n");
                return;
            }
            
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, API_ENDPOINT);
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", API_VERSION);
                
                string systemPrompt = "You are a Unity/C# coding assistant. IMPORTANT: Only generate C# code for Unity. Never use Python, JavaScript, or other languages. All code examples must be valid C# for Unity.";
                string requestBody = BuildRequestBody(systemPrompt, prompt);
                
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                
                progress?.Report("[Connecting to Claude...]\n");
                
                var response = await _httpClient.SendAsync(request, token);
                string responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report($"[Error] Claude API error ({response.StatusCode}): {responseBody}\n");
                    return;
                }
                
                string generatedText = ParseResponse(responseBody);
                
                if (!string.IsNullOrEmpty(generatedText))
                {
                    progress?.Report(generatedText);
                }
                else
                {
                    progress?.Report("[Error] Empty response from Claude API.\n");
                }
            }
            catch (TaskCanceledException)
            {
                progress?.Report("\n[Cancelled]");
            }
            catch (Exception ex)
            {
                progress?.Report($"\n[Error] {ex.Message}");
                Debug.LogError($"[LocalAI] Claude error: {ex}");
            }
        }
        
        private string BuildRequestBody(string systemPrompt, string userPrompt)
        {
            return $@"{{
  ""model"": ""{MODEL}"",
  ""max_tokens"": {LocalAISettings.MaxTokens},
  ""system"": ""{EscapeJson(systemPrompt)}"",
  ""messages"": [
    {{
      ""role"": ""user"",
      ""content"": ""{EscapeJson(userPrompt)}""
    }}
  ]
}}";
        }
        
        private string ParseResponse(string json)
        {
            // Claude response format: {"content":[{"type":"text","text":"..."}]}
            try
            {
                // Find text field within content array
                int textStart = json.IndexOf("\"text\":");
                if (textStart < 0) return null;
                
                textStart = json.IndexOf("\"", textStart + 7) + 1;
                int textEnd = FindClosingQuote(json, textStart);
                
                if (textEnd > textStart)
                {
                    string text = json.Substring(textStart, textEnd - textStart);
                    return UnescapeJson(text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] Parse error: {ex.Message}");
            }
            
            return null;
        }
        
        private int FindClosingQuote(string json, int start)
        {
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    return i;
            }
            return -1;
        }
        
        private string EscapeJson(string text)
        {
            return text?.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t") ?? "";
        }
        
        private string UnescapeJson(string text)
        {
            return text?.Replace("\\n", "\n")
                       .Replace("\\r", "\r")
                       .Replace("\\t", "\t")
                       .Replace("\\\"", "\"")
                       .Replace("\\\\", "\\") ?? "";
        }
        
        public void Dispose()
        {
            // HttpClient is static, no cleanup needed
        }
    }
}
