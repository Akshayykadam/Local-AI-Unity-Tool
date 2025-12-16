using UnityEngine.UIElements;
using LocalAI.Editor.Services;
using UnityEditor;

namespace LocalAI.Editor.UI
{
    public class SettingsView
    {
        private readonly ModelManager _modelManager;
        private readonly VisualElement _settingsContainer;
        private readonly VisualElement _mainContainer;
        
        private readonly TextField _pathField;
        private readonly Button _deleteBtn;
        private readonly Button _closeBtn;

        public SettingsView(VisualElement root, ModelManager modelManager)
        {
            _modelManager = modelManager;
            _settingsContainer = root.Q<VisualElement>("settings-container");
            _mainContainer = root.Q<VisualElement>("content-container"); // Actually main-split is inside content-container? No, main-split is direct child in old UXML?
            // Wait, I updated UXML. Look at structure.
            // content-container wraps main-split?
            // Let's check the UXML structure.
            // In the UXML file: root -> header, settings, content-container(main-split).
            // So to hide main content, hide content-container.
            
            _mainContainer = root.Q<VisualElement>("content-container");

            _pathField = root.Q<TextField>("field-model-path");
            _deleteBtn = root.Q<Button>("btn-delete-model");
            _closeBtn = root.Q<Button>("btn-close-settings");

            // Init
            _pathField.value = _modelManager.GetModelDirectory();
            
            _deleteBtn.clicked += DeleteModel;
            _closeBtn.clicked += Hide;

            // Header button hookup needs to happen externally or via event. 
            // Better: LocalAIEditorWindow handles the toggle, or we expose Show/Hide methods.
        }

        public void Show()
        {
            _pathField.value = _modelManager.GetModelDirectory();
            _settingsContainer.style.display = DisplayStyle.Flex;
            if (_mainContainer != null) _mainContainer.style.display = DisplayStyle.None;
        }

        public void Hide()
        {
            _settingsContainer.style.display = DisplayStyle.None;
            if (_mainContainer != null) _mainContainer.style.display = DisplayStyle.Flex;
        }

        private void DeleteModel()
        {
            if (EditorUtility.DisplayDialog("Delete Model?", "Are you sure you want to delete the downloaded model?", "Yes", "No"))
            {
                // _modelManager.DeleteModel(); // Need to implement this in Manager
                // Mock for now
                UnityEngine.Debug.Log("Model deleted (simulated)");
                // _modelManager.SetState(ModelManager.ModelState.NotInstalled);
            }
        }
    }
}
