using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LocalAI.Editor.UI
{
    /// <summary>
    /// Represents a single tab in the tab system.
    /// </summary>
    public class TabInfo
    {
        public string Label;
        public string Name;
        public VisualElement Content;
        public Button TabButton;
    }

    /// <summary>
    /// Horizontal tab navigation system.
    /// </summary>
    public class TabSystem
    {
        public event Action<int, string> OnTabChanged;
        
        private readonly List<TabInfo> _tabs = new List<TabInfo>();
        private readonly VisualElement _tabBar;
        private readonly VisualElement _contentArea;
        private int _activeTabIndex = -1;
        
        public int ActiveTabIndex => _activeTabIndex;
        
        public TabSystem(VisualElement tabBar, VisualElement contentArea)
        {
            _tabBar = tabBar;
            _contentArea = contentArea;
        }
        
        /// <summary>
        /// Adds a new tab to the system.
        /// </summary>
        public void AddTab(string icon, string label, string name, VisualElement content)
        {
            var tab = new TabInfo
            {
                Label = label,
                Name = name,
                Content = content
            };
            
            // Create horizontal tab button
            var tabBtn = new Button(() => SelectTab(_tabs.IndexOf(tab)));
            tabBtn.name = $"tab-btn-{name.ToLower()}";
            tabBtn.text = label;
            tabBtn.AddToClassList("tab-btn");
            tabBtn.tooltip = label;
            
            tab.TabButton = tabBtn;
            _tabs.Add(tab);
            _tabBar.Add(tabBtn);
            
            // Add content to content area (hidden by default)
            content.name = $"tab-content-{name.ToLower()}";
            content.AddToClassList("tab-content");
            content.style.display = DisplayStyle.None;
            content.style.flexGrow = 1;
            _contentArea.Add(content);
        }
        
        /// <summary>
        /// Selects a tab by index.
        /// </summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            if (index == _activeTabIndex) return;
            
            // Deselect previous
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
            {
                var prevTab = _tabs[_activeTabIndex];
                prevTab.TabButton.RemoveFromClassList("tab-btn-active");
                prevTab.Content.style.display = DisplayStyle.None;
            }
            
            // Select new
            var newTab = _tabs[index];
            newTab.TabButton.AddToClassList("tab-btn-active");
            newTab.Content.style.display = DisplayStyle.Flex;
            
            _activeTabIndex = index;
            OnTabChanged?.Invoke(index, newTab.Name);
        }
        
        /// <summary>
        /// Selects a tab by name.
        /// </summary>
        public void SelectTab(string name)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    SelectTab(i);
                    return;
                }
            }
        }
        
        /// <summary>
        /// Gets the content element for a tab by name.
        /// </summary>
        public VisualElement GetTabContent(string name)
        {
            foreach (var tab in _tabs)
            {
                if (tab.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return tab.Content;
            }
            return null;
        }
    }
}
