using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;
using UnityEditor;

namespace LocalAI.Editor.UI
{
    public class SettingsView
    {
        private readonly ModelManager _modelManager;
        private readonly VisualElement _settingsContainer;
        private readonly VisualElement _mainContainer;
        
        private readonly TextField _pathField;
        private readonly Button _deleteBtn;
        private readonly Button _closeBtn;
        
        private PopupField<string> _contextSizeDropdown;
        private PopupField<string> _maxTokensDropdown;

        public SettingsView(VisualElement root, ModelManager modelManager)
        {
            _modelManager = modelManager;
            _settingsContainer = root.Q<VisualElement>("settings-container");
            _mainContainer = root.Q<VisualElement>("content-container");

            _pathField = root.Q<TextField>("field-model-path");
            _deleteBtn = root.Q<Button>("btn-delete-model");
            _closeBtn = root.Q<Button>("btn-close-settings");

            if (_pathField != null) _pathField.value = _modelManager.GetModelDirectory();
            if (_deleteBtn != null) _deleteBtn.clicked += DeleteModel;
            if (_closeBtn != null) _closeBtn.clicked += Hide;
            
            if (_settingsContainer != null)
            {
                CreateAISettingsSection();
            }
        }
        
        private void CreateAISettingsSection()
        {
            var aiSection = new VisualElement();
            aiSection.style.marginTop = 12;
            aiSection.style.paddingTop = 8;
            aiSection.style.borderTopWidth = 1;
            aiSection.style.borderTopColor = new Color(0.14f, 0.14f, 0.14f);
            
            var sectionTitle = new Label("AI Settings");
            sectionTitle.style.fontSize = 12;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 8;
            sectionTitle.style.color = new Color(0.77f, 0.77f, 0.77f);
            aiSection.Add(sectionTitle);
            
            // Context Size
            var contextLabel = new Label("Context Size");
            contextLabel.style.fontSize = 11;
            contextLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            contextLabel.style.marginBottom = 4;
            aiSection.Add(contextLabel);
            
            var contextSizeList = new System.Collections.Generic.List<string>(LocalAISettings.ContextSizeLabels);
            _contextSizeDropdown = new PopupField<string>(contextSizeList, LocalAISettings.GetContextSizeIndex());
            _contextSizeDropdown.style.marginBottom = 12;
            _contextSizeDropdown.RegisterValueChangedCallback(evt => {
                int index = contextSizeList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.ContextSize = LocalAISettings.ContextSizeOptions[index];
                Debug.Log($"[LocalAI] Context size: {LocalAISettings.ContextSize}");
            });
            aiSection.Add(_contextSizeDropdown);
            
            // Max Response Tokens
            var maxTokensLabel = new Label("Max Response Length");
            maxTokensLabel.style.fontSize = 11;
            maxTokensLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            maxTokensLabel.style.marginBottom = 4;
            aiSection.Add(maxTokensLabel);
            
            var maxTokensList = new System.Collections.Generic.List<string>(LocalAISettings.MaxTokensLabels);
            _maxTokensDropdown = new PopupField<string>(maxTokensList, LocalAISettings.GetMaxTokensIndex());
            _maxTokensDropdown.style.marginBottom = 8;
            _maxTokensDropdown.RegisterValueChangedCallback(evt => {
                int index = maxTokensList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.MaxTokens = LocalAISettings.MaxTokensOptions[index];
                Debug.Log($"[LocalAI] Max tokens: {LocalAISettings.MaxTokens}");
            });
            aiSection.Add(_maxTokensDropdown);
            
            var noteLabel = new Label("Changes apply on next query. Larger context uses more RAM.");
            noteLabel.style.fontSize = 10;
            noteLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            noteLabel.style.whiteSpace = WhiteSpace.Normal;
            aiSection.Add(noteLabel);
            
            _settingsContainer.Add(aiSection);
        }

        public void Show()
        {
            if (_pathField != null) _pathField.value = _modelManager.GetModelDirectory();
            if (_settingsContainer != null) _settingsContainer.style.display = DisplayStyle.Flex;
            if (_mainContainer != null) _mainContainer.style.display = DisplayStyle.None;
        }

        public void Hide()
        {
            if (_settingsContainer != null) _settingsContainer.style.display = DisplayStyle.None;
            if (_mainContainer != null) _mainContainer.style.display = DisplayStyle.Flex;
        }

        private void DeleteModel()
        {
            if (EditorUtility.DisplayDialog("Delete Model?", "Are you sure you want to delete the downloaded model?", "Yes", "No"))
            {
                Debug.Log("[LocalAI] Model deleted");
            }
        }
    }
}
