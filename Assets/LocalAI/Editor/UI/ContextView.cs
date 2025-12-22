using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ContextView
    {
        private readonly ContextCollector _collector;
        private readonly Label _selectionSummary;
        private readonly TextField _manualInput;
        private readonly Toggle _includeSelectionToggle;
        private readonly VisualElement _contextContainer;
        
        // Removed old "context-text" label support as we are using a robust manual+auto flow
        
        private readonly ProgressBar _usageBar;
        private readonly Label _usageLabel;
        private ContextData _cachedData;

        public ContextView(VisualElement root, ContextCollector collector)
        {
            _collector = collector;
            _contextContainer = root.Q<VisualElement>("context-container");
            
            if (_contextContainer == null) return;
            
            // Clear existing
            _contextContainer.Clear();
            
            // 1. Selection Summary
            _selectionSummary = new Label("Selected: None");
            _selectionSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _contextContainer.Add(_selectionSummary);
            
            // 2. Usage Stats (NEW)
            var statsContainer = new VisualElement();
            statsContainer.style.flexDirection = FlexDirection.Row;
            statsContainer.style.marginTop = 2;
            statsContainer.style.marginBottom = 8;
            statsContainer.style.height = 14;
            
            _usageBar = new ProgressBar();
            _usageBar.style.flexGrow = 1;
            _usageBar.style.marginRight = 5;
            _usageBar.lowValue = 0;
            _usageBar.highValue = 100;
            
            _usageLabel = new Label("0 / 4000");
            _usageLabel.style.fontSize = 10;
            _usageLabel.style.width = 80;
            _usageLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            
            statsContainer.Add(_usageBar);
            statsContainer.Add(_usageLabel);
            _contextContainer.Add(statsContainer);
            
            // 3. Toggle "Include Selection"
            _includeSelectionToggle = new Toggle("Include Selection Context");
            _includeSelectionToggle.value = true;
            _includeSelectionToggle.RegisterValueChangedCallback(evt => OnSelectionChanged());
            _contextContainer.Add(_includeSelectionToggle);
            
            // 4. Manual Input
            var inputLabel = new Label("User Input (Question / Instructions):");
            inputLabel.style.fontSize = 11;
            _contextContainer.Add(inputLabel);

            _manualInput = new TextField();
            _manualInput.multiline = true;
            _manualInput.style.flexGrow = 1;
            _manualInput.style.minHeight = 100;
            _manualInput.style.whiteSpace = WhiteSpace.PreWrap;
            _manualInput.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            
            _contextContainer.Add(_manualInput);
            
            // Hook Events
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged(); // Init
        }

        private void OnSelectionChanged()
        {
            // Update Summary
            int count = Selection.objects.Length;
            if (count == 0) _selectionSummary.text = "Selected: None";
            else if (count == 1) _selectionSummary.text = $"Selected: {Selection.activeObject.name}";
            else _selectionSummary.text = $"Selected: {count} items";

            // Update Context Data
            if (_includeSelectionToggle.value)
            {
                _cachedData = _collector.CollectContext();
                UpdateUsageStats();
            }
            else
            {
                _cachedData = new ContextData(); // Empty
                _usageBar.value = 0;
                _usageLabel.text = "Disabled";
            }
        }
        
        private void UpdateUsageStats()
        {
            float percent = (float)_cachedData.TotalChars / _cachedData.MaxChars * 100f;
            _usageBar.value = percent;
            _usageBar.title = $"{_cachedData.TotalChars} chars"; // Warning: Unity ProgressBar doesn't always show title
            _usageLabel.text = $"{_cachedData.TotalChars} / {_cachedData.MaxChars}";
            
            if (_cachedData.IsTruncated)
            {
                _usageLabel.style.color = Color.yellow;
                _usageLabel.text += " (!)";
            }
            else
            {
                _usageLabel.style.color = new Color(0.7f, 0.7f, 0.7f); // Default grey
            }
        }

        public string GetContext()
        {
            string context = _cachedData.FullText;
            string userPrompt = _manualInput.value;
            
            if (string.IsNullOrWhiteSpace(userPrompt)) return context;
            if (string.IsNullOrWhiteSpace(context)) return userPrompt;
            
            return $"USER QUERY:\n{userPrompt}\n\nREFERENCE CONTEXT:\n{context}";
        }
    }
}
