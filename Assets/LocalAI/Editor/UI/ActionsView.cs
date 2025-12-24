using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    /// <summary>
    /// UI View for the Actions tab - Execute commands directly in Unity.
    /// </summary>
    public class ActionsView
    {
        private readonly VisualElement _root;
        private TextField _commandInput;
        private VisualElement _quickActionsPanel;
        private VisualElement _historyPanel;
        private Label _statusLabel;
        
        // AI Actions
        private TextField _aiInput;
        private VisualElement _aiPreviewPanel;
        private Label _aiStatusLabel;
        private Button _executeAllBtn;
        private Button _cancelBtn;
        private AIActionService _aiActionService;
        private List<AIAction> _pendingActions;
        private bool _isProcessing;
        
        private List<string> _commandHistory = new List<string>();

        public ActionsView(VisualElement root)
        {
            _root = root;
            _aiActionService = new AIActionService();
            _pendingActions = new List<AIAction>();
            BuildUI();
        }

        private void BuildUI()
        {
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            
            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;
            
            var header = new Label("Actions");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(header);
            
            var experimentalBadge = new Label("EXPERIMENTAL");
            experimentalBadge.style.fontSize = 8;
            experimentalBadge.style.marginLeft = 8;
            experimentalBadge.style.paddingLeft = 4;
            experimentalBadge.style.paddingRight = 4;
            experimentalBadge.style.paddingTop = 2;
            experimentalBadge.style.paddingBottom = 2;
            experimentalBadge.style.backgroundColor = new Color(0.8f, 0.5f, 0.1f);
            experimentalBadge.style.color = Color.white;
            experimentalBadge.style.borderTopLeftRadius = 3;
            experimentalBadge.style.borderTopRightRadius = 3;
            experimentalBadge.style.borderBottomLeftRadius = 3;
            experimentalBadge.style.borderBottomRightRadius = 3;
            headerRow.Add(experimentalBadge);
            
            _root.Add(headerRow);
            
            var description = new Label("Execute commands directly in your scene. Some features may not work as expected.");
            description.style.fontSize = 10;
            description.style.color = new Color(0.6f, 0.6f, 0.6f);
            description.style.marginBottom = 12;
            _root.Add(description);
            
            // Command Input Section
            var inputSection = CreateSection("Command Input");
            
            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.marginBottom = 8;
            
            _commandInput = new TextField();
            _commandInput.style.flexGrow = 1;
            _commandInput.style.height = 24;
            _commandInput.value = "Create a Cube at (0, 2, 0)";
            _commandInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    ExecuteCommand();
                }
            });
            inputRow.Add(_commandInput);
            
            var executeBtn = new Button(ExecuteCommand) { text = "Execute" };
            executeBtn.style.width = 70;
            executeBtn.style.height = 24;
            executeBtn.style.marginLeft = 4;
            executeBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            inputRow.Add(executeBtn);
            
            inputSection.Add(inputRow);
            
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = new Color(0.6f, 0.8f, 0.6f);
            inputSection.Add(_statusLabel);
            
            _root.Add(inputSection);
            
            // AI Actions Section
            var aiSection = CreateSection("🤖 AI Actions");
            aiSection.style.borderLeftWidth = 2;
            aiSection.style.borderLeftColor = new Color(0.4f, 0.6f, 0.9f);
            
            var aiDescription = new Label("Describe complex setups - AI generates all actions.");
            aiDescription.style.fontSize = 9;
            aiDescription.style.color = new Color(0.6f, 0.7f, 0.9f);
            aiDescription.style.marginBottom = 6;
            aiSection.Add(aiDescription);
            
            var aiInputRow = new VisualElement();
            aiInputRow.style.flexDirection = FlexDirection.Row;
            aiInputRow.style.marginBottom = 6;
            
            _aiInput = new TextField();
            _aiInput.style.flexGrow = 1;
            _aiInput.style.height = 24;
            _aiInput.multiline = false;
            _aiInput.value = "Create a player with movement controller and camera";
            _aiInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AskAI();
                }
            });
            aiInputRow.Add(_aiInput);
            
            var askAIBtn = new Button(AskAI) { text = "Ask AI" };
            askAIBtn.name = "ask-ai-btn";
            askAIBtn.style.width = 70;
            askAIBtn.style.height = 24;
            askAIBtn.style.marginLeft = 4;
            askAIBtn.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
            aiInputRow.Add(askAIBtn);
            
            aiSection.Add(aiInputRow);
            
            // AI Status
            _aiStatusLabel = new Label("");
            _aiStatusLabel.style.fontSize = 10;
            _aiStatusLabel.style.marginBottom = 6;
            aiSection.Add(_aiStatusLabel);
            
            // AI Preview Panel
            _aiPreviewPanel = new VisualElement();
            _aiPreviewPanel.name = "ai-preview-panel";
            _aiPreviewPanel.style.display = DisplayStyle.None;
            _aiPreviewPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            _aiPreviewPanel.style.borderTopLeftRadius = 4;
            _aiPreviewPanel.style.borderTopRightRadius = 4;
            _aiPreviewPanel.style.borderBottomLeftRadius = 4;
            _aiPreviewPanel.style.borderBottomRightRadius = 4;
            _aiPreviewPanel.style.paddingTop = 6;
            _aiPreviewPanel.style.paddingBottom = 6;
            _aiPreviewPanel.style.paddingLeft = 8;
            _aiPreviewPanel.style.paddingRight = 8;
            _aiPreviewPanel.style.marginBottom = 6;
            
            var previewHeader = new Label("📋 Action Plan");
            previewHeader.style.fontSize = 10;
            previewHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewHeader.style.marginBottom = 4;
            _aiPreviewPanel.Add(previewHeader);
            
            var previewScroll = new ScrollView();
            previewScroll.name = "preview-scroll";
            previewScroll.style.maxHeight = 120;
            _aiPreviewPanel.Add(previewScroll);
            
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 6;
            
            _executeAllBtn = new Button(ExecuteAllAIActions) { text = "Execute All" };
            _executeAllBtn.style.flexGrow = 1;
            _executeAllBtn.style.height = 24;
            _executeAllBtn.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
            _executeAllBtn.style.marginRight = 4;
            buttonRow.Add(_executeAllBtn);
            
            _cancelBtn = new Button(CancelAIActions) { text = "Cancel" };
            _cancelBtn.style.width = 70;
            _cancelBtn.style.height = 24;
            _cancelBtn.style.backgroundColor = new Color(0.5f, 0.3f, 0.3f);
            buttonRow.Add(_cancelBtn);
            
            _aiPreviewPanel.Add(buttonRow);
            aiSection.Add(_aiPreviewPanel);
            
            _root.Add(aiSection);
            
            // Quick Actions Section
            _quickActionsPanel = CreateSection("Quick Actions");
            
            var row1 = CreateButtonRow(new (string, Action)[]
            {
                ("Cube", () => QuickCreate(PrimitiveType.Cube)),
                ("Sphere", () => QuickCreate(PrimitiveType.Sphere)),
                ("Capsule", () => QuickCreate(PrimitiveType.Capsule)),
                ("Cylinder", () => QuickCreate(PrimitiveType.Cylinder)),
                ("Plane", () => QuickCreate(PrimitiveType.Plane)),
            });
            _quickActionsPanel.Add(row1);
            
            var row2 = CreateButtonRow(new (string, Action)[]
            {
                ("+ Rigidbody", () => AddComponentToSelected("Rigidbody")),
                ("+ BoxCollider", () => AddComponentToSelected("BoxCollider")),
                ("+ Light", () => AddComponentToSelected("Light")),
                ("+ AudioSource", () => AddComponentToSelected("AudioSource")),
            });
            _quickActionsPanel.Add(row2);
            
            var row3 = CreateButtonRow(new (string, Action)[]
            {
                ("Red Material", () => ApplyColorToSelected(Color.red, "Red")),
                ("Green Material", () => ApplyColorToSelected(Color.green, "Green")),
                ("Blue Material", () => ApplyColorToSelected(Color.blue, "Blue")),
                ("Yellow Material", () => ApplyColorToSelected(Color.yellow, "Yellow")),
            });
            _quickActionsPanel.Add(row3);
            
            _root.Add(_quickActionsPanel);
            
            // Templates Section
            var templatesSection = CreateSection("Templates");
            
            var templatesRow = new VisualElement();
            templatesRow.style.flexDirection = FlexDirection.Row;
            templatesRow.style.flexWrap = Wrap.Wrap;
            
            foreach (var template in CommandTemplates.Templates)
            {
                var btn = new Button(() => ExecuteTemplate(template));
                btn.text = template.Name;
                btn.tooltip = template.Description;
                btn.style.height = 22;
                btn.style.fontSize = 9;
                btn.style.marginRight = 4;
                btn.style.marginBottom = 2;
                btn.style.paddingLeft = 6;
                btn.style.paddingRight = 6;
                templatesRow.Add(btn);
            }
            templatesSection.Add(templatesRow);
            _root.Add(templatesSection);
            
            // Smart Suggestions Section
            var suggestionsSection = CreateSection("Smart Suggestions");
            suggestionsSection.name = "suggestions-section";
            
            var suggestionsContainer = new VisualElement();
            suggestionsContainer.name = "suggestions-container";
            suggestionsContainer.style.flexDirection = FlexDirection.Row;
            suggestionsContainer.style.flexWrap = Wrap.Wrap;
            suggestionsSection.Add(suggestionsContainer);
            
            var refreshSuggestionsBtn = new Button(UpdateSuggestions) { text = "Refresh Suggestions" };
            refreshSuggestionsBtn.style.height = 20;
            refreshSuggestionsBtn.style.fontSize = 10;
            refreshSuggestionsBtn.style.marginTop = 6;
            suggestionsSection.Add(refreshSuggestionsBtn);
            
            _root.Add(suggestionsSection);
            
            // Selected Object Info
            var selectionSection = CreateSection("Selected Object");
            var selectionInfo = new Label();
            selectionInfo.name = "selection-info";
            selectionInfo.style.fontSize = 10;
            selectionInfo.style.whiteSpace = WhiteSpace.Normal;
            selectionSection.Add(selectionInfo);
            
            var refreshBtn = new Button(() => { UpdateSelectionInfo(selectionInfo); UpdateSuggestions(); }) { text = "Refresh" };
            refreshBtn.style.height = 20;
            refreshBtn.style.fontSize = 10;
            refreshBtn.style.marginTop = 4;
            selectionSection.Add(refreshBtn);
            
            _root.Add(selectionSection);
            
            // History Section
            _historyPanel = CreateSection("Command History");
            var historyScroll = new ScrollView();
            historyScroll.name = "history-scroll";
            historyScroll.style.maxHeight = 80;
            _historyPanel.Add(historyScroll);
            
            var clearHistoryBtn = new Button(ClearHistory) { text = "Clear History" };
            clearHistoryBtn.style.height = 20;
            clearHistoryBtn.style.fontSize = 10;
            clearHistoryBtn.style.marginTop = 4;
            _historyPanel.Add(clearHistoryBtn);
            
            _root.Add(_historyPanel);
            
            // Initial updates
            UpdateSelectionInfo(selectionInfo);
            UpdateSuggestions();
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 12;
            section.style.paddingTop = 6;
            section.style.paddingBottom = 6;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;
            
            var header = new Label(title);
            header.style.fontSize = 11;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 6;
            section.Add(header);
            
            return section;
        }

        private VisualElement CreateButtonRow((string label, Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 4;
            
            foreach (var (label, action) in buttons)
            {
                var btn = new Button(action) { text = label };
                btn.style.height = 22;
                btn.style.fontSize = 10;
                btn.style.marginRight = 4;
                btn.style.marginBottom = 2;
                btn.style.paddingLeft = 8;
                btn.style.paddingRight = 8;
                row.Add(btn);
            }
            
            return row;
        }

        private void ExecuteCommand()
        {
            string command = _commandInput.value;
            if (string.IsNullOrWhiteSpace(command)) return;
            
            var commands = CommandParser.ParseCommands(command);
            
            if (commands.Count == 0)
            {
                _statusLabel.text = "No valid commands detected";
                _statusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                return;
            }
            
            int executed = CommandParser.ExecuteCommands(commands);
            
            _statusLabel.text = $"Executed {executed}/{commands.Count} action(s)";
            _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
            
            // Add to history
            AddToHistory(command);
            
            // Clear input
            _commandInput.value = "";
        }

        private void AddToHistory(string command)
        {
            _commandHistory.Insert(0, command);
            if (_commandHistory.Count > 10)
                _commandHistory.RemoveAt(_commandHistory.Count - 1);
            
            UpdateHistoryUI();
        }

        private void UpdateHistoryUI()
        {
            var scroll = _historyPanel.Q<ScrollView>("history-scroll");
            if (scroll == null) return;
            
            scroll.Clear();
            
            foreach (var cmd in _commandHistory)
            {
                var btn = new Button(() =>
                {
                    _commandInput.value = cmd;
                });
                btn.text = cmd.Length > 40 ? cmd.Substring(0, 40) + "..." : cmd;
                btn.style.height = 18;
                btn.style.fontSize = 9;
                btn.style.marginBottom = 1;
                scroll.Add(btn);
            }
        }

        private void ClearHistory()
        {
            _commandHistory.Clear();
            UpdateHistoryUI();
        }

        private void QuickCreate(PrimitiveType type)
        {
            Vector3 position = GetSpawnPosition();
            ActionExecutor.CreateGameObject(type.ToString(), type, position);
            _statusLabel.text = $"Created {type} at {position}";
            _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
        }

        private Vector3 GetSpawnPosition()
        {
            // Spawn in front of scene camera
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                return sceneView.camera.transform.position + sceneView.camera.transform.forward * 5f;
            }
            return Vector3.zero;
        }

        private void AddComponentToSelected(string componentName)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                _statusLabel.text = "No object selected";
                _statusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                return;
            }
            
            ActionExecutor.AddComponent(selected, componentName);
            _statusLabel.text = $"Added {componentName} to {selected.name}";
            _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
        }

        private void ApplyColorToSelected(Color color, string colorName)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                _statusLabel.text = "No object selected";
                _statusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                return;
            }
            
            var renderer = selected.GetComponent<Renderer>();
            if (renderer == null)
            {
                _statusLabel.text = "Selected object has no Renderer";
                _statusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                return;
            }
            
            var material = ActionExecutor.CreateMaterial($"{colorName}Material", color);
            ActionExecutor.AssignMaterial(renderer, material);
            
            _statusLabel.text = $"Applied {colorName} material to {selected.name}";
            _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
        }

        private void UpdateSelectionInfo(Label label)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                label.text = "No object selected";
                return;
            }
            
            var components = selected.GetComponents<Component>();
            var componentNames = new List<string>();
            foreach (var c in components)
            {
                if (c != null && !(c is Transform))
                    componentNames.Add(c.GetType().Name);
            }
            
            label.text = $"Name: {selected.name}\n" +
                        $"Position: {selected.transform.position}\n" +
                        $"Components: {string.Join(", ", componentNames)}";
        }

        private void ExecuteTemplate(CommandTemplate template)
        {
            int executed = CommandTemplates.ExecuteTemplate(template);
            _statusLabel.text = $"Executed template '{template.Name}' ({executed} actions)";
            _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
            
            AddToHistory($"[Template] {template.Name}");
        }

        private void UpdateSuggestions()
        {
            var container = _root.Q<VisualElement>("suggestions-container");
            if (container == null) return;
            
            container.Clear();
            
            var selected = Selection.activeGameObject;
            var suggestions = SmartSuggestions.GetSuggestions(selected);
            
            foreach (var suggestion in suggestions)
            {
                var btn = new Button(() => ExecuteSuggestion(suggestion));
                btn.text = suggestion.Label;
                btn.tooltip = suggestion.Command;
                btn.style.height = 20;
                btn.style.fontSize = 9;
                btn.style.marginRight = 4;
                btn.style.marginBottom = 2;
                btn.style.paddingLeft = 6;
                btn.style.paddingRight = 6;
                
                // Color by category
                switch (suggestion.Category)
                {
                    case SuggestionCategory.Physics:
                        btn.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f);
                        break;
                    case SuggestionCategory.Material:
                        btn.style.backgroundColor = new Color(0.5f, 0.3f, 0.4f);
                        break;
                    case SuggestionCategory.Create:
                        btn.style.backgroundColor = new Color(0.3f, 0.5f, 0.3f);
                        break;
                }
                
                container.Add(btn);
            }
            
            if (suggestions.Count == 0)
            {
                var label = new Label("Select an object for suggestions");
                label.style.fontSize = 9;
                label.style.color = new Color(0.5f, 0.5f, 0.5f);
                container.Add(label);
            }
        }

        private void ExecuteSuggestion(ActionSuggestion suggestion)
        {
            var commands = CommandParser.ParseCommands(suggestion.Command);
            
            if (commands.Count > 0)
            {
                int executed = CommandParser.ExecuteCommands(commands);
                _statusLabel.text = $"Executed: {suggestion.Label}";
                _statusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
            }
            else
            {
                // Handle special cases not covered by parser
                HandleSpecialSuggestion(suggestion);
            }
            
            // Refresh suggestions after action
            UpdateSuggestions();
        }

        private void HandleSpecialSuggestion(ActionSuggestion suggestion)
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return;
            
            if (suggestion.Label.Contains("Material"))
            {
                Color color = Color.white;
                string colorName = "White";
                
                if (suggestion.Label.Contains("Red")) { color = Color.red; colorName = "Red"; }
                else if (suggestion.Label.Contains("Blue")) { color = Color.blue; colorName = "Blue"; }
                else if (suggestion.Label.Contains("Green")) { color = Color.green; colorName = "Green"; }
                
                ApplyColorToSelected(color, colorName);
            }
            else
            {
                _statusLabel.text = suggestion.Command;
                _statusLabel.style.color = new Color(0.9f, 0.7f, 0.4f);
            }
        }

        #region AI Actions

        private void AskAI()
        {
            if (_isProcessing)
            {
                _aiStatusLabel.text = "Please wait...";
                _aiStatusLabel.style.color = new Color(0.9f, 0.7f, 0.4f);
                return;
            }

            string request = _aiInput.value;
            if (string.IsNullOrWhiteSpace(request))
            {
                _aiStatusLabel.text = "Please enter a request";
                _aiStatusLabel.style.color = new Color(0.9f, 0.7f, 0.4f);
                return;
            }

            _isProcessing = true;
            _aiStatusLabel.text = "🔄 Generating action plan...";
            _aiStatusLabel.style.color = new Color(0.6f, 0.7f, 0.9f);

            // Disable button while processing
            var askBtn = _root.Q<Button>("ask-ai-btn");
            if (askBtn != null) askBtn.SetEnabled(false);

            _aiActionService.GenerateActions(request,
                actions => OnAIActionsGenerated(actions),
                error => OnAIError(error)
            );
        }

        private void OnAIActionsGenerated(List<AIAction> actions)
        {
            _isProcessing = false;
            
            var askBtn = _root.Q<Button>("ask-ai-btn");
            if (askBtn != null) askBtn.SetEnabled(true);

            if (actions == null || actions.Count == 0)
            {
                _aiStatusLabel.text = "⚠️ No actions generated";
                _aiStatusLabel.style.color = new Color(0.9f, 0.7f, 0.4f);
                _aiPreviewPanel.style.display = DisplayStyle.None;
                return;
            }

            _pendingActions = actions;
            _aiStatusLabel.text = $"✓ {actions.Count} actions ready";
            _aiStatusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);

            UpdateAIPreview();
        }

        private void OnAIError(string error)
        {
            _isProcessing = false;
            
            var askBtn = _root.Q<Button>("ask-ai-btn");
            if (askBtn != null) askBtn.SetEnabled(true);

            _aiStatusLabel.text = $"❌ {error}";
            _aiStatusLabel.style.color = new Color(0.9f, 0.4f, 0.4f);
            _aiPreviewPanel.style.display = DisplayStyle.None;
        }

        private void UpdateAIPreview()
        {
            _aiPreviewPanel.style.display = DisplayStyle.Flex;

            var scroll = _aiPreviewPanel.Q<ScrollView>("preview-scroll");
            if (scroll == null) return;

            scroll.Clear();

            foreach (var action in _pendingActions)
            {
                var actionRow = new VisualElement();
                actionRow.style.flexDirection = FlexDirection.Row;
                actionRow.style.marginBottom = 2;

                var icon = new Label(GetActionIcon(action.ActionType));
                icon.style.width = 18;
                icon.style.fontSize = 10;
                actionRow.Add(icon);

                var desc = new Label(action.GetDescription());
                desc.style.fontSize = 9;
                desc.style.flexGrow = 1;
                desc.style.color = new Color(0.8f, 0.8f, 0.8f);
                actionRow.Add(desc);

                scroll.Add(actionRow);
            }
        }

        private string GetActionIcon(string actionType)
        {
            switch (actionType)
            {
                case "CreatePrimitive":
                case "CreateEmpty":
                    return "📦";
                case "AddComponent":
                    return "➕";
                case "SetParent":
                    return "🔗";
                case "SetTransform":
                    return "↔️";
                case "SetMaterial":
                    return "🎨";
                case "CreateScript":
                    return "📝";
                default:
                    return "▪";
            }
        }

        private void ExecuteAllAIActions()
        {
            if (_pendingActions == null || _pendingActions.Count == 0)
            {
                _aiStatusLabel.text = "No actions to execute";
                return;
            }

            int executed = AIActionService.ExecuteActions(_pendingActions);
            
            _aiStatusLabel.text = $"✓ Executed {executed}/{_pendingActions.Count} actions";
            _aiStatusLabel.style.color = new Color(0.6f, 0.9f, 0.6f);

            // Add to history
            AddToHistory($"[AI] {_aiInput.value}");

            // Clear
            _pendingActions.Clear();
            _aiPreviewPanel.style.display = DisplayStyle.None;
            _aiInput.value = "";
        }

        private void CancelAIActions()
        {
            _pendingActions.Clear();
            _aiPreviewPanel.style.display = DisplayStyle.None;
            _aiStatusLabel.text = "Cancelled";
            _aiStatusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        }

        #endregion
    }
}

