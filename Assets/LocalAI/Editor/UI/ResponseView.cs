using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ResponseView
    {
        private readonly TextField _responseText;
        private readonly ScrollView _scroll;
        private readonly VisualElement _responseContainer;
        private readonly VisualElement _root;
        
        private VisualElement _actionsBar;
        private List<ActionCommand> _detectedCommands;

        public ResponseView(VisualElement root)
        {
            _root = root;
            _responseContainer = root.Q<VisualElement>("response-container");
            _responseText = root.Q<TextField>("response-text");
            _scroll = root.Q<ScrollView>("response-scroll");
            
            // Wire up Copy button if it exists
            var copyBtn = root.Q<Button>("btn-copy");
            if (copyBtn != null)
            {
                copyBtn.clicked += () =>
                {
                    if (_responseText != null) 
                    {
                        GUIUtility.systemCopyBuffer = _responseText.value;
                        Debug.Log("[LocalAI] Response copied to clipboard!");
                    }
                };
            }
            
            // Wire up Apply button if it exists
            var applyBtn = root.Q<Button>("btn-apply");
            if (applyBtn != null)
            {
                applyBtn.clicked += OnApplyClicked;
            }
            
            // Wire up Clear button if it exists
            var clearBtn = root.Q<Button>("btn-clear");
            if (clearBtn != null)
            {
                clearBtn.clicked += () =>
                {
                    if (_responseText != null) _responseText.value = "Select code and click an action below.";
                    HideActionsBar();
                };
            }
        }

        public void SetText(string text)
        {
            if (_responseText != null) _responseText.value = text;
            CheckForCommands(text);
        }

        public void AppendText(string text)
        {
            if (_responseText != null)
            {
                _responseText.value += text;
                // Auto-scroll to bottom
                if (_scroll != null)
                {
                    _scroll.scrollOffset = new Vector2(0, float.MaxValue);
                }
            }
        }
        
        /// <summary>
        /// Called after response is complete to check for actionable commands.
        /// </summary>
        public void OnResponseComplete()
        {
            if (_responseText != null)
            {
                CheckForCommands(_responseText.value);
            }
        }
        
        private void OnApplyClicked()
        {
            if (_responseText == null) return;
            
            string text = _responseText.value;
            var codeBlocks = ScriptApplicator.ExtractCodeBlocks(text);
            
            if (codeBlocks.Count == 0)
            {
                Debug.Log("[LocalAI] No code blocks found in response.");
                return;
            }
            
            // Apply the first code block
            ScriptApplicator.ShowApplyDialog(codeBlocks[0].Code);
        }
        
        private void CheckForCommands(string text)
        {
            _detectedCommands = CommandParser.ParseCommands(text);
            
            if (_detectedCommands.Count > 0)
            {
                ShowActionsBar();
            }
            else
            {
                HideActionsBar();
            }
        }
        
        private void ShowActionsBar()
        {
            if (_actionsBar != null)
            {
                _actionsBar.RemoveFromHierarchy();
            }
            
            _actionsBar = new VisualElement();
            _actionsBar.name = "detected-actions-bar";
            _actionsBar.style.marginTop = 8;
            _actionsBar.style.paddingTop = 6;
            _actionsBar.style.paddingBottom = 6;
            _actionsBar.style.paddingLeft = 6;
            _actionsBar.style.paddingRight = 6;
            _actionsBar.style.backgroundColor = new Color(0.2f, 0.25f, 0.2f);
            _actionsBar.style.borderTopLeftRadius = 4;
            _actionsBar.style.borderTopRightRadius = 4;
            _actionsBar.style.borderBottomLeftRadius = 4;
            _actionsBar.style.borderBottomRightRadius = 4;
            
            var header = new Label($"⚡ {_detectedCommands.Count} action(s) detected:");
            header.style.fontSize = 10;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            _actionsBar.Add(header);
            
            // List actions
            foreach (var cmd in _detectedCommands)
            {
                var cmdLabel = new Label($"  • {cmd.GetDescription()}");
                cmdLabel.style.fontSize = 9;
                cmdLabel.style.color = new Color(0.7f, 0.9f, 0.7f);
                _actionsBar.Add(cmdLabel);
            }
            
            // Execute button
            var executeBtn = new Button(OnExecuteCommands) { text = "Execute Actions" };
            executeBtn.style.marginTop = 8;
            executeBtn.style.height = 24;
            executeBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            _actionsBar.Add(executeBtn);
            
            // Add to content container or response container
            if (_responseContainer != null)
            {
                _responseContainer.Add(_actionsBar);
            }
            else
            {
                _root.Add(_actionsBar);
            }
        }
        
        private void HideActionsBar()
        {
            if (_actionsBar != null)
            {
                _actionsBar.RemoveFromHierarchy();
                _actionsBar = null;
            }
            _detectedCommands = null;
        }
        
        private void OnExecuteCommands()
        {
            if (_detectedCommands == null || _detectedCommands.Count == 0) return;
            
            int executed = CommandParser.ExecuteCommands(_detectedCommands);
            Debug.Log($"[LocalAI] Executed {executed}/{_detectedCommands.Count} actions");
            
            HideActionsBar();
        }
    }
}

