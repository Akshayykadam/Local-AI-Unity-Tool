using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;
using LocalAI.Editor.Services.SemanticSearch;

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
        private SemanticIndex _semanticIndex;

        private HeaderView _headerView;
        private SettingsView _settingsView;
        private ContextView _contextView;
        private ResponseView _responseView;
        private ActionBarView _actionBarView;
        private ProjectSearchView _projectSearchView;

        public void CreateGUI()
        {
            // Initialize Services
            _modelManager = new ModelManager();
            _inferenceService = new InferenceService();
            _contextCollector = new ContextCollector();
            _semanticIndex = new SemanticIndex();

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
            
            // Initialize Project Search View
            _projectSearchView = new ProjectSearchView(
                rootVisualElement, 
                _semanticIndex, 
                () => _actionBarView.GetActiveService()
            );
            
            // Wire up Header Settings Button
            var settingsBtn = rootVisualElement.Q<Button>("btn-settings");
            if (settingsBtn != null) settingsBtn.clicked += _settingsView.Show;
            
            // Wire up Project Search Button
            var searchBtn = rootVisualElement.Q<Button>("btn-project-search");
            if (searchBtn != null) searchBtn.clicked += _projectSearchView.Toggle;

            // ContextView initializes itself
        }

        private void OnDestroy()
        {
            _modelManager?.CancelDownload();
            _semanticIndex?.Dispose();
        }
    }
}
