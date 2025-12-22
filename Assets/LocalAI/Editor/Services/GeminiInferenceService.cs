using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Google Gemini API inference service.
    /// </summary>
    public class GeminiInferenceService : IInferenceService
    {
        // Base URL, model will be appended
        private const string BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models";
        private static readonly HttpClient _httpClient = new HttpClient();
        
        public string DisplayName => "Google Gemini";
        
        public bool IsReady => LocalAISettings.HasApiKey(AIProvider.Gemini);
        
        public async Task StartInferenceAsync(string prompt, IProgress<string> progress, CancellationToken token)
        {
            string apiKey = LocalAISettings.GeminiApiKey;
            
            if (string.IsNullOrEmpty(apiKey))
            {
                progress?.Report("[Error] Gemini API key not configured. Go to Settings to add your API key.\n");
                return;
            }
            
            try
            {
                string modelName = LocalAISettings.GeminiModel;
                string url = $"{BASE_URL}/{modelName}:generateContent?key={apiKey}";
                
                // Build request body
                string systemPrompt = "You are a Senior Unity Developer. Expert in C# and Unity Engine. Provide concise, correct answers using best practices. If the user asks for code, write clean, optimized C#. If asking to explain, provide clear explanations. IMPORTANT: Wrap any code examples in markdown blocks.";
                string requestBody = BuildRequestBody(systemPrompt, prompt);
                
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                
                progress?.Report($"[Connecting to {modelName}...]\n");
                
                var response = await _httpClient.PostAsync(url, content, token);
                string responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report($"[Error] Gemini API error ({response.StatusCode}): {responseBody}\n");
                    return;
                }
                
                // Parse response
                string generatedText = ParseResponse(responseBody);
                
                if (!string.IsNullOrEmpty(generatedText))
                {
                    progress?.Report(generatedText);
                }
                else
                {
                    progress?.Report("[Error] Empty response from Gemini API.\n");
                }
            }
            catch (TaskCanceledException)
            {
                progress?.Report("\n[Cancelled]");
            }
            catch (Exception ex)
            {
                progress?.Report($"\n[Error] {ex.Message}");
                Debug.LogError($"[LocalAI] Gemini error: {ex}");
            }
        }
        
        private string BuildRequestBody(string systemPrompt, string userPrompt)
        {
            // Gemini API format with System Instruction
            return $@"{{
  ""systemInstruction"": {{
    ""parts"": [
      {{ ""text"": ""{EscapeJson(systemPrompt)}"" }}
    ]
  }},
  ""contents"": [
    {{
      ""parts"": [
        {{ ""text"": ""{EscapeJson(userPrompt)}"" }}
      ]
    }}
  ],
  ""generationConfig"": {{
    ""maxOutputTokens"": {LocalAISettings.MaxTokens},
    ""temperature"": 0.4
  }}
}}";
        }
        
        private string ParseResponse(string json)
        {
            // Simple JSON parsing for Gemini response
            // Response format: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
            try
            {
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
