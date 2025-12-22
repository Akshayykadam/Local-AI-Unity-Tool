using UnityEngine;
using UnityEngine.UIElements;

namespace LocalAI.Editor.UI
{
    public class ResponseView
    {
        private readonly TextField _responseText;
        private readonly ScrollView _scroll;
        private readonly VisualElement _responseContainer;

        public ResponseView(VisualElement root)
        {
            _responseContainer = root.Q<VisualElement>("response-container");
            _responseText = root.Q<TextField>("response-text");
            _scroll = root.Q<ScrollView>("response-scroll");
            var actionsContainer = root.Q<VisualElement>("response-actions");
            
            if (_responseContainer == null || actionsContainer == null) return;
            
            // Copy Response button
            var copyBtn = new Button(() => 
            {
                if (_responseText != null) 
                {
                    GUIUtility.systemCopyBuffer = _responseText.value;
                    Debug.Log("[LocalAI] Response copied to clipboard!");
                }
            }) { text = "Copy" };
            copyBtn.AddToClassList("panel-header-btn");
            
            // Clear Response button
            var clearBtn = new Button(() => 
            {
                if (_responseText != null) _responseText.value = "Select code and click an action below.";
            }) { text = "Clear" };
            clearBtn.AddToClassList("panel-header-btn");
            
            actionsContainer.Add(copyBtn);
            actionsContainer.Add(clearBtn);
        }

        public void SetText(string text)
        {
            if (_responseText != null) _responseText.value = text;
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
    }
}
