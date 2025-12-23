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
| **Tabbed Interface** | Horizontal tabs: Chat, Search, Analyze, Actions, Refactor, Settings |
| **Actions Tab** | [NEW] Execute commands, quick actions, templates |
| **Code Refactoring** | Rename symbols, find references, call hierarchy |
| **Project Search** | Semantic code search - ask questions about your codebase |
| **Scene Analyzer** | One-click scene performance and hygiene analysis |
| **Local + Cloud AI** | Offline local inference or cloud APIs (Gemini, OpenAI, Claude) |
| **100% Private (Local)** | Local mode - all processing on your machine |

<img src="https://github.com/user-attachments/assets/d1c9c2dd-243d-4c36-8945-c15b665bd063" width="180" />
<img src="https://github.com/user-attachments/assets/2c158764-e918-43fb-a404-a79862a41028" width="180" />
<img src="https://github.com/user-attachments/assets/9d6d4cf6-224c-4a57-83da-c705b371e962" width="180" />
<img src="https://github.com/user-attachments/assets/fb74575e-3caa-4976-a115-ad0cc08292d6" width="180" />
<img src="https://github.com/user-attachments/assets/e067bbb8-d08b-4176-9793-41a9eda9d359" width="180" />

---

## Quick Start

### 1. Install Native Libraries
> **Tools → Local AI → Install Native Libraries**

### 2. Open the Assistant
> **Tools → Local AI Assistant**

### 3. Choose Your AI Provider
- **Local (Offline)**: Download the 4GB model in Settings tab
- **Cloud**: Enter API key for Gemini, OpenAI, or Claude

---

## Tabs Overview

| Tab | Purpose |
| :--- | :--- |
| **Chat** | Ask questions, explain code, generate scripts |
| **Search** | Semantic search through your codebase |
| **Analyze** | Scene performance and hygiene report |
| **Actions** | Execute commands directly in scene (Experimental) |
| **Refactor** | Code navigation and refactoring tools |
| **Settings** | Configure AI provider and preferences |

---

## Actions Tab (Experimental)

Execute commands directly in your Unity scene.

### Features
- **Command Input**: Type commands like "Create a Cube at (0, 5, 0)"
- **Quick Actions**: One-click buttons for primitives, components, materials
- **Templates**: Pre-built setups (FPS Player, Physics Cube, Enemy, etc.)
- **Smart Suggestions**: Context-aware actions based on selection

### Templates
| Template | What it creates |
|----------|----------------|
| FPS Player | Capsule + CharacterController |
| Third Person Player | Capsule + Rigidbody + Collider |
| Physics Cube | Cube + Rigidbody |
| Bouncy Ball | Sphere + Rigidbody |
| Point Light | Empty + Light component |
| Basic Enemy | Capsule + Rigidbody + Collider |
| Trigger Zone | Cube + BoxCollider |
| Audio Source | Empty + AudioSource |

---

## Refactor Tab

Navigate and refactor your codebase with safety checks.

### Features
- **Symbol Search**: Find classes, methods, fields
- **Go to Definition**: Jump to symbol declaration
- **Find References**: List all usages across project
- **Call Hierarchy**: See callers and callees
- **Rename**: Rename with all reference updates and preview

### Safety System
| Risk Level | Meaning |
|------------|---------|
| Low | Safe local change |
| Medium | Affects public API or serialized data |
| High | Unity magic methods (blocked) |

---

## Settings

| Setting | Options | Description |
| :--- | :--- | :--- |
| **AI Provider** | Local, Gemini, OpenAI, Claude | Choose inference engine |
| **API Key** | (hidden for Local) | Cloud provider API key |
| **Context Size** | 2K - 32K | Larger = more code analyzed |
| **Max Response** | 512 - 8192 | Maximum tokens generated |

---

## Project Structure

```
Assets/LocalAI/
├── Editor/
│   ├── Services/
│   │   ├── ActionExecutor.cs          # Scene action execution
│   │   ├── CommandParser.cs           # Natural language parsing
│   │   ├── CommandTemplates.cs        # Pre-built templates
│   │   ├── ScriptApplicator.cs        # Apply AI-generated code
│   │   ├── SemanticSearch/            # Project Search
│   │   └── Refactoring/               # Code Navigation
│   └── UI/
│       ├── LocalAIEditorWindow.cs
│       ├── ActionsView.cs             # Actions tab
│       ├── RefactorView.cs            # Refactor tab
│       └── TabSystem.cs
└── Plugins/
    └── (libllama.dylib / llama.dll)
```

---

## Requirements

- **Unity 2021.3** or later
- **RAM**: 8GB minimum, 16GB recommended
- **Disk**: ~5GB (model + libraries)

---

## License

MIT — Use it, modify it, ship it.

---

<p align="center">
  <i>Built with C#, and way too much coffee.</i> ☕
</p>
