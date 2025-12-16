# 🧠 Local AI Assistant `(Unity Tool)`

![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-000000?style=flat-square&logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/Status-Working%20Prototype-brightgreen?style=flat-square)

> **"Privacy-focused, offline AI coding companion living directly inside your Unity Editor."**

---

## 📖 The Story

I built this tool because I wanted a smart assistant to help with my Unity projects—answering code questions, explaining exceptions, and brainstorming mechanics—**without** sending my proprietary code to the cloud or burning through API credits.

It runs a quantization-optimized mid-tier LLM (**Mistral 7B**) locally on your machine using `llama.cpp`. No internet required. No data leaks. Just you and your AI.

---

## ✨ Key Features

| Feature | Description |
| :--- | :--- |
| 🛡️ **100% Private** | Your project data never leaves `localhost`. |
| 💸 **Free Forever** | Runs on your hardware (CPU / Metal / CUDA). |
| 🧩 **Unity Native** | Built with **UI Toolkit** for a polished Editor look. |
| ⚡ **Context Aware** | Reads your selected GameObject or Console errors. |
| 🔒 **Safe Interop** | No `unsafe` code — uses managed marshalling for stability. |

---

## 🚀 Quick Start

### 1. Install Native Libraries
> **Tools → Local AI → Install Native Libraries**

This downloads the pre-built `llama.cpp` binaries (~50MB) from the official GitHub release and places them in `Assets/LocalAI/Plugins/`.

### 2. Download the AI Model
> **Tools → Local AI Assistant → Download Model**

Downloads **Mistral 7B Instruct (Q4_K_M)** (~4GB) to:
- macOS: `~/Library/Application Support/LocalAIUnity/models/`
- Windows: `%AppData%\LocalAIUnity\models\`

### 3. Start Using
1.  Select a **GameObject** in your scene.
2.  Open **Tools → Local AI Assistant**.
3.  Click **"Generate"** or **"Explain Code"**.
4.  Watch the AI stream its response in real-time!

---

## 🛠️ Tech Stack

| Component | Technology |
| :--- | :--- |
| **UI** | Unity UI Toolkit (UXML / USS) |
| **Backend** | C# with `Marshal`-based safe P/Invoke |
| **Inference** | `llama.cpp` (b7423 release) |
| **Model** | [Mistral-7B-Instruct-v0.1-GGUF](https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.1-GGUF) |

---

## � Project Structure

```
Assets/LocalAI/
├── Editor/
│   ├── Services/
│   │   ├── ModelManager.cs          # Model state & download
│   │   ├── ModelDownloadService.cs  # HTTP resume support
│   │   ├── InferenceService.cs      # Token generation loop
│   │   └── ContextCollector.cs      # Editor context gathering
│   ├── UI/
│   │   ├── LocalAIEditorWindow.cs   # Main window
│   │   ├── HeaderView.cs
│   │   ├── ContextView.cs
│   │   ├── ResponseView.cs
│   │   └── ActionBarView.cs
│   └── Setup/
│       └── NativeSetup.cs           # Binary installer
├── Runtime/
│   └── Native/
│       └── LLMNativeBridge.cs       # Safe P/Invoke bindings
└── Plugins/
    └── (libllama.dylib / llama.dll) # Downloaded automatically
```

---

## 🔮 Roadmap

- [ ] **Sampler Options**: Add temperature, top-k, top-p controls.
- [ ] **Chat History**: Persist context across sessions.
- [ ] **Real-time Log Monitoring**: Auto-detect console errors.
- [ ] **Agent Mode**: Let AI write files to your project (safely!).

---

## ⚠️ Known Limitations

- **First Load is Slow**: The 4GB model takes ~10-30 seconds to load into memory on first use.
- **Memory Usage**: Requires ~6GB RAM during inference.
- **Struct Compatibility**: The native bridge assumes `llama.cpp` release `b7423`. Other versions may have different struct layouts.

---

## 📜 License

MIT — Use it, modify it, ship it. Just don't blame me if the AI suggests deleting `System32`.

---

<p align="center">
  <i>Built with ❤️, C#, and way too much coffee.</i> ☕
</p>
