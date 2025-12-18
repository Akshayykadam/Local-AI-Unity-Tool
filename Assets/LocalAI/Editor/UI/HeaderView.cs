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

            UpdateUI(_modelManager.CurrentState);
        }

        private void OnSettingsClicked()
        {
            // TODO: Open Settings
        }

        private void OnStateChanged(ModelManager.ModelState state)
        {
            UpdateUI(state);
        }

        private void OnDownloadProgress(DownloadProgress progress)
        {
            // Marshal to main thread for UI update
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (_modelManager.CurrentState == ModelManager.ModelState.Downloading)
                {
                    _modelLabel.text = progress.GetDisplayText();
                }
            };
        }

        private void UpdateUI(ModelManager.ModelState state)
        {
            _statusDot.RemoveFromClassList("ready");
            _statusDot.RemoveFromClassList("downloading");
            _statusDot.RemoveFromClassList("error");

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
