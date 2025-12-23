# Local AI Assistant for Unity

![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-000000?style=flat-square&logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/Status-Working-brightgreen?style=flat-square)

> **AI coding assistant for Unity Editor with local and cloud AI options.**

---
## Preview
![ScreenRecording2025-12-22at10 31 48PM-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/2e8187e5-79ad-4e5b-8ab9-ccde35563e84)

## Features

| Feature | Description |
| :--- | :--- |
| **Local + Cloud AI** | Choose between offline local inference or cloud APIs (Gemini, OpenAI, Claude) |
| **100% Private (Local)** | Local mode - all processing happens on your machine. No data leaves. |
| **Unity Native UI** | Built with UI Toolkit, matches Unity Editor styling. |
| **C# Code Only** | AI generates only C#/Unity code, never Python or other languages. |
| **Clean Output** | Response shows only AI content - ready to copy. |
| **Configurable** | Adjust context size (1K-8K) and response length. |
| **Multi-Query** | Run multiple queries without restarting Unity. |

---

## Quick Start

### 1. Install Native Libraries
### 1. Install Native Libraries
> **Tools → Local AI → Install Native Libraries**

Downloads `llama.cpp` binaries to `Assets/LocalAI/Plugins/`.
**Required** for local inference.

### 2. Download AI Model
> **Tools → Local AI Assistant → Settings → Download Model**

Downloads **Mistral 7B Instruct (Q4_K_M)** (~4GB):
- macOS: `~/Library/Application Support/LocalAIUnity/models/`
- Windows: `%AppData%\LocalAIUnity\models\`

### 3. Use the Assistant
1. **Select** a GameObject (Hierarchy) or Script (Project) to give the AI context.
   - *The AI automatically analyzes components, code, and hierarchy.*
2. **Type your question** (e.g., "Add a jump feature") in the input field.
3. Click an action:
   - **Ask**: Best for general questions about the selected object.
   - **Explain Error**: Fixes errors relevant to your selection.
   - **Explain Code**: Explains the selected script or object logic.
   - **Generate**: Generates new code using your selection as a reference.
4. **Copy** the result.

### 4. Analyze Scene
> **Tools → Local AI Assistant → Analyze Scene**

Generates a comprehensive report of your current scene, flagging:
- Performance risks (Polycount, Draw calls)
- Cleanup opportunities (Empty objects, Missing scripts)
- Platform-specific warnings (Mobile/VR)

> **Tip**: Toggle "Include Selection Context" in the UI to control if the AI sees your selection.

---

## Features
| Feature | Description |
| :--- | :--- |
| **🔍 Project Search** | [NEW] Semantic code search - ask questions about your codebase in natural language. |
| **Ask Button** | [NEW] Dedicated Q&A mode for general Unity questions. |
| **Scene Analyzer** | [NEW] One-click scene performance and hygiene analysis. |
| **Smart Context** | [NEW] Select objects/scripts to automatically analyze them. |
| **Log Integration** | [NEW] Import Console errors (stack trace + message) into context. |
| **Visual Limits** | [NEW] Real-time usage bar and dynamic safety limits. |
| **Safety Guard** | [NEW] Actions disabled if selection exceeds limits. |
| **Local + Cloud AI** | Choose offline inference or cloud APIs (Gemini, OpenAI, Claude). |
| **100% Private** | Local mode is fully offline. Cloud mode is opt-in. |
| **Unity Native UI** | Matches Editor styling. Selectable text. |
| **C# Code Only** | Optimized for Unity development. |
| **Configurable** | Adjust model, context size, response length. |

---

## Project Search (Semantic Code Search)

Search your codebase using natural language queries like "Where is player movement handled?" or "Find damage calculation logic".

### How It Works
1. Click **🔍 Search** in the header to open the Search panel
2. Click **Re-Index** to build a semantic index of your project (first time only)
3. Type a natural language query and press Enter
4. Results show:
   - **Matching code chunks** with file paths and line numbers
   - **AI Summary** explaining the relevant code
5. Click **Open** on any result to jump directly to that line in your IDE

### Configuration
In Settings:
- **Indexed Folders**: Which folders to scan (default: `Assets/`)
- **Max Files**: Limit for large projects (default: 5000)
- **Auto Re-index**: Toggle automatic updates when files change

> **Tip**: The index is stored in `Library/LocalAI/SemanticIndex/` and persists across sessions.

---

## Cloud AI Providers (Optional)

For faster responses or when you don't want to download the 4GB local model, use cloud AI:

### Setup
1. Open **Tools → Local AI Assistant → Settings**
2. Select a provider from the **AI Provider** dropdown:
   - **Google Gemini** - Defaults to `gemini-2.5-flash-lite` (Free Tier friendly)
   - **OpenAI** - Uses `gpt-4o-mini`
   - **Anthropic Claude** - Uses `claude-3-haiku`
3. Enter your API key (get one from the provider's website)
4. Start querying!

### Gemini Model Selection
Exclusively for Gemini, you can choose the specific model version in Settings:
- **Gemini 2.5 Flash Lite** (Default) - Optimized for speed and free tier usage.

### Get API Keys
| Provider | Link |
| :--- | :--- |
| Google Gemini | [aistudio.google.com](https://aistudio.google.com) |
| OpenAI | [platform.openai.com](https://platform.openai.com) |
| Anthropic Claude | [console.anthropic.com](https://console.anthropic.com) |

> ⚠️ **Privacy Note**: Cloud providers send your code to external servers. Use local mode for sensitive projects.

---

## Settings

Click **Settings** in the toolbar to configure:

| Setting | Options | Description |
| :--- | :--- | :--- |
| **AI Provider** | Local, Gemini, OpenAI, Claude | Choose inference engine |
| **Gemini Model** | 2.5 Flash Lite | Specific model version (Gemini only) |
| **Context Size** | 2K, 4K, 8K, 16K, 32K | Larger = more code analyzed, more RAM |
| **Max Response** | 512, 1024, 2048, 4096, 8192 | Maximum tokens the AI will generate |

## Improved UI Features
- **Selectable Text**: Response text can now be selected and copied (Read-only text field).
- **Persistent Actions**: "Copy" and "Clear" buttons are always visible in the header.
- **Smart Status**: Status bar indicates the active model (Local vs Cloud).

---

## Tech Stack

| Component | Technology |
| :--- | :--- |
| **UI** | Unity UI Toolkit (UXML/USS) |
| **Backend** | C# with safe P/Invoke marshalling |
| **Inference** | `llama.cpp` (ARM64/x64 native) |
| **Model** | Mistral-7B-Instruct-v0.1-GGUF (Q4_K_M) |

---

## Project Structure

```
Assets/LocalAI/
├── Editor/
│   ├── Services/
│   │   ├── IInferenceService.cs     # Common interface for all providers
│   │   ├── InferenceService.cs      # Local llama.cpp inference
│   │   ├── GeminiInferenceService.cs  # Google Gemini API
│   │   ├── OpenAIInferenceService.cs  # OpenAI API
│   │   ├── ClaudeInferenceService.cs  # Anthropic Claude API
│   │   ├── LocalAISettings.cs       # User preferences & API keys
│   │   ├── ModelManager.cs          # Model state & download
│   │   └── SemanticSearch/          # [NEW] Project Search feature
│   │       ├── CSharpParser.cs      # Parse C# files
│   │       ├── ProjectScanner.cs    # Scan project folders
│   │       ├── EmbeddingService.cs  # Generate embeddings
│   │       ├── VectorStore.cs       # Store & search vectors
│   │       ├── SemanticIndex.cs     # Index coordinator
│   │       └── RAGService.cs        # Query with LLM
│   ├── UI/
│   │   ├── SettingsView.cs          # Settings + provider selection
│   │   ├── ActionBarView.cs         # Provider-aware actions
│   │   ├── ProjectSearchView.cs     # [NEW] Search UI panel
│   │   └── ...
│   └── Setup/
│       └── NativeSetup.cs           # Binary installer
├── Runtime/
│   └── Native/
│       └── LLMNativeBridge.cs       # Safe P/Invoke bindings
└── Plugins/
    └── (libllama.dylib / llama.dll) # Downloaded automatically
```

---

## Requirements

- **Unity 2021.3** or later
- **RAM**: 8GB minimum, 16GB recommended
- **Disk**: ~5GB (model + libraries)
- **macOS**: Apple Silicon (M1/M2/M3) or Intel
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
