using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ContextView
    {
        private readonly ContextCollector _collector;
        private readonly Label _contextText;
        private readonly TextField _manualInput;
        private readonly Toggle _useManualToggle;
        private readonly VisualElement _contextContainer;

        public bool UseManualInput => _useManualToggle?.value ?? false;
        public string ManualInputText => _manualInput?.value ?? "";

        public ContextView(VisualElement root, ContextCollector collector)
        {
            _collector = collector;
            _contextContainer = root.Q<VisualElement>("context-container");
            _contextText = root.Q<Label>("context-text");
            
            if (_contextContainer == null) return;
            
            // Create manual input section
            var manualSection = new VisualElement();
            manualSection.style.marginTop = 8;
            
            // Toggle for manual input mode
            _useManualToggle = new Toggle("Use Manual Input");
            _useManualToggle.value = false;
            _useManualToggle.style.marginBottom = 6;
            manualSection.Add(_useManualToggle);
            
            // Manual input text area - wrapped in ScrollView for scrolling
            var inputScroll = new ScrollView();
            inputScroll.style.minHeight = 100;
            inputScroll.style.maxHeight = 150;
            inputScroll.style.display = DisplayStyle.None;
            inputScroll.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            inputScroll.style.borderTopLeftRadius = 3;
            inputScroll.style.borderTopRightRadius = 3;
            inputScroll.style.borderBottomLeftRadius = 3;
            inputScroll.style.borderBottomRightRadius = 3;
            
            _manualInput = new TextField();
            _manualInput.multiline = true;
            _manualInput.style.minHeight = 100;
            _manualInput.style.whiteSpace = WhiteSpace.PreWrap;
            _manualInput.style.fontSize = 11;
            _manualInput.value = "// Paste code or error here...";
            inputScroll.Add(_manualInput);
            
            // Toggle callback to show/hide input scroll
            _useManualToggle.RegisterValueChangedCallback(evt => {
                inputScroll.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (_contextText != null)
                    _contextText.parent.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
            });
            
            manualSection.Add(inputScroll);
            _contextContainer.Add(manualSection);
        }

        public void RefreshContext()
        {
            if (_contextText != null && !UseManualInput)
            {
                _contextText.text = _collector.CollectContext();
            }
        }
        
        public string GetContext()
        {
            if (UseManualInput && !string.IsNullOrWhiteSpace(ManualInputText))
            {
                return ManualInputText;
            }
            return _collector.CollectContext();
        }
    }
}
