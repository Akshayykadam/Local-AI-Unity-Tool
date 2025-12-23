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
            
            // Wire up Clear button if it exists
            var clearBtn = root.Q<Button>("btn-clear");
            if (clearBtn != null)
            {
                clearBtn.clicked += () =>
                {
                    if (_responseText != null) _responseText.value = "Select code and click an action below.";
                };
            }
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
