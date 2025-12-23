using System.Threading;
using UnityEditor;
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
        private readonly Button _btnWriteTests;
        private readonly Button _btnAnalyze;
        private readonly Button _btnCancel;

        private readonly UnitTestGenerator _testGenerator;

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
            _btnWriteTests = root.Q<Button>("btn-write-tests");
            _btnAnalyze = root.Q<Button>("btn-analyze");
            _btnCancel = root.Q<Button>("btn-cancel");
            
            _testGenerator = new UnitTestGenerator();

            // Wire up buttons with null checks (some may not exist in tabbed UI)
            if (_btnAsk != null) _btnAsk.clicked += () => StartInference("Question:");
            if (_btnExplainError != null) _btnExplainError.clicked += () => StartInference("Explain the following error context:");
            if (_btnExplainCode != null) _btnExplainCode.clicked += () => StartInference("Explain this code:");
            if (_btnGenerate != null) _btnGenerate.clicked += () => StartInference("Generate a script for:");
            if (_btnWriteTests != null) _btnWriteTests.clicked += StartTestGeneration;
            if (_btnAnalyze != null) _btnAnalyze.clicked += AnalyzeScene;
            if (_btnCancel != null) _btnCancel.clicked += CancelInference;

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
            
            // Apply to buttons with null checks
            if (_btnAsk != null) { _btnAsk.SetEnabled(safe); _btnAsk.tooltip = tooltip; }
            if (_btnExplainError != null) { _btnExplainError.SetEnabled(safe); _btnExplainError.tooltip = tooltip; }
            if (_btnExplainCode != null) { _btnExplainCode.SetEnabled(safe); _btnExplainCode.tooltip = tooltip; }
            if (_btnWriteTests != null) { _btnWriteTests.SetEnabled(safe); _btnWriteTests.tooltip = tooltip; }
            if (_btnAnalyze != null) { _btnAnalyze.SetEnabled(safe); _btnAnalyze.tooltip = tooltip; }
            
            if (_btnGenerate != null)
            {
                _btnGenerate.SetEnabled(safe);
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
        }

        private void DownloadModel()
        {
            _modelManager.StartDownload();
        }

        public IInferenceService GetActiveService()
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

        private void AnalyzeScene()
        {
             string report = SceneAnalyzer.AnalyzeCurrentScene();
             StartInference("SceneAnalysis: Analyze this Unity Scene Report.", report);
        }

        private void StartTestGeneration()
        {
            if (!_testGenerator.ValidateSelection(out MonoScript script, out string error))
            {
                _responseView.SetText($"[Error] {error}\n\nPlease select a C# script (.cs file) in the Project window.");
                return;
            }

            string testPrompt = _testGenerator.BuildTestPrompt(script);
            string suggestedPath = _testGenerator.GetTestFilePath(script);
            
            _responseView.SetText($"Generating tests for: {script.name}\nSuggested path: {suggestedPath}\n\n");
            
            StartInference("UnitTest: ", testPrompt);
        }

        private async void StartInference(string prefix, string contextOverride = null)
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
             
             // Hide action buttons, show cancel (with null checks)
             if (_btnGenerate != null) _btnGenerate.style.display = DisplayStyle.None;
             if (_btnAnalyze != null) _btnAnalyze.style.display = DisplayStyle.None;
             if (_btnCancel != null) _btnCancel.style.display = DisplayStyle.Flex;

              _responseView.SetText("");
              string context = contextOverride ?? _contextView.GetContext();
             
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

             // Restore buttons after inference (with null checks)
             if (_btnAsk != null) _btnAsk.style.display = DisplayStyle.Flex;
             if (_btnExplainError != null) _btnExplainError.style.display = DisplayStyle.Flex;
             if (_btnExplainCode != null) _btnExplainCode.style.display = DisplayStyle.Flex;
             if (_btnGenerate != null) _btnGenerate.style.display = DisplayStyle.Flex;
             if (_btnAnalyze != null) _btnAnalyze.style.display = DisplayStyle.Flex;
             if (_btnCancel != null) _btnCancel.style.display = DisplayStyle.None;
             
             UpdateButtonStates();
        }

        private void CancelInference()
        {
            _cts?.Cancel();
        }
    }
}
