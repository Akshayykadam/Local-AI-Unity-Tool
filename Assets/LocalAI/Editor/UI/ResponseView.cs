using UnityEngine;
using UnityEngine.UIElements;

namespace LocalAI.Editor.UI
{
    public class ResponseView
    {
        private readonly Label _responseText;
        private readonly ScrollView _scroll;
        private readonly VisualElement _responseContainer;

        public ResponseView(VisualElement root)
        {
            _responseContainer = root.Q<VisualElement>("response-container");
            _responseText = root.Q<Label>("response-text");
            _scroll = root.Q<ScrollView>("response-scroll");
            
            if (_responseContainer == null) return;
            
            // Create a button container at bottom of response section
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 12;
            buttonContainer.style.paddingTop = 10;
            buttonContainer.style.borderTopWidth = 1;
            buttonContainer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            
            // Copy Response button
            var copyBtn = new Button(() => 
            {
                if (_responseText != null) 
                {
                    GUIUtility.systemCopyBuffer = _responseText.text;
                    Debug.Log("[LocalAI] Response copied to clipboard!");
                }
            }) { text = "Copy" };
            copyBtn.style.flexGrow = 1;
            copyBtn.style.marginRight = 6;
            copyBtn.style.height = 28;
            
            // Clear Response button
            var clearBtn = new Button(() => 
            {
                if (_responseText != null) _responseText.text = "Select code and click an action below.";
            }) { text = "Clear" };
            clearBtn.style.flexGrow = 1;
            clearBtn.style.height = 28;
            
            buttonContainer.Add(copyBtn);
            buttonContainer.Add(clearBtn);
            _responseContainer.Add(buttonContainer);
        }

        public void SetText(string text)
        {
            if (_responseText != null) _responseText.text = text;
        }

        public void AppendText(string text)
        {
            if (_responseText != null)
            {
                _responseText.text += text;
                // Auto-scroll to bottom
                if (_scroll != null)
                {
                    _scroll.scrollOffset = new Vector2(0, float.MaxValue);
                }
            }
        }
    }
}
