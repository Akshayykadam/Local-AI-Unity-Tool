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
        private readonly Toggle _includeLogsToggle; // [NEW] Explicit declaration
        private readonly VisualElement _contextContainer;
        
        private readonly ProgressBar _usageBar;
        private readonly Label _usageLabel;
        private ContextData _cachedData;
        
        public event System.Action<ContextData> OnContextUpdated;

        public ContextView(VisualElement root, ContextCollector collector)
        {
            _collector = collector;
            _contextContainer = root.Q<VisualElement>("context-container");
            
            if (_contextContainer == null) return;
            
            _contextContainer.Clear();
            _contextContainer.style.paddingLeft = 4;
            _contextContainer.style.paddingRight = 4;
            _contextContainer.style.paddingTop = 4;
            
            // 1. Header Row (Selected + Stats)
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 2;
            
            _selectionSummary = new Label("Selected: None");
            _selectionSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selectionSummary.style.flexShrink = 1;
            
            _usageLabel = new Label("0 / 4000");
            _usageLabel.style.fontSize = 10;
            _usageLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            
            header.Add(_selectionSummary);
            header.Add(_usageLabel);
            _contextContainer.Add(header);
            
            // 2. Usage Bar (Full Width)
            _usageBar = new ProgressBar();
            _usageBar.style.height = 6;
            _usageBar.style.marginBottom = 6;
            _usageBar.style.marginTop = 0;
            // Hack to hide internal label if sticking out
            
            _contextContainer.Add(_usageBar);
            
            // 3. Toggles Row
            var togglesRow = new VisualElement();
            togglesRow.style.flexDirection = FlexDirection.Row;
            togglesRow.style.marginBottom = 6;
            
            _includeSelectionToggle = new Toggle("Include Selection");
            _includeSelectionToggle.value = true;
            _includeSelectionToggle.style.marginRight = 15;
            _includeSelectionToggle.RegisterValueChangedCallback(evt => OnSelectionChanged());
            
            _includeLogsToggle = new Toggle("Include Logs");
            _includeLogsToggle.value = false;
            _includeLogsToggle.RegisterValueChangedCallback(evt => OnSelectionChanged());
            
            togglesRow.Add(_includeSelectionToggle);
            togglesRow.Add(_includeLogsToggle);
            _contextContainer.Add(togglesRow);
            
            // 4. Input
            var inputLabel = new Label("User Input:");
            inputLabel.style.fontSize = 11;
            inputLabel.style.marginBottom = 2;
            _contextContainer.Add(inputLabel);

            _manualInput = new TextField();
            _manualInput.multiline = true;
            _manualInput.style.flexGrow = 1;
            _manualInput.style.minHeight = 80;
            _manualInput.style.whiteSpace = WhiteSpace.PreWrap;
            _manualInput.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _manualInput.style.borderTopLeftRadius = 3;
            _manualInput.style.borderBottomRightRadius = 3;
            
            _contextContainer.Add(_manualInput);
            
            // Hook
            Selection.selectionChanged += OnSelectionChanged;
            
            // Deferred init
            _contextContainer.schedule.Execute(OnSelectionChanged);
        }

        private void OnSelectionChanged()
        {
            // Update Summary
            int count = Selection.objects.Length;
            if (count == 0) _selectionSummary.text = "Selected: None";
            else if (count == 1) _selectionSummary.text = $"Selected: {Selection.activeObject.name}";
            else _selectionSummary.text = $"Selected: {count} items";

            // Update Context Data
            // We include logs if specific toggle is on OR if user wants full context?
            // Usually logs are part of context.
            
            if (_includeSelectionToggle.value || _includeLogsToggle.value)
            {
                // Pass the log toggle value
                bool includeLogs = _includeLogsToggle.value;
                _cachedData = _collector.CollectContext(includeLogs);
                UpdateUsageStats();
                OnContextUpdated?.Invoke(_cachedData);
            }
            else
            {
                _cachedData = new ContextData(); // Empty
                _usageBar.value = 0;
                _usageLabel.text = "Disabled";
                OnContextUpdated?.Invoke(_cachedData);
            }
        }
        
        private void UpdateUsageStats()
        {
            float percent = (float)_cachedData.TotalChars / _cachedData.MaxChars * 100f;
            _usageBar.value = percent;
            
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
