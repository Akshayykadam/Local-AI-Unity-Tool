# Local AI Assistant for Unity

![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-000000?style=flat-square&logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/Status-Working-brightgreen?style=flat-square)

> **AI coding assistant for Unity Editor with local and cloud AI options.**

---

## Features

| Feature | Description |
| :--- | :--- |
| **Tabbed Interface** | Clean horizontal tabs: Chat, Search, Analyze, Settings |
| **Project Search** | Semantic code search - ask questions about your codebase |
| **Scene Analyzer** | One-click scene performance and hygiene analysis |
| **Local + Cloud AI** | Choose between offline local inference or cloud APIs |
| **100% Private (Local)** | Local mode - all processing happens on your machine |
| **Unity Native UI** | Built with UI Toolkit, matches Unity Editor styling |
| **C# Code Only** | AI generates only C#/Unity code, never Python |
| **Log Integration** | Import Console errors into context for debugging |

---

## Quick Start

### 1. Install Native Libraries
> **Tools → Local AI → Install Native Libraries**

Downloads `llama.cpp` binaries. **Required for local inference.**

### 2. Open the Assistant
> **Tools → Local AI Assistant**

### 3. Choose Your AI Provider
- **Local (Offline)**: Download the 4GB model in Settings tab
- **Cloud**: Enter API key for Gemini, OpenAI, or Claude

### 4. Use the Tabs

| Tab | Purpose |
| :--- | :--- |
| **Chat** | Ask questions, explain code, generate scripts |
| **Search** | Semantic search through your codebase |
| **Analyze** | Scene performance and hygiene report |
| **Settings** | Configure AI provider and preferences |

---

## Chat Tab

1. **Select** a GameObject or Script to give the AI context
2. **Type your question** in the User Input field
3. Click an action:
   - **Ask**: General questions about the selection
   - **Explain Error**: Fixes errors in your code
   - **Explain Code**: Explains the selected script
   - **Generate**: Creates new code
   - **Tests**: Generates unit tests
4. **Copy** the result

---

## Search Tab (Semantic Code Search)

Search your codebase using natural language queries.

### How to Use
1. Switch to **Search** tab
2. Click **Re-Index** to build the index (first time only)
3. Type a query like "Where is player movement handled?"
4. Results show matching code with file paths and line numbers
5. Click **Open** to jump to the code in your IDE

### Configuration
- **Indexed Folders**: Which folders to scan (default: `Assets/`)
- **Max Files**: Limit for large projects (default: 5000)

> Index stored in `Library/LocalAI/SemanticIndex/` and persists across sessions.

---

## Analyze Tab

Click **Analyze Current Scene** to get a comprehensive report:
- Performance risks (Polycount, Draw calls)
- Cleanup opportunities (Empty objects, Missing scripts)
- Platform-specific warnings (Mobile/VR)

---

## Settings Tab

| Setting | Options | Description |
| :--- | :--- | :--- |
| **AI Provider** | Local, Gemini, OpenAI, Claude | Choose inference engine |
| **API Key** | (hidden for Local) | Cloud provider API key |
| **Context Size** | 2K - 32K | Larger = more code analyzed |
| **Max Response** | 512 - 8192 | Maximum tokens generated |

### Get API Keys
| Provider | Link |
| :--- | :--- |
| Google Gemini | [aistudio.google.com](https://aistudio.google.com) |
| OpenAI | [platform.openai.com](https://platform.openai.com) |
| Anthropic Claude | [console.anthropic.com](https://console.anthropic.com) |

> ⚠️ **Privacy Note**: Cloud providers send your code to external servers. Use local mode for sensitive projects.

---

## Tech Stack

| Component | Technology |
| :--- | :--- |
| **UI** | Unity UI Toolkit (UXML/USS) |
| **Backend** | C# with P/Invoke marshalling |
| **Inference** | `llama.cpp` (ARM64/x64 native) |
| **Model** | Mistral-7B-Instruct-v0.1-GGUF (Q4_K_M) |

---

## Project Structure

```
Assets/LocalAI/
├── Editor/
│   ├── Services/
│   │   ├── IInferenceService.cs        # Common interface
│   │   ├── InferenceService.cs         # Local llama.cpp
│   │   ├── GeminiInferenceService.cs   # Google Gemini API
│   │   ├── OpenAIInferenceService.cs   # OpenAI API
│   │   ├── ClaudeInferenceService.cs   # Anthropic Claude API
│   │   ├── LocalAISettings.cs          # User preferences
│   │   └── SemanticSearch/             # Project Search
│   │       ├── SemanticIndex.cs        # Index coordinator
│   │       ├── VectorStore.cs          # Vector storage
│   │       └── RAGService.cs           # Query with LLM
│   ├── UI/
│   │   ├── LocalAIEditorWindow.cs      # Main window
│   │   ├── TabSystem.cs                # Horizontal tabs
│   │   ├── ProjectSearchView.cs        # Search UI
│   │   └── Resources/
│   │       ├── LocalAIWindow.uxml      # Layout
│   │       └── LocalAIStyles.uss       # Styling
│   └── Setup/
│       └── NativeSetup.cs              # Binary installer
└── Plugins/
    └── (libllama.dylib / llama.dll)    # Downloaded automatically
```

---

## Requirements

- **Unity 2021.3** or later
- **RAM**: 8GB minimum, 16GB recommended
- **Disk**: ~5GB (model + libraries)
- **macOS**: Apple Silicon or Intel
- **Windows**: x64 with AVX2 support

---

## Known Limitations

- **First Load**: Model takes 10-30 seconds to load initially
- **Memory**: Uses ~6GB RAM during inference
- **Context Recreation**: Each query recreates context (adds ~100ms)

---

## License

MIT — Use it, modify it, ship it.

---

<p align="center">
  <i>Built with C#, and way too much coffee.</i> ☕
</p>
