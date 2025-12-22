# Local AI Assistant for Unity

![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-000000?style=flat-square&logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/Status-Working-brightgreen?style=flat-square)

> **AI coding assistant for Unity Editor with local and cloud AI options.**

---
## UI
<img src="https://github.com/user-attachments/assets/f78e7980-463b-4fc9-a8f8-c913449d62be" width="160" />
<img src="https://github.com/user-attachments/assets/42d2065b-c31a-4562-94c8-f725cf47ddcd" width="160" />
<img src="https://github.com/user-attachments/assets/b33dab62-5e02-4382-83bb-374408d4c840" width="160" />
<img src="https://github.com/user-attachments/assets/1665d517-6c27-4af2-be72-e565889bc667" width="160" />


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
> **Tools → Local AI → Install Native Libraries**

Downloads `llama.cpp` binaries (~50MB) to `Assets/LocalAI/Plugins/`.

### 2. Download AI Model
> **Tools → Local AI Assistant → Settings → Download Model**

Downloads **Mistral 7B Instruct (Q4_K_M)** (~4GB):
- macOS: `~/Library/Application Support/LocalAIUnity/models/`
- Windows: `%AppData%\LocalAIUnity\models\`

### 3. Use the Assistant
1. Open **Tools → Local AI Assistant**
2. **Type your question** (e.g., "Why does my async method freeze?") or paste code/error in the input field.
3. Click an action:
   - **Ask**: General Q&A (Best for "Why?" or "How to?" questions).
   - **Explain Error**: Diagnoses errors and suggests fixes.
   - **Explain Code**: Explains selected scripts.
   - **Generate**: Writes new scripts based on your request.
4. **Select & Copy** the text you need from the response.

---

## Features
| Feature | Description |
| :--- | :--- |
| **Ask Button** | [NEW] Dedicated Q&A mode for general Unity questions. |
| **Local AI 2.0** | Optimized Mistral 7B with expert prompting & repetition penalties. |
| **Select & Copy** | Response text is now fully selectable for partial copying. |
| **Local + Cloud AI** | Choose between offline local inference or cloud APIs (Gemini, OpenAI, Claude) |
| **100% Private (Local)** | Local mode - all processing happens on your machine. No data leaves. |
| **Unity Native UI** | Built with UI Toolkit, matches Unity Editor styling. |
| **C# Code Only** | AI generates only C#/Unity code, never Python or other languages. |
| **Clean Output** | Response shows only AI content - ready to copy. |
| **Configurable** | Adjust context size. Gemini 2.5 Flash Lite supported. |

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
│   │   └── ...
│   ├── UI/
│   │   ├── SettingsView.cs          # Settings + provider selection
│   │   ├── ActionBarView.cs         # Provider-aware actions
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
