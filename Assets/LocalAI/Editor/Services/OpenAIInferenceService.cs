using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// OpenAI API inference service.
    /// </summary>
    public class OpenAIInferenceService : IInferenceService
    {
        private const string API_ENDPOINT = "https://api.openai.com/v1/chat/completions";
        private const string MODEL = "gpt-4o-mini";
        private static readonly HttpClient _httpClient = new HttpClient();
        
        public string DisplayName => "OpenAI";
        
        public bool IsReady => LocalAISettings.HasApiKey(AIProvider.OpenAI);
        
        public async Task StartInferenceAsync(string prompt, IProgress<string> progress, CancellationToken token)
        {
            string apiKey = LocalAISettings.OpenAIApiKey;
            
            if (string.IsNullOrEmpty(apiKey))
            {
                progress?.Report("[Error] OpenAI API key not configured. Go to Settings to add your API key.\n");
                return;
            }
            
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, API_ENDPOINT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                
                string systemPrompt = "You are a Unity/C# coding assistant. IMPORTANT: Only generate C# code for Unity. Never use Python, JavaScript, or other languages. All code examples must be valid C# for Unity.";
                string requestBody = BuildRequestBody(systemPrompt, prompt);
                
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                
                progress?.Report("[Connecting to OpenAI...]\n");
                
                var response = await _httpClient.SendAsync(request, token);
                string responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report($"[Error] OpenAI API error ({response.StatusCode}): {responseBody}\n");
                    return;
                }
                
                string generatedText = ParseResponse(responseBody);
                
                if (!string.IsNullOrEmpty(generatedText))
                {
                    progress?.Report(generatedText);
                }
                else
                {
                    progress?.Report("[Error] Empty response from OpenAI API.\n");
                }
            }
            catch (TaskCanceledException)
            {
                progress?.Report("\n[Cancelled]");
            }
            catch (Exception ex)
            {
                progress?.Report($"\n[Error] {ex.Message}");
                Debug.LogError($"[LocalAI] OpenAI error: {ex}");
            }
        }
        
        private string BuildRequestBody(string systemPrompt, string userPrompt)
        {
            return $@"{{
  ""model"": ""{MODEL}"",
  ""messages"": [
    {{
      ""role"": ""system"",
      ""content"": ""{EscapeJson(systemPrompt)}""
    }},
    {{
      ""role"": ""user"",
      ""content"": ""{EscapeJson(userPrompt)}""
    }}
  ],
  ""max_tokens"": {LocalAISettings.MaxTokens},
  ""temperature"": 0.7
}}";
        }
        
        private string ParseResponse(string json)
        {
            // OpenAI response format: {"choices":[{"message":{"content":"..."}}]}
            try
            {
                int contentStart = json.IndexOf("\"content\":");
                if (contentStart < 0) return null;
                
                contentStart = json.IndexOf("\"", contentStart + 10) + 1;
                int contentEnd = FindClosingQuote(json, contentStart);
                
                if (contentEnd > contentStart)
                {
                    string text = json.Substring(contentStart, contentEnd - contentStart);
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
