# Local AI Assistant for Unity

![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-000000?style=flat-square&logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/Status-Working-brightgreen?style=flat-square)

> **Privacy-focused, offline AI coding assistant running directly inside Unity Editor.**

---

## Features

| Feature | Description |
| :--- | :--- |
| **100% Private** | All processing happens locally. No data leaves your machine. |
| **Free Forever** | Uses your hardware (CPU / Metal GPU). No API costs. |
| **Unity Native UI** | Built with UI Toolkit, matches Unity Editor styling. |
| **Context Aware** | Reads selected code, console errors, or manual input. |
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
2. Select code in your editor, or enable **"Use Manual Input"** to paste code/errors
3. Click **Explain Error**, **Explain Code**, or **Generate**
4. Watch the AI response stream in real-time

---

## Settings

Click **Settings** in the toolbar to configure:

| Setting | Options | Description |
| :--- | :--- | :--- |
| **Context Size** | 1K, 2K, 4K, 8K | Larger = more code analyzed, more RAM |
| **Max Response** | 128, 256, 512, 1024 | Maximum tokens the AI will generate |

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
│   │   ├── ModelManager.cs          # Model state & download
│   │   ├── ModelDownloadService.cs  # HTTP resume support
│   │   ├── InferenceService.cs      # Token generation loop
│   │   ├── ContextCollector.cs      # Editor context gathering
│   │   └── LocalAISettings.cs       # User preferences
│   ├── UI/
│   │   ├── LocalAIEditorWindow.cs   # Main window
│   │   ├── SettingsView.cs          # Settings panel
│   │   ├── ContextView.cs           # Context & manual input
│   │   ├── ResponseView.cs          # AI response display
│   │   └── ActionBarView.cs         # Action buttons
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
  <i>Built with ❤️, C#, and way too much coffee.</i> ☕
</p>
