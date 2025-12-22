using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ActionBarView
    {
        private readonly ModelManager _modelManager;
        private readonly InferenceService _localInferenceService;
        private readonly GeminiInferenceService _geminiService;
        private readonly OpenAIInferenceService _openAIService;
        private readonly ClaudeInferenceService _claudeService;
        private readonly ContextView _contextView;
        private readonly ResponseView _responseView;

        private readonly Button _btnAsk;
        private readonly Button _btnExplainError;
        private readonly Button _btnExplainCode;
        private readonly Button _btnGenerate;
        private readonly Button _btnCancel;

        private CancellationTokenSource _cts;

        public ActionBarView(VisualElement root, ModelManager modelManager, InferenceService inferenceService, ContextView contextView, ResponseView responseView)
        {
            _modelManager = modelManager;
            _localInferenceService = inferenceService;
            _contextView = contextView;
            _responseView = responseView;
            
            // Initialize cloud services
            _geminiService = new GeminiInferenceService();
            _openAIService = new OpenAIInferenceService();
            _claudeService = new ClaudeInferenceService();

            _btnAsk = root.Q<Button>("btn-ask");
            _btnExplainError = root.Q<Button>("btn-explain-error");
            _btnExplainCode = root.Q<Button>("btn-explain-code");
            _btnGenerate = root.Q<Button>("btn-generate");
            _btnCancel = root.Q<Button>("btn-cancel");

            _btnAsk.clicked += () => StartInference("Question:");
            _btnExplainError.clicked += () => StartInference("Explain the following error context:");
            _btnExplainCode.clicked += () => StartInference("Explain this code:");
            _btnGenerate.clicked += () => StartInference("Generate a script for:");
            
            _btnCancel.clicked += CancelInference;

            _modelManager.OnStateChanged += OnModelStateChanged;
            UpdateButtonStates();
        }

        private void OnModelStateChanged(ModelManager.ModelState state)
        {
            UpdateButtonStates();
        }
        
        private void UpdateButtonStates()
        {
            AIProvider provider = LocalAISettings.ActiveProvider;
            bool ready = false;
            bool showDownload = false;
            
            if (provider == AIProvider.Local)
            {
                ready = _modelManager.CurrentState == ModelManager.ModelState.Ready;
                showDownload = _modelManager.CurrentState == ModelManager.ModelState.NotInstalled;
            }
            else
            {
                // Cloud providers - check if API key is configured
                ready = LocalAISettings.HasApiKey(provider);
            }
            
            _btnAsk.SetEnabled(ready);
            _btnExplainError.SetEnabled(ready);
            _btnExplainCode.SetEnabled(ready);
            _btnGenerate.SetEnabled(ready);

            if (showDownload && provider == AIProvider.Local)
            {
                _btnGenerate.text = "Download Model";
                _btnGenerate.SetEnabled(true);
                _btnGenerate.clicked -= DownloadModel;
                _btnGenerate.clicked += DownloadModel;
            }
            else if (!ready && provider != AIProvider.Local)
            {
                _btnGenerate.text = "Configure API Key";
                _btnGenerate.SetEnabled(false);
            }
            else
            {
                _btnGenerate.text = "Generate";
            }
        }

        private void DownloadModel()
        {
            _modelManager.StartDownload();
        }

        private IInferenceService GetActiveService()
        {
            AIProvider provider = LocalAISettings.ActiveProvider;
            Debug.Log($"[LocalAI] GetActiveService - Provider: {provider}");
            return provider switch
            {
                AIProvider.Gemini => _geminiService,
                AIProvider.OpenAI => _openAIService,
                AIProvider.Claude => _claudeService,
                _ => _localInferenceService
            };
        }

        private async void StartInference(string prefix)
        {
             // Cleanup hack
             _btnGenerate.clicked -= DownloadModel;
             
             AIProvider provider = LocalAISettings.ActiveProvider;
             Debug.Log($"[LocalAI] StartInference - Active Provider: {provider}");
             
             // Check readiness based on provider
             if (provider == AIProvider.Local)
             {
                 if (_modelManager.CurrentState != ModelManager.ModelState.Ready) return;
                 _localInferenceService.SetModelPath(_modelManager.GetModelPath());
             }
             else
             {
                 if (!LocalAISettings.HasApiKey(provider)) return;
             }

             _cts = new CancellationTokenSource();
             
             _btnAsk.style.display = DisplayStyle.None;
             _btnExplainError.style.display = DisplayStyle.None;
             _btnExplainCode.style.display = DisplayStyle.None;
             _btnGenerate.style.display = DisplayStyle.None;
             _btnCancel.style.display = DisplayStyle.Flex;

             _responseView.SetText("");
             string context = _contextView.GetContext();
             
             string fullPrompt;
             if (provider == AIProvider.Local)
             {
                string systemPrompt = "";
                 
                 if (prefix.Contains("Error"))
                 {
                     // DIAGNOSTIC PROMPT
                     systemPrompt = 
                         "You are a specific, deterministic Unity 2021.3+ C# expert. Fix errors and explain why.\n" +
                         "RULES:\n" +
                         "1. RESPONSE FORMAT: Diagnosis (1 sentence) -> Fix (1 sentence) -> Code Block.\n" +
                         "2. EXPLAIN WHY the error happened before fixing.\n" +
                         "3. OUTPUT valid C# code.";
                 }
                 else if (prefix.Contains("Question"))
                 {
                     // Q&A PROMPT
                     systemPrompt = 
                         "You are a Unity C# Expert. Answer the user's question clearly and concisely.\n" +
                         "RULES:\n" +
                         "1. Explain the concept simply.\n" +
                         "2. Provide a short C# code example to illustrate.\n" +
                         "3. Do not invent APIs.";
                 }
                 else
                 {
                     // GENERIC / GENERATION PROMPT
                     systemPrompt = 
                         "You are a Unity C# Expert. Write clean, optimized scripts.\n" +
                         "RULES:\n" +
                         "1. Use standard Unity patterns (Awake, SerializeField).\n" +
                         "2. Output only valid C# in markdown blocks.\n" +
                         "3. Be concise.";
                 }
                 
                 fullPrompt = $"[INST] {systemPrompt}\n\nTASK: {prefix}\n\nCONTEXT:\n{context} [/INST]";
             }
             else
             {
                 // Plain prompt for cloud providers (they handle system prompts internally)
                 fullPrompt = $"{prefix}\n\n{context}";
             }

             var progress = new System.Progress<string>(token => 
             {
                 _responseView.AppendText(token);
             });

             IInferenceService service = GetActiveService();
             await service.StartInferenceAsync(fullPrompt, progress, _cts.Token);

             _btnAsk.style.display = DisplayStyle.Flex;
             _btnExplainError.style.display = DisplayStyle.Flex;
             _btnExplainCode.style.display = DisplayStyle.Flex;
             _btnGenerate.style.display = DisplayStyle.Flex;
             _btnCancel.style.display = DisplayStyle.None;
             
             UpdateButtonStates();
        }

        private void CancelInference()
        {
            _cts?.Cancel();
        }
    }
}
