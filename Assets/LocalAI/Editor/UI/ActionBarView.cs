using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ActionBarView
    {
        private readonly ModelManager _modelManager;
        private readonly InferenceService _inferenceService;
        private readonly ContextView _contextView;
        private readonly ResponseView _responseView;

        private readonly Button _btnExplainError;
        private readonly Button _btnExplainCode;
        private readonly Button _btnGenerate;
        private readonly Button _btnCancel;

        private CancellationTokenSource _cts;

        public ActionBarView(VisualElement root, ModelManager modelManager, InferenceService inferenceService, ContextView contextView, ResponseView responseView)
        {
            _modelManager = modelManager;
            _inferenceService = inferenceService;
            _contextView = contextView;
            _responseView = responseView;

            _btnExplainError = root.Q<Button>("btn-explain-error");
            _btnExplainCode = root.Q<Button>("btn-explain-code");
            _btnGenerate = root.Q<Button>("btn-generate");
            _btnCancel = root.Q<Button>("btn-cancel");

            _btnExplainError.clicked += () => StartInference("Explain the following error context:");
            _btnExplainCode.clicked += () => StartInference("Explain this code:");
            _btnGenerate.clicked += () => StartInference("Generate a script for:");
            
            _btnCancel.clicked += CancelInference;

            _modelManager.OnStateChanged += OnModelStateChanged;
            OnModelStateChanged(_modelManager.CurrentState);
        }

        private void OnModelStateChanged(ModelManager.ModelState state)
        {
            bool ready = state == ModelManager.ModelState.Ready;
            bool missing = state == ModelManager.ModelState.NotInstalled;

            _btnExplainError.SetEnabled(ready);
            _btnExplainCode.SetEnabled(ready);
            _btnGenerate.SetEnabled(ready);

            if (missing)
            {
                // TODO: Hack to show download button here if we don't have a separate DownloadView overlay yet
                _btnGenerate.text = "Download Model";
                _btnGenerate.SetEnabled(true);
                _btnGenerate.clicked -= DownloadModel; // prevent double sub
                _btnGenerate.clicked += DownloadModel;
            }
        }

        private void DownloadModel()
        {
            _modelManager.StartDownload();
        }

        private async void StartInference(string prefix)
        {
             // Cleanup hack
             _btnGenerate.clicked -= DownloadModel;
             if (_modelManager.CurrentState != ModelManager.ModelState.Ready) return;

             _cts = new CancellationTokenSource();
             
             _btnExplainError.style.display = DisplayStyle.None;
             _btnExplainCode.style.display = DisplayStyle.None;
             _btnGenerate.style.display = DisplayStyle.None;
             _btnCancel.style.display = DisplayStyle.Flex;

             _responseView.SetText("");
             string context = _contextView.GetContext();
             
             // System instruction: Enforce C#/Unity code only
             string systemPrompt = "You are a Unity/C# coding assistant. IMPORTANT: Only generate C# code for Unity. Never use Python, JavaScript, or other languages. All code examples must be valid C# for Unity.";
             
             // Mistral Instruct Template: [INST] Instruction [/INST]
             string fullPrompt = $"[INST] {systemPrompt}\n\n{prefix}\n\n{context} [/INST]";

             var progress = new System.Progress<string>(token => 
             {
                 _responseView.AppendText(token);
             });

             await _inferenceService.StartInferenceAsync(fullPrompt, _modelManager.GetModelPath(), progress, _cts.Token);

             _btnExplainError.style.display = DisplayStyle.Flex;
             _btnExplainCode.style.display = DisplayStyle.Flex;
             _btnGenerate.style.display = DisplayStyle.Flex;
             _btnCancel.style.display = DisplayStyle.None;
        }

        private void CancelInference()
        {
            _cts?.Cancel();
        }
    }
}
