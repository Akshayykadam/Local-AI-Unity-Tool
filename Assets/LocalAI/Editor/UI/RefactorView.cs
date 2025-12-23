using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services.Refactoring;

namespace LocalAI.Editor.UI
{
    /// <summary>
    /// UI View for the Refactor tab - Code Navigation & AI-Assisted Refactoring.
    /// </summary>
    public class RefactorView
    {
        private readonly VisualElement _root;
        private readonly SymbolResolver _resolver;
        private readonly RefactoringSafetyChecker _safetyChecker;
        
        // UI Elements
        private TextField _symbolSearchField;
        private ScrollView _symbolList;
        private VisualElement _selectedSymbolPanel;
        private Label _symbolNameLabel;
        private Label _symbolTypeLabel;
        private Label _symbolPathLabel;
        private VisualElement _actionsPanel;
        private VisualElement _previewPanel;
        private Label _previewOriginal;
        private Label _previewNew;
        private Label _statusLabel;
        private VisualElement _resultsPanel;
        
        private CodeSymbol _selectedSymbol;
        private RefactoringPreview _currentPreview;
        private RefactoringOperation _pendingOperation;
        
        private bool _isIndexBuilt = false;

        public RefactorView(VisualElement root)
        {
            _root = root;
            _resolver = new SymbolResolver();
            _safetyChecker = new RefactoringSafetyChecker();
            
            BuildUI();
        }

        private void BuildUI()
        {
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            
            // Header
            var header = new Label("Refactor");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8;
            _root.Add(header);
            
            var description = new Label("Navigate and refactor your codebase with AI assistance.");
            description.style.fontSize = 10;
            description.style.color = new Color(0.6f, 0.6f, 0.6f);
            description.style.marginBottom = 12;
            _root.Add(description);
            
            // Status and Build Index button
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 8;
            
            _statusLabel = new Label("Index not built");
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.flexGrow = 1;
            statusRow.Add(_statusLabel);
            
            var buildBtn = new Button(BuildIndex) { text = "Build Index" };
            buildBtn.style.height = 20;
            buildBtn.style.fontSize = 10;
            statusRow.Add(buildBtn);
            
            _root.Add(statusRow);
            
            // Symbol Search
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 8;
            
            _symbolSearchField = new TextField();
            _symbolSearchField.style.flexGrow = 1;
            _symbolSearchField.style.height = 22;
            _symbolSearchField.value = "Search symbols...";
            _symbolSearchField.RegisterValueChangedCallback(OnSearchChanged);
            _symbolSearchField.RegisterCallback<FocusInEvent>(_ => {
                if (_symbolSearchField.value == "Search symbols...") _symbolSearchField.value = "";
            });
            _symbolSearchField.RegisterCallback<FocusOutEvent>(_ => {
                if (string.IsNullOrWhiteSpace(_symbolSearchField.value)) _symbolSearchField.value = "Search symbols...";
            });
            searchRow.Add(_symbolSearchField);
            
            _root.Add(searchRow);
            
            // Symbol List
            _symbolList = new ScrollView();
            _symbolList.style.maxHeight = 120;
            _symbolList.style.marginBottom = 8;
            _symbolList.style.borderTopWidth = 1;
            _symbolList.style.borderRightWidth = 1;
            _symbolList.style.borderBottomWidth = 1;
            _symbolList.style.borderLeftWidth = 1;
            _symbolList.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            _symbolList.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            _symbolList.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            _symbolList.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            _root.Add(_symbolList);
            
            // Selected Symbol Panel
            _selectedSymbolPanel = new VisualElement();
            _selectedSymbolPanel.style.borderTopWidth = 1;
            _selectedSymbolPanel.style.borderRightWidth = 1;
            _selectedSymbolPanel.style.borderBottomWidth = 1;
            _selectedSymbolPanel.style.borderLeftWidth = 1;
            _selectedSymbolPanel.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            _selectedSymbolPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            _selectedSymbolPanel.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            _selectedSymbolPanel.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            _selectedSymbolPanel.style.paddingTop = 6;
            _selectedSymbolPanel.style.paddingRight = 6;
            _selectedSymbolPanel.style.paddingBottom = 6;
            _selectedSymbolPanel.style.paddingLeft = 6;
            _selectedSymbolPanel.style.marginBottom = 8;
            _selectedSymbolPanel.style.display = DisplayStyle.None;
            
            _symbolNameLabel = new Label();
            _symbolNameLabel.style.fontSize = 12;
            _symbolNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selectedSymbolPanel.Add(_symbolNameLabel);
            
            _symbolTypeLabel = new Label();
            _symbolTypeLabel.style.fontSize = 10;
            _symbolTypeLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
            _selectedSymbolPanel.Add(_symbolTypeLabel);
            
            _symbolPathLabel = new Label();
            _symbolPathLabel.style.fontSize = 9;
            _symbolPathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _selectedSymbolPanel.Add(_symbolPathLabel);
            
            _root.Add(_selectedSymbolPanel);
            
            // Actions Panel
            _actionsPanel = new VisualElement();
            _actionsPanel.style.flexDirection = FlexDirection.Row;
            _actionsPanel.style.flexWrap = Wrap.Wrap;
            _actionsPanel.style.marginBottom = 8;
            _actionsPanel.style.display = DisplayStyle.None;
            
            var renameBtn = new Button(() => StartRename()) { text = "Rename" };
            renameBtn.style.height = 22;
            renameBtn.style.marginRight = 4;
            _actionsPanel.Add(renameBtn);
            
            var findRefsBtn = new Button(() => FindReferences()) { text = "Find References" };
            findRefsBtn.style.height = 22;
            findRefsBtn.style.marginRight = 4;
            _actionsPanel.Add(findRefsBtn);
            
            var callHierarchyBtn = new Button(() => ShowCallHierarchy()) { text = "Call Hierarchy" };
            callHierarchyBtn.style.height = 22;
            callHierarchyBtn.style.marginRight = 4;
            _actionsPanel.Add(callHierarchyBtn);
            
            var goToDefBtn = new Button(() => GoToDefinition()) { text = "Go to Definition" };
            goToDefBtn.style.height = 22;
            _actionsPanel.Add(goToDefBtn);
            
            _root.Add(_actionsPanel);
            
            // Results Panel
            _resultsPanel = new ScrollView();
            _resultsPanel.style.flexGrow = 1;
            _resultsPanel.style.borderTopWidth = 1;
            _resultsPanel.style.borderRightWidth = 1;
            _resultsPanel.style.borderBottomWidth = 1;
            _resultsPanel.style.borderLeftWidth = 1;
            _resultsPanel.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            _resultsPanel.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            _resultsPanel.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            _resultsPanel.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            _resultsPanel.style.paddingTop = 6;
            _resultsPanel.style.paddingRight = 6;
            _resultsPanel.style.paddingBottom = 6;
            _resultsPanel.style.paddingLeft = 6;
            _root.Add(_resultsPanel);
            
            // Preview Panel (for refactoring)
            _previewPanel = new VisualElement();
            _previewPanel.style.flexDirection = FlexDirection.Row;
            _previewPanel.style.flexGrow = 1;
            _previewPanel.style.display = DisplayStyle.None;
            _previewPanel.style.marginTop = 8;
            
            var originalPanel = new VisualElement();
            originalPanel.style.flexGrow = 1;
            originalPanel.style.marginRight = 4;
            originalPanel.style.borderTopWidth = 1;
            originalPanel.style.borderRightWidth = 1;
            originalPanel.style.borderBottomWidth = 1;
            originalPanel.style.borderLeftWidth = 1;
            originalPanel.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            originalPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            originalPanel.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            originalPanel.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            originalPanel.style.paddingTop = 4;
            originalPanel.style.paddingRight = 4;
            originalPanel.style.paddingBottom = 4;
            originalPanel.style.paddingLeft = 4;
            
            var origHeader = new Label("Original");
            origHeader.style.fontSize = 10;
            origHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            originalPanel.Add(origHeader);
            
            _previewOriginal = new Label();
            _previewOriginal.style.fontSize = 10;
            _previewOriginal.style.whiteSpace = WhiteSpace.Normal;
            originalPanel.Add(_previewOriginal);
            
            _previewPanel.Add(originalPanel);
            
            var newPanel = new VisualElement();
            newPanel.style.flexGrow = 1;
            newPanel.style.borderTopWidth = 1;
            newPanel.style.borderRightWidth = 1;
            newPanel.style.borderBottomWidth = 1;
            newPanel.style.borderLeftWidth = 1;
            newPanel.style.borderTopColor = new Color(0.3f, 0.5f, 0.3f);
            newPanel.style.borderRightColor = new Color(0.3f, 0.5f, 0.3f);
            newPanel.style.borderBottomColor = new Color(0.3f, 0.5f, 0.3f);
            newPanel.style.borderLeftColor = new Color(0.3f, 0.5f, 0.3f);
            newPanel.style.paddingTop = 4;
            newPanel.style.paddingRight = 4;
            newPanel.style.paddingBottom = 4;
            newPanel.style.paddingLeft = 4;
            
            var newHeader = new Label("Proposed");
            newHeader.style.fontSize = 10;
            newHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            newPanel.Add(newHeader);
            
            _previewNew = new Label();
            _previewNew.style.fontSize = 10;
            _previewNew.style.whiteSpace = WhiteSpace.Normal;
            newPanel.Add(_previewNew);
            
            _previewPanel.Add(newPanel);
            _root.Add(_previewPanel);
        }

        private void BuildIndex()
        {
            _statusLabel.text = "Building index...";
            
            EditorApplication.delayCall += () =>
            {
                try
                {
                    string projectPath = Application.dataPath;
                    _resolver.BuildSymbolTable(projectPath);
                    _isIndexBuilt = true;
                    _statusLabel.text = $"Index ready: {_resolver.SymbolCount} symbols";
                }
                catch (Exception ex)
                {
                    _statusLabel.text = $"Error: {ex.Message}";
                }
            };
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            string query = evt.newValue;
            if (string.IsNullOrWhiteSpace(query) || query == "Search symbols..." || !_isIndexBuilt)
            {
                _symbolList.Clear();
                return;
            }
            
            var results = _resolver.SearchSymbols(query);
            DisplaySymbolList(results);
        }

        private void DisplaySymbolList(List<CodeSymbol> symbols)
        {
            _symbolList.Clear();
            
            foreach (var symbol in symbols.GetRange(0, Math.Min(symbols.Count, 20)))
            {
                var row = new Button(() => SelectSymbol(symbol));
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.height = 20;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 4;
                row.style.marginBottom = 1;
                
                var nameLabel = new Label($"{symbol.ParentClass}.{symbol.Name}");
                nameLabel.style.fontSize = 10;
                row.Add(nameLabel);
                
                var typeLabel = new Label(symbol.Type.ToString());
                typeLabel.style.fontSize = 9;
                typeLabel.style.color = new Color(0.5f, 0.6f, 0.8f);
                row.Add(typeLabel);
                
                _symbolList.Add(row);
            }
        }

        private void SelectSymbol(CodeSymbol symbol)
        {
            _selectedSymbol = symbol;
            
            _symbolNameLabel.text = $"{symbol.ParentClass}.{symbol.Name}";
            _symbolTypeLabel.text = $"{symbol.Type} | Line {symbol.StartLine}-{symbol.EndLine}";
            _symbolPathLabel.text = GetRelativePath(symbol.FilePath);
            
            // Show warnings if any
            if (symbol.IsUnityLifecycle)
            {
                _symbolTypeLabel.text += " | Unity Lifecycle";
            }
            if (symbol.IsSerializedField)
            {
                _symbolTypeLabel.text += " | SerializeField";
            }
            
            _selectedSymbolPanel.style.display = DisplayStyle.Flex;
            _actionsPanel.style.display = DisplayStyle.Flex;
            _resultsPanel.Clear();
            _previewPanel.style.display = DisplayStyle.None;
        }

        private void StartRename()
        {
            if (_selectedSymbol == null) return;
            
            _resultsPanel.Clear();
            
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            var label = new Label("New name:");
            label.style.fontSize = 10;
            label.style.marginRight = 8;
            container.Add(label);
            
            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.style.height = 22;
            nameField.value = _selectedSymbol.Name;
            container.Add(nameField);
            
            var previewBtn = new Button(() => PreviewRename(nameField.value)) { text = "Preview" };
            previewBtn.style.height = 22;
            previewBtn.style.marginLeft = 4;
            container.Add(previewBtn);
            
            _resultsPanel.Add(container);
        }

        private void PreviewRename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName == _selectedSymbol.Name)
            {
                ShowResult("Please enter a different name.");
                return;
            }
            
            var operation = new RenameOperation(_resolver, _selectedSymbol, newName);
            _currentPreview = operation.Prepare();
            _pendingOperation = operation;
            
            DisplayPreview(_currentPreview);
        }

        private void DisplayPreview(RefactoringPreview preview)
        {
            _resultsPanel.Clear();
            
            // Safety report
            var safetyLabel = new Label(preview.SafetyReport.GetSummary());
            safetyLabel.style.fontSize = 10;
            safetyLabel.style.marginBottom = 8;
            
            if (preview.SafetyReport.RiskLevel == RiskLevel.High)
                safetyLabel.style.color = new Color(0.9f, 0.3f, 0.3f);
            else if (preview.SafetyReport.RiskLevel == RiskLevel.Medium)
                safetyLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
            else
                safetyLabel.style.color = new Color(0.3f, 0.8f, 0.3f);
            
            _resultsPanel.Add(safetyLabel);
            
            // Warnings
            foreach (var warning in preview.SafetyReport.Warnings)
            {
                var warnLabel = new Label($"⚠ {warning}");
                warnLabel.style.fontSize = 9;
                warnLabel.style.color = new Color(0.9f, 0.7f, 0.2f);
                warnLabel.style.marginBottom = 2;
                _resultsPanel.Add(warnLabel);
            }
            
            // Summary
            var summaryLabel = new Label($"Files affected: {preview.FilesAffected.Count} | Changes: {preview.TotalChanges}");
            summaryLabel.style.fontSize = 10;
            summaryLabel.style.marginTop = 8;
            summaryLabel.style.marginBottom = 8;
            _resultsPanel.Add(summaryLabel);
            
            // Apply/Cancel buttons
            if (preview.SafetyReport.CanProceed)
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;
                
                var applyBtn = new Button(ApplyRefactoring) { text = "Apply" };
                applyBtn.style.height = 24;
                applyBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
                btnRow.Add(applyBtn);
                
                var cancelBtn = new Button(CancelRefactoring) { text = "Cancel" };
                cancelBtn.style.height = 24;
                cancelBtn.style.marginLeft = 4;
                btnRow.Add(cancelBtn);
                
                _resultsPanel.Add(btnRow);
            }
            
            // Show diff preview
            if (preview.DiffPreviews.Count > 0)
            {
                var diff = preview.DiffPreviews[0];
                _previewOriginal.text = TruncatePreview(diff.OriginalContent, 500);
                _previewNew.text = TruncatePreview(diff.NewContent, 500);
                _previewPanel.style.display = DisplayStyle.Flex;
            }
        }

        private void ApplyRefactoring()
        {
            if (_pendingOperation == null) return;
            
            bool success = _pendingOperation.Apply();
            
            if (success)
            {
                ShowResult("Refactoring applied successfully!");
                _previewPanel.style.display = DisplayStyle.None;
                _pendingOperation = null;
                _currentPreview = null;
                
                // Rebuild index
                BuildIndex();
            }
            else
            {
                ShowResult("Failed to apply refactoring. Check console for details.");
            }
        }

        private void CancelRefactoring()
        {
            _pendingOperation = null;
            _currentPreview = null;
            _previewPanel.style.display = DisplayStyle.None;
            _resultsPanel.Clear();
        }

        private void FindReferences()
        {
            if (_selectedSymbol == null) return;
            
            _resultsPanel.Clear();
            
            var references = _resolver.GetReferences(_selectedSymbol.Name);
            
            var header = new Label($"Found {references.Count} references to '{_selectedSymbol.Name}':");
            header.style.fontSize = 10;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8;
            _resultsPanel.Add(header);
            
            foreach (var reference in references)
            {
                var refBtn = new Button(() => OpenFileAtLine(reference.FilePath, reference.Line));
                refBtn.style.flexDirection = FlexDirection.Column;
                refBtn.style.alignItems = Align.FlexStart;
                refBtn.style.marginBottom = 2;
                refBtn.style.paddingLeft = 4;
                
                var pathLabel = new Label($"{GetRelativePath(reference.FilePath)}:{reference.Line}");
                pathLabel.style.fontSize = 10;
                pathLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
                refBtn.Add(pathLabel);
                
                var contextLabel = new Label(reference.Context);
                contextLabel.style.fontSize = 9;
                contextLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                refBtn.Add(contextLabel);
                
                _resultsPanel.Add(refBtn);
            }
        }

        private void ShowCallHierarchy()
        {
            if (_selectedSymbol == null || _selectedSymbol.Type != SymbolType.Method) return;
            
            _resultsPanel.Clear();
            
            var hierarchy = _resolver.GetCallHierarchy(_selectedSymbol);
            
            var header = new Label($"Call Hierarchy for '{_selectedSymbol.Name}':");
            header.style.fontSize = 10;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8;
            _resultsPanel.Add(header);
            
            // Callers
            if (hierarchy.Callers.Count > 0)
            {
                var callersHeader = new Label("Called by:");
                callersHeader.style.fontSize = 10;
                callersHeader.style.marginTop = 4;
                _resultsPanel.Add(callersHeader);
                
                foreach (var caller in hierarchy.Callers)
                {
                    var btn = new Button(() => SelectSymbol(caller.Symbol));
                    btn.text = $"  ← {caller.Symbol.ParentClass}.{caller.Symbol.Name}";
                    btn.style.fontSize = 10;
                    btn.style.height = 18;
                    _resultsPanel.Add(btn);
                }
            }
            
            // Callees
            if (hierarchy.Callees.Count > 0)
            {
                var calleesHeader = new Label("Calls:");
                calleesHeader.style.fontSize = 10;
                calleesHeader.style.marginTop = 8;
                _resultsPanel.Add(calleesHeader);
                
                foreach (var callee in hierarchy.Callees)
                {
                    var btn = new Button(() => SelectSymbol(callee.Symbol));
                    btn.text = $"  → {callee.Symbol.ParentClass}.{callee.Symbol.Name}";
                    btn.style.fontSize = 10;
                    btn.style.height = 18;
                    _resultsPanel.Add(btn);
                }
            }
            
            if (hierarchy.Callers.Count == 0 && hierarchy.Callees.Count == 0)
            {
                ShowResult("No call relationships found.");
            }
        }

        private void GoToDefinition()
        {
            if (_selectedSymbol == null) return;
            OpenFileAtLine(_selectedSymbol.FilePath, _selectedSymbol.StartLine);
        }

        private void ShowResult(string message)
        {
            _resultsPanel.Clear();
            var label = new Label(message);
            label.style.fontSize = 10;
            _resultsPanel.Add(label);
        }

        private void OpenFileAtLine(string filePath, int line)
        {
            string assetPath = filePath.Replace("\\", "/");
            int assetsIndex = assetPath.IndexOf("Assets/");
            if (assetsIndex >= 0)
            {
                assetPath = assetPath.Substring(assetsIndex);
            }
            
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, line);
            }
            else
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, line);
            }
        }

        private string GetRelativePath(string fullPath)
        {
            string normalized = fullPath.Replace("\\", "/");
            int assetsIndex = normalized.IndexOf("Assets/");
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex) : Path.GetFileName(fullPath);
        }

        private string TruncatePreview(string content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "";
            if (content.Length <= maxLength) return content;
            return content.Substring(0, maxLength) + "\n...";
        }
    }
}
