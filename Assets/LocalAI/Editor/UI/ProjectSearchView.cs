using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalAI.Editor.Services;
using LocalAI.Editor.Services.SemanticSearch;

namespace LocalAI.Editor.UI
{
    /// <summary>
    /// UI View for the Project Search (Semantic Search) feature.
    /// </summary>
    public class ProjectSearchView
    {
        private readonly VisualElement _root;
        private readonly VisualElement _container;
        private readonly SemanticIndex _index;
        private readonly RAGService _ragService;
        private readonly Func<IInferenceService> _getInferenceService;
        
        // UI Elements
        private TextField _searchInput;
        private Button _searchBtn;
        private Button _reindexBtn;
        private Button _clearBtn;
        private Label _statusLabel;
        private VisualElement _statusDot;
        private ProgressBar _progressBar;
        private ScrollView _resultsScroll;
        private VisualElement _resultsContainer;
        private Label _aiSummary;
        
        private CancellationTokenSource _cts;
        private List<SearchResult> _lastResults;
        
        public event Action<string, int> OnOpenFile;
        
        public ProjectSearchView(VisualElement root, SemanticIndex index, Func<IInferenceService> getInferenceService)
        {
            _root = root;
            _index = index;
            _getInferenceService = getInferenceService;
            _ragService = new RAGService(index, getInferenceService);
            
            // Create container
            _container = CreateUI();
            
            // Find the content-container and add after action-bar
            var contentContainer = root.Q<VisualElement>("content-container");
            if (contentContainer != null)
            {
                // Insert at the beginning of content container
                contentContainer.Insert(0, _container);
            }
            else
            {
                root.Add(_container);
            }
            
            // Subscribe to index events
            _index.OnIndexProgress += OnIndexProgress;
            _index.OnStateChanged += OnIndexStateChanged;
            
            // Initial state update
            UpdateStatus();
            
            // Hide by default (toggle via header)
            _container.style.display = DisplayStyle.None;
        }
        
        private VisualElement CreateUI()
        {
            var container = new VisualElement();
            container.name = "project-search-container";
            container.AddToClassList("panel");
            container.style.marginBottom = 4;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            
            var title = new Label("🔍 Project Search");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);
            
            var closeBtn = new Button(() => Hide()) { text = "✕" };
            closeBtn.style.width = 20;
            closeBtn.style.height = 18;
            closeBtn.style.fontSize = 10;
            header.Add(closeBtn);
            
            container.Add(header);
            
            // Search Row
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 6;
            
            _searchInput = new TextField();
            _searchInput.style.flexGrow = 1;
            _searchInput.style.height = 22;
            _searchInput.value = "";
            
            // Handle Enter key
            _searchInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    PerformSearch();
                }
            });
            
            // Placeholder text
            var inputField = _searchInput.Q<TextElement>();
            if (inputField != null)
            {
                _searchInput.RegisterCallback<FocusInEvent>(_ =>
                {
                    if (_searchInput.value == "Ask about your code...")
                        _searchInput.value = "";
                });
                _searchInput.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (string.IsNullOrWhiteSpace(_searchInput.value))
                        _searchInput.value = "Ask about your code...";
                });
                _searchInput.value = "Ask about your code...";
            }
            
            searchRow.Add(_searchInput);
            
            _searchBtn = new Button(PerformSearch) { text = "Search" };
            _searchBtn.style.width = 60;
            _searchBtn.style.marginLeft = 4;
            searchRow.Add(_searchBtn);
            
            container.Add(searchRow);
            
            // Status Row
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 6;
            
            _statusDot = new VisualElement();
            _statusDot.style.width = 8;
            _statusDot.style.height = 8;
            _statusDot.style.borderTopLeftRadius = 4;
            _statusDot.style.borderTopRightRadius = 4;
            _statusDot.style.borderBottomLeftRadius = 4;
            _statusDot.style.borderBottomRightRadius = 4;
            _statusDot.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            _statusDot.style.marginRight = 6;
            statusRow.Add(_statusDot);
            
            _statusLabel = new Label("Index: Idle");
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _statusLabel.style.flexGrow = 1;
            statusRow.Add(_statusLabel);
            
            _reindexBtn = new Button(StartReindex) { text = "Re-Index" };
            _reindexBtn.style.height = 18;
            _reindexBtn.style.fontSize = 10;
            _reindexBtn.style.paddingLeft = 6;
            _reindexBtn.style.paddingRight = 6;
            statusRow.Add(_reindexBtn);
            
            _clearBtn = new Button(ClearIndex) { text = "Clear" };
            _clearBtn.style.height = 18;
            _clearBtn.style.fontSize = 10;
            _clearBtn.style.paddingLeft = 6;
            _clearBtn.style.paddingRight = 6;
            _clearBtn.style.marginLeft = 4;
            statusRow.Add(_clearBtn);
            
            container.Add(statusRow);
            
            // Progress Bar (hidden by default)
            _progressBar = new ProgressBar();
            _progressBar.style.height = 4;
            _progressBar.style.marginBottom = 6;
            _progressBar.style.display = DisplayStyle.None;
            container.Add(_progressBar);
            
            // Results Container
            _resultsScroll = new ScrollView();
            _resultsScroll.style.flexGrow = 1;
            _resultsScroll.style.minHeight = 100;
            _resultsScroll.style.maxHeight = 250;
            
            _resultsContainer = new VisualElement();
            _resultsScroll.Add(_resultsContainer);
            container.Add(_resultsScroll);
            
            // AI Summary
            _aiSummary = new Label();
            _aiSummary.style.marginTop = 6;
            _aiSummary.style.fontSize = 11;
            _aiSummary.style.color = new Color(0.7f, 0.7f, 0.7f);
            _aiSummary.style.whiteSpace = WhiteSpace.Normal;
            _aiSummary.style.display = DisplayStyle.None;
            container.Add(_aiSummary);
            
            return container;
        }
        
        public void Show()
        {
            _container.style.display = DisplayStyle.Flex;
            _searchInput.Focus();
        }
        
        public void Hide()
        {
            _container.style.display = DisplayStyle.None;
        }
        
        public void Toggle()
        {
            if (_container.style.display == DisplayStyle.None)
                Show();
            else
                Hide();
        }
        
        private void UpdateStatus()
        {
            string stateText = _index.State switch
            {
                IndexState.Idle => "Idle (no index)",
                IndexState.Indexing => "Indexing...",
                IndexState.Ready => $"Ready ({_index.IndexedChunkCount} chunks)",
                IndexState.Error => "Error",
                _ => "Unknown"
            };
            
            _statusLabel.text = $"Index: {stateText}";
            
            Color dotColor = _index.State switch
            {
                IndexState.Idle => new Color(0.4f, 0.4f, 0.4f),
                IndexState.Indexing => new Color(0.96f, 0.74f, 0.01f),
                IndexState.Ready => new Color(0.55f, 0.78f, 0.25f),
                IndexState.Error => new Color(0.81f, 0.23f, 0.23f),
                _ => new Color(0.4f, 0.4f, 0.4f)
            };
            _statusDot.style.backgroundColor = dotColor;
            
            // Enable/disable buttons
            bool isIndexing = _index.State == IndexState.Indexing;
            _searchBtn.SetEnabled(!isIndexing && _index.State == IndexState.Ready);
            _reindexBtn.SetEnabled(!isIndexing);
            _clearBtn.SetEnabled(!isIndexing);
            
            // Show/hide progress bar
            _progressBar.style.display = isIndexing ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void OnIndexStateChanged(IndexState state)
        {
            // Must run on main thread
            EditorApplication.delayCall += UpdateStatus;
        }
        
        private void OnIndexProgress(float progress, string message)
        {
            EditorApplication.delayCall += () =>
            {
                _progressBar.value = progress * 100f;
                _progressBar.title = message;
                
                // Only update status label during active indexing
                if (_index.State == IndexState.Indexing)
                {
                    _statusLabel.text = message;
                }
                
                // When progress reaches 100%, call UpdateStatus to show proper final state
                if (progress >= 1f)
                {
                    UpdateStatus();
                }
            };
        }
        
        private async void StartReindex()
        {
            _resultsContainer.Clear();
            _aiSummary.style.display = DisplayStyle.None;
            
            var folders = LocalAISettings.GetIndexedFoldersList();
            
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            
            await _index.RebuildIndexAsync(folders, _cts.Token);
            
            UpdateStatus();
        }
        
        private void ClearIndex()
        {
            _cts?.Cancel();
            _index.ClearIndex();
            _resultsContainer.Clear();
            _aiSummary.style.display = DisplayStyle.None;
            UpdateStatus();
        }
        
        private async void PerformSearch()
        {
            string query = _searchInput.value;
            if (string.IsNullOrWhiteSpace(query) || query == "Ask about your code...")
            {
                return;
            }
            
            if (_index.State != IndexState.Ready)
            {
                _aiSummary.text = "⚠️ Please build the index first using 'Re-Index'.";
                _aiSummary.style.display = DisplayStyle.Flex;
                return;
            }
            
            _resultsContainer.Clear();
            _aiSummary.style.display = DisplayStyle.Flex;
            _aiSummary.text = "🔍 Searching...";
            
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            
            try
            {
                // First, get search results
                _lastResults = _index.Query(query, 5);
                DisplaySearchResults(_lastResults);
                
                // Then, get AI summary
                _aiSummary.text = "";
                
                await _ragService.QueryWithContextAsync(
                    query,
                    new Progress<string>(chunk =>
                    {
                        EditorApplication.delayCall += () =>
                        {
                            _aiSummary.text += chunk;
                        };
                    }),
                    _cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                _aiSummary.text = "[Search cancelled]";
            }
            catch (Exception ex)
            {
                _aiSummary.text = $"Error: {ex.Message}";
            }
        }
        
        private void DisplaySearchResults(List<SearchResult> results)
        {
            _resultsContainer.Clear();
            
            if (results.Count == 0)
            {
                var noResults = new Label("No results found.");
                noResults.style.color = new Color(0.6f, 0.6f, 0.6f);
                noResults.style.fontSize = 11;
                _resultsContainer.Add(noResults);
                return;
            }
            
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                _resultsContainer.Add(CreateResultCard(result, i + 1));
            }
        }
        
        private VisualElement CreateResultCard(SearchResult result, int rank)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            card.style.borderTopLeftRadius = 3;
            card.style.borderTopRightRadius = 3;
            card.style.borderBottomLeftRadius = 3;
            card.style.borderBottomRightRadius = 3;
            card.style.marginBottom = 4;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            
            var nameLabel = new Label($"{rank}. {result.Chunk.Name}");
            nameLabel.style.fontSize = 11;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;
            headerRow.Add(nameLabel);
            
            var scoreLabel = new Label($"{result.Score:P0}");
            scoreLabel.style.fontSize = 9;
            scoreLabel.style.color = new Color(0.5f, 0.7f, 0.5f);
            headerRow.Add(scoreLabel);
            
            card.Add(headerRow);
            
            // File path
            string relativePath = GetRelativePath(result.Chunk.FilePath);
            var fileLabel = new Label($"{relativePath}:{result.Chunk.StartLine}");
            fileLabel.style.fontSize = 10;
            fileLabel.style.color = new Color(0.5f, 0.6f, 0.8f);
            fileLabel.style.marginTop = 2;
            card.Add(fileLabel);
            
            // Summary or preview
            string preview = !string.IsNullOrEmpty(result.Chunk.Summary) 
                ? result.Chunk.Summary 
                : TruncateText(result.Chunk.Content, 100);
            
            var previewLabel = new Label(preview);
            previewLabel.style.fontSize = 10;
            previewLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            previewLabel.style.marginTop = 4;
            previewLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(previewLabel);
            
            // Open button
            var openBtn = new Button(() => OpenFileAtLine(result.Chunk.FilePath, result.Chunk.StartLine)) 
            { 
                text = "Open" 
            };
            openBtn.style.height = 16;
            openBtn.style.fontSize = 9;
            openBtn.style.marginTop = 4;
            openBtn.style.alignSelf = Align.FlexStart;
            card.Add(openBtn);
            
            return card;
        }
        
        private void OpenFileAtLine(string filePath, int line)
        {
            // Convert to asset path if needed
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
                // Try opening directly
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, line);
            }
            
            OnOpenFile?.Invoke(filePath, line);
        }
        
        private string GetRelativePath(string fullPath)
        {
            string normalized = fullPath.Replace("\\", "/");
            int assetsIndex = normalized.IndexOf("Assets/");
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex) : System.IO.Path.GetFileName(fullPath);
        }
        
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\n", " ").Replace("\r", "").Trim();
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }
    }
}
