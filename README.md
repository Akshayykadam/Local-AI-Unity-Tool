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

## Chat Tab

Your AI coding assistant for Unity development.

### Features
- **Ask Questions**: Get answers about Unity APIs, C# patterns, best practices
- **Explain Code**: Select any script and ask AI to explain what it does
- **Generate Scripts**: Describe what you need, AI generates complete C# scripts
- **Fix Errors**: Paste console errors, AI suggests fixes
- **Context Aware**: AI sees your selected scripts, GameObjects, and scene hierarchy

### Actions
| Button | What it does |
|--------|--------------|
| Ask | Send a custom question with selected context |
| Explain | Explain the selected code |
| Review | Code review with suggestions |
| Optimize | Performance optimization suggestions |
| Document | Generate XML documentation comments |
| Fix Errors | Analyze console errors and suggest fixes |

### Response Panel
- **Copy**: Copy AI response to clipboard
- **Apply**: Save AI-generated code directly to a script file
- **Clear**: Clear the response

---

## Search Tab

Semantic search across your entire codebase.

### Features
- **Natural Language Queries**: Ask "Where is player health managed?" instead of searching for keywords
- **Project-Wide Index**: Indexes all C# scripts in your project
- **Ranked Results**: Most relevant results shown first
- **Click to Open**: Jump directly to the file and line

### How to Use
1. Click **Build Index** to scan your project scripts
2. Type your question in the search box
3. Click results to open files in your IDE

---

## Analyze Tab

One-click scene performance and hygiene analysis.

### What it Checks
- **Performance Issues**: Large meshes, too many lights, expensive shaders
- **Missing References**: Null references in components
- **Inactive Objects**: Hidden objects that might be forgotten
- **Best Practices**: Naming conventions, layer usage, tag consistency

### Output
Generates a detailed report with:
- Issue severity (Warning, Error, Info)
- Affected GameObject names
- Suggested fixes

---

## Actions Tab (Experimental)

Execute commands directly in your Unity scene without writing code.

### Command Input
Type natural language commands:
- "Create a Cube at (0, 5, 0)"
- "Add Rigidbody to selected"
- "Create red material"

### Quick Actions
One-click buttons for common operations:

| Category | Actions |
|----------|---------|
| **Primitives** | Cube, Sphere, Capsule, Cylinder, Plane |
| **Components** | + Rigidbody, + BoxCollider, + Light, + AudioSource |
| **Materials** | Red, Green, Blue, Yellow |

### Templates
Pre-built setups that create multiple objects at once:

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

### Smart Suggestions
Context-aware action buttons that change based on your selection:
- No selection → Create Cube, Sphere, Empty
- Object with Renderer → Add Rigidbody, Add Collider, Apply Material
- Light selected → Adjust intensity, change type

---

## Refactor Tab

Navigate and refactor your codebase with safety checks.

### Navigation Features
| Feature | Description |
|---------|-------------|
| **Symbol Search** | Find classes, methods, fields, properties by name |
| **Go to Definition** | Jump to where a symbol is declared |
| **Find References** | List all usages of a symbol across the project |
| **Call Hierarchy** | See what methods call this method, and what it calls |

### Refactoring Features
| Feature | Description |
|---------|-------------|
| **Rename** | Rename symbols with all references updated |
| **Preview** | Side-by-side diff before applying changes |

### Safety System
Protects against dangerous refactorings:

| Risk Level | Meaning | Examples |
|------------|---------|----------|
| Low | Safe local change | Private methods, local variables |
| Medium | Review recommended | Public APIs, SerializeField |
| High | Blocked | Start(), Update(), OnCollisionEnter() |

### How to Use
1. Click **Build Index** to scan your project
2. Search for a symbol name
3. Click a result to select it
4. Use navigation buttons (Go to Definition, Find References, etc.)
5. For renaming: enter new name → Preview → Apply

---

## Settings Tab

Configure AI provider and generation parameters.

### AI Provider
| Provider | Description |
|----------|-------------|
| **Local** | Offline inference using llama.cpp (requires model download) |
| **Gemini** | Google's Gemini API (requires API key) |
| **OpenAI** | OpenAI GPT API (requires API key) |
| **Claude** | Anthropic Claude API (requires API key) |

### Parameters
| Setting | Range | Description |
|---------|-------|-------------|
| **Context Size** | 2K - 32K | How much code context AI can see |
| **Max Response** | 512 - 8192 | Maximum tokens in AI response |

### Local Model
- Click **Download Model** to get the 4GB AI model
- Progress bar shows download status
- Once downloaded, works completely offline

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
