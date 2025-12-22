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
            // === Cloud AI Provider Section ===
            var cloudSection = new VisualElement();
            cloudSection.style.marginTop = 12;
            cloudSection.style.paddingTop = 8;
            cloudSection.style.borderTopWidth = 1;
            cloudSection.style.borderTopColor = new Color(0.14f, 0.14f, 0.14f);
            
            var cloudTitle = new Label("Cloud AI Provider");
            cloudTitle.style.fontSize = 12;
            cloudTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            cloudTitle.style.marginBottom = 8;
            cloudTitle.style.color = new Color(0.77f, 0.77f, 0.77f);
            cloudSection.Add(cloudTitle);
            
            // Provider Dropdown
            var providerLabel = new Label("AI Provider");
            providerLabel.style.fontSize = 11;
            providerLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            providerLabel.style.marginBottom = 4;
            cloudSection.Add(providerLabel);
            
            var providerList = new System.Collections.Generic.List<string>(LocalAISettings.ProviderLabels);
            var providerDropdown = new PopupField<string>(providerList, (int)LocalAISettings.ActiveProvider);
            providerDropdown.style.marginBottom = 12;
            cloudSection.Add(providerDropdown);
            
            // Gemini Model (visible only for Gemini)
            var geminiModelContainer = new VisualElement();
            geminiModelContainer.style.marginBottom = 12;
            geminiModelContainer.style.display = DisplayStyle.None;
            
            var geminiModelLabel = new Label("Gemini Model");
            geminiModelLabel.style.fontSize = 11;
            geminiModelLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            geminiModelLabel.style.marginBottom = 4;
            geminiModelContainer.Add(geminiModelLabel);
            
            var geminiModelList = new System.Collections.Generic.List<string>(LocalAISettings.GeminiModelLabels);
            var geminiModelDropdown = new PopupField<string>(geminiModelList, LocalAISettings.GetGeminiModelIndex());
            geminiModelDropdown.RegisterValueChangedCallback(evt => {
                int index = geminiModelList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.GeminiModel = LocalAISettings.GeminiModelOptions[index];
            });
            geminiModelContainer.Add(geminiModelDropdown);
            cloudSection.Add(geminiModelContainer);


            // API Key Container (will show/hide based on provider)
            var apiKeyContainer = new VisualElement();
            apiKeyContainer.style.marginBottom = 8;
            
            var apiKeyLabel = new Label("API Key");
            apiKeyLabel.style.fontSize = 11;
            apiKeyLabel.style.color = new Color(0.53f, 0.53f, 0.53f);
            apiKeyLabel.style.marginBottom = 4;
            apiKeyContainer.Add(apiKeyLabel);
            
            var apiKeyField = new TextField();
            apiKeyField.isPasswordField = true;
            apiKeyField.style.marginBottom = 4;
            apiKeyContainer.Add(apiKeyField);
            
            // Status label
            var statusLabel = new Label("");
            statusLabel.style.fontSize = 10;
            statusLabel.style.marginBottom = 8;
            apiKeyContainer.Add(statusLabel);
            
            // Get API key link
            var linkLabel = new Label("");
            linkLabel.style.fontSize = 10;
            linkLabel.style.color = new Color(0.4f, 0.6f, 0.9f);
            linkLabel.style.cursor = StyleKeyword.None;
            apiKeyContainer.Add(linkLabel);
            
            cloudSection.Add(apiKeyContainer);
            
            // Helper to update API key field and status
            void UpdateApiKeySection(AIProvider provider)
            {
                bool showApiKey = provider != AIProvider.Local;
                apiKeyContainer.style.display = showApiKey ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Show/Hide Gemini Model
                geminiModelContainer.style.display = (provider == AIProvider.Gemini) ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (showApiKey)
                {
                    apiKeyField.value = LocalAISettings.GetApiKey(provider);
                    bool hasKey = LocalAISettings.HasApiKey(provider);
                    statusLabel.text = hasKey ? "✓ API Key configured" : "⚠ API Key required";
                    statusLabel.style.color = hasKey ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.6f, 0.2f);
                    
                    // Update link based on provider
                    switch (provider)
                    {
                        case AIProvider.Gemini:
                            linkLabel.text = "Get API key: aistudio.google.com";
                            break;
                        case AIProvider.OpenAI:
                            linkLabel.text = "Get API key: platform.openai.com";
                            break;
                        case AIProvider.Claude:
                            linkLabel.text = "Get API key: console.anthropic.com";
                            break;
                    }
                }
            }
            
            // Provider change handler
            providerDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = providerList.IndexOf(evt.newValue);
                if (index >= 0)
                {
                    AIProvider newProvider = (AIProvider)index;
                    // Only save if actually different from current stored value
                    if (newProvider != LocalAISettings.ActiveProvider)
                    {
                        LocalAISettings.ActiveProvider = newProvider;
                        Debug.Log($"[LocalAI] Provider changed to: {LocalAISettings.ActiveProvider}");
                    }
                    UpdateApiKeySection(newProvider);
                }
            });
            
            // API key change handler
            apiKeyField.RegisterValueChangedCallback(evt =>
            {
                AIProvider provider = LocalAISettings.ActiveProvider;
                switch (provider)
                {
                    case AIProvider.Gemini:
                        LocalAISettings.GeminiApiKey = evt.newValue;
                        break;
                    case AIProvider.OpenAI:
                        LocalAISettings.OpenAIApiKey = evt.newValue;
                        break;
                    case AIProvider.Claude:
                        LocalAISettings.ClaudeApiKey = evt.newValue;
                        break;
                }
                UpdateApiKeySection(provider);
            });
            
            // Initial state
            UpdateApiKeySection(LocalAISettings.ActiveProvider);
            
            _settingsContainer.Add(cloudSection);
            
            // === AI Settings Section (existing) ===
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
