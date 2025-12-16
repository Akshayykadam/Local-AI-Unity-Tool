using UnityEngine.UIElements;

namespace LocalAI.Editor.UI
{
    public class ResponseView
    {
        private readonly Label _responseText;
        private readonly ScrollView _scroll;

        public ResponseView(VisualElement root)
        {
            _responseText = root.Q<Label>("response-text");
            _scroll = root.Q<ScrollView>("response-scroll");
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
                // Auto scroll?
                // _scroll.ScrollTo(_responseText); // Not easily exposed in all Unity versions without extension
            }
        }
    }
}
