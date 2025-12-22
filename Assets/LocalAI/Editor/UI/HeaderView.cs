using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class HeaderView
    {
        private readonly ModelManager _modelManager;
        private readonly VisualElement _statusDot;
        private readonly Label _modelLabel;
        private readonly Button _settingsBtn;

        public HeaderView(VisualElement root, ModelManager modelManager)
        {
            _modelManager = modelManager;
            _statusDot = root.Q<VisualElement>("status-dot");
            _modelLabel = root.Q<Label>("model-label");
            _settingsBtn = root.Q<Button>("btn-settings");

            _modelManager.OnStateChanged += OnStateChanged;
            _modelManager.OnDownloadProgress += OnDownloadProgress;
            
            _settingsBtn.clicked += OnSettingsClicked;

            LocalAISettings.OnProviderChanged += (p) => UpdateUI();
            
            UpdateUI();
        }

        private void OnSettingsClicked()
        {
            // TODO: Open Settings
        }

        private void OnStateChanged(ModelManager.ModelState state)
        {
            UpdateUI();
        }

        private void OnDownloadProgress(DownloadProgress progress)
        {
            // Marshal to main thread for UI update
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (LocalAISettings.ActiveProvider == AIProvider.Local && 
                    _modelManager.CurrentState == ModelManager.ModelState.Downloading)
                {
                    _modelLabel.text = progress.GetDisplayText();
                }
            };
        }

        private void UpdateUI()
        {
            _statusDot.RemoveFromClassList("ready");
            _statusDot.RemoveFromClassList("downloading");
            _statusDot.RemoveFromClassList("error");

            var provider = LocalAISettings.ActiveProvider;

            if (provider != AIProvider.Local)
            {
                // Cloud Provider Logic
                bool hasKey = LocalAISettings.HasApiKey(provider);
                if (hasKey)
                {
                    _statusDot.AddToClassList("ready");
                    _modelLabel.text = $"Active: {provider}";
                }
                else
                {
                    _statusDot.AddToClassList("error"); // Or yellow?
                    _modelLabel.text = $"{provider} (No API Key)";
                }
                return;
            }

            // Local Model Logic
            var state = _modelManager.CurrentState;
            switch (state)
            {
                case ModelManager.ModelState.Ready:
                    _statusDot.AddToClassList("ready");
                    _modelLabel.text = "Model Ready (Mistral-7B)";
                    break;
                case ModelManager.ModelState.Downloading:
                    _statusDot.AddToClassList("downloading");
                    // Text handled by progress
                    break;
                case ModelManager.ModelState.NotInstalled:
                    _modelLabel.text = "Model Missing";
                    break;
                case ModelManager.ModelState.Error:
                    _statusDot.AddToClassList("error");
                    _modelLabel.text = "Error: " + _modelManager.ErrorMessage;
                    break;
            }
        }
    }
}
