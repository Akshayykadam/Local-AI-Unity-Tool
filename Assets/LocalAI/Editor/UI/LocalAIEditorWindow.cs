using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;
using LocalAI.Editor.Services.SemanticSearch;

namespace LocalAI.Editor.UI
{
    public class LocalAIEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Local AI Assistant")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<LocalAIEditorWindow>();
            wnd.titleContent = new GUIContent("Local AI");
            wnd.minSize = new Vector2(650, 450);
        }

        // Services
        private ModelManager _modelManager;
        private InferenceService _inferenceService;
        private ContextCollector _contextCollector;
        private SemanticIndex _semanticIndex;

        // Tab System
        private TabSystem _tabSystem;
        
        // Views
        private HeaderView _headerView;
        private ActionBarView _actionBarView;
        
        // Tab content containers
        private VisualElement _chatTab;
        private VisualElement _searchTab;
        private VisualElement _analyzeTab;
        private VisualElement _actionsTab;
        private VisualElement _refactorTab;
        private VisualElement _settingsTab;

        public void CreateGUI()
        {
            // Initialize Services
            _modelManager = new ModelManager();
            _inferenceService = new InferenceService();
            _contextCollector = new ContextCollector();
            _semanticIndex = new SemanticIndex();

            // Load UI
            var visualTree = Resources.Load<VisualTreeAsset>("LocalAIWindow");
            if (visualTree == null)
            {
                Debug.LogError("[LocalAI] Could not find LocalAIWindow.uxml in Resources");
                return;
            }
            visualTree.CloneTree(rootVisualElement);

            // Get layout elements
            var tabBar = rootVisualElement.Q<VisualElement>("tab-bar");
            var contentArea = rootVisualElement.Q<VisualElement>("content-area");
            
            if (tabBar == null || contentArea == null)
            {
                Debug.LogError("[LocalAI] Could not find tab-bar or content-area in UXML");
                return;
            }

            // Initialize Tab System
            _tabSystem = new TabSystem(tabBar, contentArea);
            
            // Create tab contents
            CreateChatTab();
            CreateSearchTab();
            CreateAnalyzeTab();
            CreateActionsTab();
            CreateRefactorTab();
            CreateSettingsTab();
            
            // Add tabs (no emojis per user preference)
            _tabSystem.AddTab("", "Chat", "chat", _chatTab);
            _tabSystem.AddTab("", "Search", "search", _searchTab);
            _tabSystem.AddTab("", "Analyze", "analyze", _analyzeTab);
            _tabSystem.AddTab("", "Actions", "actions", _actionsTab);
            _tabSystem.AddTab("", "Refactor", "refactor", _refactorTab);
            _tabSystem.AddTab("", "Settings", "settings", _settingsTab);
            
            // Initialize header
            InitializeHeader();
            
            // Select first tab
            _tabSystem.SelectTab(0);
            
            // Update provider badge
            UpdateProviderBadge();
            LocalAISettings.OnProviderChanged += _ => UpdateProviderBadge();
        }

        private void InitializeHeader()
        {
            var statusDot = rootVisualElement.Q<VisualElement>("status-dot");
            
            // Helper to update status dot
            void UpdateStatusDot(ModelManager.ModelState state)
            {
                if (statusDot == null) return;
                
                statusDot.RemoveFromClassList("ready");
                statusDot.RemoveFromClassList("downloading");
                statusDot.RemoveFromClassList("error");
                
                switch (state)
                {
                    case ModelManager.ModelState.Ready:
                        statusDot.AddToClassList("ready");
                        break;
                    case ModelManager.ModelState.Downloading:
                        statusDot.AddToClassList("downloading");
                        break;
                    case ModelManager.ModelState.Error:
                        statusDot.AddToClassList("error");
                        break;
                }
            }
            
            // Set initial state
            UpdateStatusDot(_modelManager.CurrentState);
            
            // Subscribe to changes
            _modelManager.OnStateChanged += UpdateStatusDot;
        }
        
        private void UpdateProviderBadge()
        {
            var badge = rootVisualElement.Q<Label>("provider-label");
            if (badge != null)
            {
                badge.text = LocalAISettings.ActiveProvider switch
                {
                    AIProvider.Local => "Local",
                    AIProvider.Gemini => "Gemini",
                    AIProvider.OpenAI => "OpenAI",
                    AIProvider.Claude => "Claude",
                    _ => ""
                };
            }
        }

        private void CreateChatTab()
        {
            _chatTab = new VisualElement();
            _chatTab.style.flexGrow = 1;
            _chatTab.style.flexDirection = FlexDirection.Column;
            
            // Response Panel
            var responsePanel = new VisualElement();
            responsePanel.AddToClassList("panel");
            responsePanel.AddToClassList("response-panel");
            
            var responseHeader = new VisualElement();
            responseHeader.AddToClassList("panel-header");
            
            var responseTitle = new Label("Response");
            responseTitle.AddToClassList("panel-header-label");
            responseHeader.Add(responseTitle);
            
            var responseActions = new VisualElement();
            responseActions.name = "response-actions";
            responseActions.style.flexDirection = FlexDirection.Row;
            responseActions.style.flexGrow = 1;
            responseActions.style.justifyContent = Justify.FlexEnd;
            
            var btnCopy = new Button { name = "btn-copy", text = "Copy" };
            btnCopy.AddToClassList("header-btn");
            responseActions.Add(btnCopy);
            
            var btnApply = new Button { name = "btn-apply", text = "Apply" };
            btnApply.AddToClassList("header-btn");
            btnApply.style.backgroundColor = new Color(0.2f, 0.4f, 0.2f);
            responseActions.Add(btnApply);
            
            var btnClear = new Button { name = "btn-clear", text = "Clear" };
            btnClear.AddToClassList("header-btn");
            responseActions.Add(btnClear);
            
            responseHeader.Add(responseActions);
            
            responsePanel.Add(responseHeader);
            
            var responseScroll = new ScrollView();
            responseScroll.name = "response-scroll";
            responseScroll.AddToClassList("scroll-area");
            
            var responseText = new TextField();
            responseText.name = "response-text";
            responseText.AddToClassList("response-text");
            responseText.multiline = true;
            responseText.isReadOnly = true;
            responseText.value = "Select code or a GameObject, then click an action below.";
            responseScroll.Add(responseText);
            
            responsePanel.Add(responseScroll);
            _chatTab.Add(responsePanel);
            
            // Action Bar
            var actionBar = new VisualElement();
            actionBar.name = "action-bar-container";
            actionBar.AddToClassList("action-bar");
            
            var btnAsk = new Button { name = "btn-ask", text = "Ask" };
            btnAsk.AddToClassList("action-btn");
            actionBar.Add(btnAsk);
            
            var btnExplainError = new Button { name = "btn-explain-error", text = "Explain Error" };
            btnExplainError.AddToClassList("action-btn");
            actionBar.Add(btnExplainError);
            
            var btnExplainCode = new Button { name = "btn-explain-code", text = "Explain Code" };
            btnExplainCode.AddToClassList("action-btn");
            actionBar.Add(btnExplainCode);
            
            var btnGenerate = new Button { name = "btn-generate", text = "Generate" };
            btnGenerate.AddToClassList("action-btn");
            btnGenerate.AddToClassList("action-btn-primary");
            actionBar.Add(btnGenerate);
            
            var btnWriteTests = new Button { name = "btn-write-tests", text = "Tests" };
            btnWriteTests.AddToClassList("action-btn");
            actionBar.Add(btnWriteTests);
            
            var btnCancel = new Button { name = "btn-cancel", text = "Cancel" };
            btnCancel.AddToClassList("action-btn");
            btnCancel.AddToClassList("cancel-btn");
            btnCancel.style.display = DisplayStyle.None;
            actionBar.Add(btnCancel);
            
            _chatTab.Add(actionBar);
            
            // Context Panel
            var contextPanel = new VisualElement();
            contextPanel.name = "context-container";
            contextPanel.AddToClassList("panel");
            contextPanel.AddToClassList("context-panel");
            
            var contextHeader = new VisualElement();
            contextHeader.AddToClassList("panel-header");
            
            var contextTitle = new Label("Context");
            contextTitle.AddToClassList("panel-header-label");
            contextHeader.Add(contextTitle);
            
            contextPanel.Add(contextHeader);
            
            var contextScroll = new ScrollView();
            contextScroll.name = "context-scroll";
            contextScroll.AddToClassList("scroll-area");
            
            var contextText = new Label("No context selected.");
            contextText.name = "context-text";
            contextText.AddToClassList("context-text");
            contextScroll.Add(contextText);
            
            contextPanel.Add(contextScroll);
            _chatTab.Add(contextPanel);
            
            // Initialize views with the chat tab
            var contextView = new ContextView(_chatTab, _contextCollector);
            var responseView = new ResponseView(_chatTab);
            _actionBarView = new ActionBarView(_chatTab, _modelManager, _inferenceService, contextView, responseView);
            
            // Wire up RAG integration for Chat
            _actionBarView.SetSemanticIndex(_semanticIndex);
        }

        private void CreateSearchTab()
        {
            _searchTab = new VisualElement();
            _searchTab.style.flexGrow = 1;
            _searchTab.style.flexDirection = FlexDirection.Column;
            
            // Header
            var header = new Label("Project Search");
            header.style.fontSize = 16;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            _searchTab.Add(header);
            
            var description = new Label("Search your codebase using natural language queries.");
            description.style.fontSize = 11;
            description.style.color = new Color(0.6f, 0.6f, 0.6f);
            description.style.marginBottom = 16;
            _searchTab.Add(description);
            
            // Initialize ProjectSearchView (it will add its own elements)
            var searchView = new ProjectSearchView(
                _searchTab,
                _semanticIndex,
                () => _actionBarView?.GetActiveService()
            );
            
            // Show it by default in this tab
            searchView.Show();
        }

        private void CreateAnalyzeTab()
        {
            _analyzeTab = new VisualElement();
            _analyzeTab.style.flexGrow = 1;
            _analyzeTab.style.flexDirection = FlexDirection.Column;
            
            // Header
            var header = new Label("Scene Analyzer");
            header.style.fontSize = 16;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            _analyzeTab.Add(header);
            
            var description = new Label("Analyze your current scene for performance issues and best practices.");
            description.style.fontSize = 11;
            description.style.color = new Color(0.6f, 0.6f, 0.6f);
            description.style.marginBottom = 16;
            _analyzeTab.Add(description);
            
            var analyzeBtn = new Button(() => AnalyzeScene());
            analyzeBtn.text = "Analyze Current Scene";
            analyzeBtn.AddToClassList("action-btn-primary");
            analyzeBtn.AddToClassList("analyze-btn-large");
            analyzeBtn.style.height = 40;
            analyzeBtn.style.fontSize = 13;
            _analyzeTab.Add(analyzeBtn);
            
            // Results container
            var resultsScroll = new ScrollView();
            resultsScroll.name = "analyze-results";
            resultsScroll.style.flexGrow = 1;
            
            var resultsLabel = new Label();
            resultsLabel.name = "analyze-results-text";
            resultsLabel.style.whiteSpace = WhiteSpace.Normal;
            resultsLabel.style.fontSize = 11;
            resultsScroll.Add(resultsLabel);
            
            _analyzeTab.Add(resultsScroll);
        }
        
        private void AnalyzeScene()
        {
            var resultsText = _analyzeTab.Q<Label>("analyze-results-text");
            if (resultsText != null)
            {
                string report = SceneAnalyzer.AnalyzeCurrentScene();
                resultsText.text = report;
            }
        }

        private void CreateActionsTab()
        {
            _actionsTab = new VisualElement();
            _actionsTab.style.flexGrow = 1;
            _actionsTab.style.flexDirection = FlexDirection.Column;
            _actionsTab.style.paddingTop = 4;
            _actionsTab.style.paddingLeft = 4;
            _actionsTab.style.paddingRight = 4;
            
            // Initialize ActionsView (it builds its own UI)
            var actionsView = new ActionsView(_actionsTab);
        }

        private void CreateRefactorTab()
        {
            _refactorTab = new VisualElement();
            _refactorTab.style.flexGrow = 1;
            _refactorTab.style.flexDirection = FlexDirection.Column;
            _refactorTab.style.paddingTop = 4;
            _refactorTab.style.paddingLeft = 4;
            _refactorTab.style.paddingRight = 4;
            
            // Initialize RefactorView (it builds its own UI)
            var refactorView = new RefactorView(_refactorTab);
        }

        private void CreateSettingsTab()
        {
            _settingsTab = new VisualElement();
            _settingsTab.style.flexGrow = 1;
            _settingsTab.style.flexDirection = FlexDirection.Column;
            
            // Header
            var header = new Label("Settings");
            header.style.fontSize = 16;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 16;
            _settingsTab.Add(header);
            
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            
            // Provider Section
            var providerSection = CreateSettingsSection("AI Provider");
            
            var providerLabel = new Label("Select AI Provider");
            providerLabel.AddToClassList("field-label");
            providerSection.Add(providerLabel);
            
            var providerList = new System.Collections.Generic.List<string>(LocalAISettings.ProviderLabels);
            var providerDropdown = new PopupField<string>(providerList, (int)LocalAISettings.ActiveProvider);
            providerDropdown.style.marginBottom = 12;
            providerSection.Add(providerDropdown);
            
            // API Key container (will be hidden for Local provider)
            var apiKeyContainer = new VisualElement();
            apiKeyContainer.name = "api-key-container";
            
            var apiKeyLabel = new Label("API Key");
            apiKeyLabel.AddToClassList("field-label");
            apiKeyContainer.Add(apiKeyLabel);
            
            var apiKeyField = new TextField();
            apiKeyField.isPasswordField = true;
            apiKeyField.style.marginBottom = 8;
            apiKeyField.value = LocalAISettings.GetApiKey(LocalAISettings.ActiveProvider);
            apiKeyField.RegisterValueChangedCallback(evt =>
            {
                switch (LocalAISettings.ActiveProvider)
                {
                    case AIProvider.Gemini: LocalAISettings.GeminiApiKey = evt.newValue; break;
                    case AIProvider.OpenAI: LocalAISettings.OpenAIApiKey = evt.newValue; break;
                    case AIProvider.Claude: LocalAISettings.ClaudeApiKey = evt.newValue; break;
                }
            });
            apiKeyContainer.Add(apiKeyField);
            providerSection.Add(apiKeyContainer);
            
            // Helper to update API Key visibility
            void UpdateApiKeyVisibility(AIProvider provider)
            {
                bool showApiKey = provider != AIProvider.Local;
                apiKeyContainer.style.display = showApiKey ? DisplayStyle.Flex : DisplayStyle.None;
                if (showApiKey)
                {
                    apiKeyField.value = LocalAISettings.GetApiKey(provider);
                }
            }
            
            // Set initial visibility
            UpdateApiKeyVisibility(LocalAISettings.ActiveProvider);
            
            // Update on provider change
            providerDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = providerList.IndexOf(evt.newValue);
                if (index >= 0)
                {
                    LocalAISettings.ActiveProvider = (AIProvider)index;
                    UpdateApiKeyVisibility((AIProvider)index);
                }
            });
            
            scroll.Add(providerSection);
            
            // AI Settings Section
            var aiSection = CreateSettingsSection("AI Settings");
            
            var contextLabel = new Label("Context Size");
            contextLabel.AddToClassList("field-label");
            aiSection.Add(contextLabel);
            
            var contextList = new System.Collections.Generic.List<string>(LocalAISettings.ContextSizeLabels);
            var contextDropdown = new PopupField<string>(contextList, LocalAISettings.GetContextSizeIndex());
            contextDropdown.style.marginBottom = 12;
            contextDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = contextList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.ContextSize = LocalAISettings.ContextSizeOptions[index];
            });
            aiSection.Add(contextDropdown);
            
            var tokensLabel = new Label("Max Response Length");
            tokensLabel.AddToClassList("field-label");
            aiSection.Add(tokensLabel);
            
            var tokensList = new System.Collections.Generic.List<string>(LocalAISettings.MaxTokensLabels);
            var tokensDropdown = new PopupField<string>(tokensList, LocalAISettings.GetMaxTokensIndex());
            tokensDropdown.style.marginBottom = 8;
            tokensDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = tokensList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.MaxTokens = LocalAISettings.MaxTokensOptions[index];
            });
            aiSection.Add(tokensDropdown);
            
            scroll.Add(aiSection);
            
            // Model Section (Local only)
            var modelSection = CreateSettingsSection("Local Model");
            
            var pathLabel = new Label("Model Storage Path");
            pathLabel.AddToClassList("field-label");
            modelSection.Add(pathLabel);
            
            var pathField = new TextField();
            pathField.isReadOnly = true;
            pathField.value = _modelManager.GetModelDirectory();
            pathField.style.marginBottom = 12;
            modelSection.Add(pathField);
            
            var downloadBtn = new Button(() => _modelManager.StartDownload());
            downloadBtn.text = "Download Model";
            downloadBtn.style.marginBottom = 8;
            modelSection.Add(downloadBtn);
            
            var deleteBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("Delete Model?", "Delete the downloaded model?", "Yes", "No"))
                {
                    Debug.Log("[LocalAI] Model deleted");
                }
            });
            deleteBtn.text = "Delete Model";
            deleteBtn.AddToClassList("danger-btn");
            modelSection.Add(deleteBtn);
            
            scroll.Add(modelSection);
            
            // RAG Settings Section
            var ragSection = CreateSettingsSection("RAG (Code Context)");
            
            var ragDescription = new Label("Retrieval-Augmented Generation adds relevant code from your project to AI queries.");
            ragDescription.style.fontSize = 10;
            ragDescription.style.color = new Color(0.6f, 0.6f, 0.6f);
            ragDescription.style.whiteSpace = WhiteSpace.Normal;
            ragDescription.style.marginBottom = 8;
            ragSection.Add(ragDescription);
            
            var ragToggle = new Toggle("Enable RAG for Chat");
            ragToggle.value = LocalAISettings.EnableRAG;
            ragToggle.style.marginBottom = 8;
            ragToggle.RegisterValueChangedCallback(evt => LocalAISettings.EnableRAG = evt.newValue);
            ragSection.Add(ragToggle);
            
            var ragTopKLabel = new Label("Context Chunks (how many code snippets to retrieve)");
            ragTopKLabel.AddToClassList("field-label");
            ragSection.Add(ragTopKLabel);
            
            var ragTopKList = new System.Collections.Generic.List<string>(LocalAISettings.RAGTopKLabels);
            var ragTopKDropdown = new PopupField<string>(ragTopKList, LocalAISettings.GetRAGTopKIndex());
            ragTopKDropdown.style.marginBottom = 8;
            ragTopKDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = ragTopKList.IndexOf(evt.newValue);
                if (index >= 0) LocalAISettings.RAGTopK = LocalAISettings.RAGTopKOptions[index];
            });
            ragSection.Add(ragTopKDropdown);
            
            var indexStatusLabel = new Label($"Index Status: {(_semanticIndex?.State.ToString() ?? "Not initialized")}");
            indexStatusLabel.style.fontSize = 10;
            indexStatusLabel.style.color = new Color(0.5f, 0.7f, 0.5f);
            ragSection.Add(indexStatusLabel);
            
            scroll.Add(ragSection);
            
            _settingsTab.Add(scroll);
        }
        
        private VisualElement CreateSettingsSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("settings-section");
            
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("settings-section-title");
            section.Add(titleLabel);
            
            return section;
        }

        private void OnDestroy()
        {
            _modelManager?.CancelDownload();
            _semanticIndex?.Dispose();
        }
    }
}
