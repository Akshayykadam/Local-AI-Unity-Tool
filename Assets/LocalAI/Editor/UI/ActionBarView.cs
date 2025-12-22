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
            _contextView.OnContextUpdated += OnContextUpdated;
            UpdateButtonStates();
        }

        private void OnModelStateChanged(ModelManager.ModelState state)
        {
            UpdateButtonStates();
        }
        
        private bool _isContextTruncated = false;
        private void OnContextUpdated(ContextData data)
        {
            _isContextTruncated = data.IsTruncated;
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
            
            // Check context safety
            string tooltip = "";
            bool safe = ready;
            
            if (_isContextTruncated)
            {
                safe = false;
                tooltip = "Context exceeds limit. Please reduce selection.";
            }
            else if (!ready)
            {
                tooltip = provider == AIProvider.Local ? "Model not ready" : "API Key missing";
            }
            
            _btnAsk.SetEnabled(safe);
            _btnExplainError.SetEnabled(safe);
            _btnExplainCode.SetEnabled(safe);
            _btnGenerate.SetEnabled(safe);
            
            _btnAsk.tooltip = tooltip;
            _btnExplainError.tooltip = tooltip;
            _btnExplainCode.tooltip = tooltip;
            _btnGenerate.tooltip = tooltip;

            if (showDownload && provider == AIProvider.Local)
            {
                _btnGenerate.text = "Download Model";
                _btnGenerate.SetEnabled(true);
                _btnGenerate.tooltip = "Download required model (4GB)";
                _btnGenerate.clicked -= DownloadModel;
                _btnGenerate.clicked += DownloadModel;
            }
            else if (!ready && provider != AIProvider.Local && !_isContextTruncated)
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
                 // Use centralized AIPrompts for better accuracy
                 string systemPrompt = AIPrompts.GetSystemPrompt(prefix);
                 fullPrompt = AIPrompts.BuildFullPrompt(systemPrompt, prefix, context);
             }
             else
             {
                 // Plain prompt for cloud providers (Gemini/GPT handle system prompts differently or internally)
                 // For now, cloud just gets text + context.
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
