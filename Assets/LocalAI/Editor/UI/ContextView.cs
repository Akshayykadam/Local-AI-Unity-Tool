using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class ContextView
    {
        private readonly ContextCollector _collector;
        private readonly Label _contextText;

        public ContextView(VisualElement root, ContextCollector collector)
        {
            _collector = collector;
            _contextText = root.Q<Label>("context-text");
        }

        public void RefreshContext()
        {
            if (_contextText != null)
            {
                _contextText.text = _collector.CollectContext();
            }
        }
    }
}
