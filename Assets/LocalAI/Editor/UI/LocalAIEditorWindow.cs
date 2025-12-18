using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;

namespace LocalAI.Editor.UI
{
    public class LocalAIEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Local AI Assistant")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<LocalAIEditorWindow>();
            wnd.titleContent = new GUIContent("Local AI");
            wnd.minSize = new Vector2(600, 400);
        }

        private ModelManager _modelManager;
        private InferenceService _inferenceService;
        private ContextCollector _contextCollector;

        private HeaderView _headerView;
        private SettingsView _settingsView;
        private ContextView _contextView;
        private ResponseView _responseView;
        private ActionBarView _actionBarView;

        public void CreateGUI()
        {
            // Initialize Services
            _modelManager = new ModelManager();
            _inferenceService = new InferenceService();
            _contextCollector = new ContextCollector();

            // Load UI
            var visualTree = Resources.Load<VisualTreeAsset>("LocalAIWindow");
            if (visualTree == null)
            {
                Debug.LogError("[LocalAI] Could not find LocalAIWindow.uxml in Resources");
                return;
            }
            visualTree.CloneTree(rootVisualElement);

            // Initialize Views
            _headerView = new HeaderView(rootVisualElement, _modelManager);
            _settingsView = new SettingsView(rootVisualElement, _modelManager); // New
            _contextView = new ContextView(rootVisualElement, _contextCollector);
            _responseView = new ResponseView(rootVisualElement);
            _actionBarView = new ActionBarView(rootVisualElement, _modelManager, _inferenceService, _contextView, _responseView);
            
            // Wire up Header Settings Button
            var settingsBtn = rootVisualElement.Q<Button>("btn-settings");
            if (settingsBtn != null) settingsBtn.clicked += _settingsView.Show;

            // Refresh initially
            _contextView.RefreshContext();
        }

        private void OnSelectionChange()
        {
            _contextView?.RefreshContext();
        }

        private void OnDestroy()
        {
            _modelManager?.CancelDownload();
        }
    }
}
