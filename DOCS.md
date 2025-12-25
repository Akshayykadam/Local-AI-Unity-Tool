# Local AI Unity Tool - Technical Documentation

This document provides detailed technical documentation for developers who want to understand, modify, or extend the Local AI Unity Tool.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Core Components](#core-components)
3. [Native Interop (P/Invoke)](#native-interop-pinvoke)
4. [Inference Pipeline](#inference-pipeline)
5. [UI System](#ui-system)
6. [Settings & Configuration](#settings--configuration)
7. [Extending the Tool](#extending-the-tool)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Unity Editor                                 │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                  LocalAIEditorWindow                      │   │
│  │  ┌─────────┐ ┌──────────┐ ┌────────────┐ ┌────────────┐  │   │
│  │  │ Header  │ │ Settings │ │  Response  │ │  Context   │  │   │
│  │  │  View   │ │   View   │ │    View    │ │    View    │  │   │
│  │  └─────────┘ └──────────┘ └────────────┘ └────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                     Services Layer                        │   │
│  │  ┌───────────────┐ ┌─────────────────┐ ┌──────────────┐  │   │
│  │  │ ModelManager  │ │ LocalInference  │ │ CloudServices│  │   │
│  │  └───────────────┘ └─────────────────┘ └──────────────┘  │   │
│  │                     (Gemini/GPT/Claude)                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│            (Local Only)      ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                   Runtime Layer                           │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │                 LLMNativeBridge                     │  │   │
│  │  │   P/Invoke bindings to llama.cpp native library     │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
└──────────────────────────────┼───────────────────────────────────┘
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Native Libraries                              │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐    │
│  │  libllama.dylib │ │ libggml.0.dylib │ │libggml-metal... │    │
│  │    (macOS)      │ │   (macOS)       │ │   (macOS GPU)   │    │
│  ├─────────────────┤ ├─────────────────┤ └─────────────────┘    │
│  │   llama.dll     │ │   ggml.dll      │                        │
│  │   (Windows)     │ │   (Windows)     │                        │
│  └─────────────────┘ └─────────────────┘                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. LLMNativeBridge.cs

**Location:** `Assets/LocalAI/Runtime/Native/LLMNativeBridge.cs`

**Purpose:** Provides safe P/Invoke bindings to the llama.cpp native library.

**Key Functions:**

```csharp
// Initialize the llama backend (call once at startup)
public static extern void llama_backend_init();

// Load a GGUF model file
public static IntPtr LoadModelSafe(string path);

// Create an inference context
public static IntPtr CreateContextSafe(IntPtr model, uint n_ctx, int n_threads);

// Tokenize text into token IDs
public static extern int llama_tokenize(IntPtr vocab, string text, int textLen, 
                                         int[] tokens, int nMaxTokens, bool addSpecial, bool parseSpecial);

// Decode a batch of tokens (forward pass)
public static extern int llama_decode(IntPtr ctx, NativeLlamaBatch batch);

// Sample the next token
public static extern int llama_sampler_sample(IntPtr smpl, IntPtr ctx, int idx);

// Convert token ID to text
public static extern int llama_token_to_piece(IntPtr vocab, int token, 
                                               StringBuilder buf, int length, int lstrip, bool special);
```

**Struct Definitions:**

```csharp
// Batch structure for processing multiple tokens
[StructLayout(LayoutKind.Sequential)]
public struct NativeLlamaBatch
{
    public int n_tokens;      // Number of tokens in batch
    public IntPtr token;      // Array of token IDs
    public IntPtr embd;       // Embeddings (null for token input)
    public IntPtr pos;        // Position array
    public IntPtr n_seq_id;   // Sequence ID counts
    public IntPtr seq_id;     // Sequence IDs
    public IntPtr logits;     // Which positions need logits
}

// Context parameters (256-byte fixed buffer for ABI safety)
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeLlamaContextParams
{
    public fixed byte data[256];
}
```

### 2. InferenceService.cs

**Location:** `Assets/LocalAI/Editor/Services/InferenceService.cs`

**Purpose:** Manages the complete inference lifecycle.

**Key Methods:**

```csharp
// Start async inference with progress reporting
public async Task StartInferenceAsync(
    string prompt,           // The formatted prompt
    string modelPath,        // Path to .gguf model file
    IProgress<string> progress,  // Callback for streaming output
    CancellationToken token  // For cancellation support
);

// [NEW] Cloud Services
// Implements IInferenceService for Gemini, OpenAI, Claude.
// Example: GeminiInferenceService uses Gemini 2.5 Flash Lite default.

// Initialize model and context (called once)
private bool Initialize(string modelPath);

// Recreate context for fresh inference
private bool RecreateContext();

// Convert text to tokens
private int[] Tokenize(string text);

// Convert tokens back to text
private string Detokenize(int token);
```

**Inference Loop:**

```csharp
// Simplified inference loop
for (int i = 0; i < maxTokens; i++)
{
    // [NEW] Repetition Penalty (1.1 over 64 token window)
    ApplyRepetitionPenalty(logits, history);

    // Sample next token (Greedy)
    int newToken = ArgMax(logits);
    
    // Check for end of sequence
    if (newToken == eosToken) break;
    
    // Convert to text and report
    string piece = Detokenize(newToken);
    progress?.Report(piece);
    
    // Decode single token for next iteration
    LLMNativeBridge.llama_decode(ctx, singleTokenBatch);
}
```

### 3. ModelManager.cs

**Location:** `Assets/LocalAI/Editor/Services/ModelManager.cs`

**Purpose:** Manages model state and download status.

**States:**

```csharp
public enum ModelState
{
    NotInstalled,    // Model not downloaded
    Downloading,     // Download in progress
    Ready,           // Model ready for inference
    Error            // Error state
}
```

### 4. ContextCollector.cs

**Location:** `Assets/LocalAI/Editor/Services/ContextCollector.cs`

**Purpose:** Gathers context from Unity Editor for AI prompts.

**Collects:**
- Currently selected code in editor
- Console log entries (errors, warnings)
- Selected GameObject information
- Active scene context

---

## Native Interop (P/Invoke)

### Platform Detection

```csharp
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    private const string LibraryName = "libllama";
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private const string LibraryName = "llama";
#endif
```

### Safe Parameter Passing

The native library uses complex structs. We handle this safely:

```csharp
// Model parameters - passed via IntPtr to avoid ABI issues
public static IntPtr LoadModelSafe(string path)
{
    IntPtr paramsPtr = Marshal.AllocHGlobal(structSize);
    try
    {
        // Zero memory for safe defaults
        for (int i = 0; i < structSize; i++)
            Marshal.WriteByte(paramsPtr, i, 0);
        
        // Set critical parameters
        Marshal.WriteInt32(paramsPtr, 16, 99); // n_gpu_layers
        
        return llama_load_model_from_file_raw(path, paramsPtr);
    }
    finally
    {
        Marshal.FreeHGlobal(paramsPtr);
    }
}
```

### Context Parameters Layout

```csharp
// Manual struct field layout (ARM64/x64)
// Based on llama.cpp llama_context_params
unsafe
{
    fixed (byte* ptr = lparams.data)
    {
        // Offset 0: n_ctx (uint32)
        WriteUInt32(ptr, 0, n_ctx);
        
        // Offset 4: n_batch (uint32)
        WriteUInt32(ptr, 4, 2048);
        
        // Offset 8: n_ubatch (uint32)
        WriteUInt32(ptr, 8, 512);
        
        // Offset 12: n_seq_max (uint32)
        WriteUInt32(ptr, 12, 1);
        
        // Offset 16: n_threads (int32)
        WriteInt32(ptr, 16, n_threads);
        
        // ... additional fields
    }
}
```

---

## Inference Pipeline

### Complete Flow

```
1. User clicks "Explain Code"
        │
        ▼
2. ActionBarView.OnExplainCode()
   - Collects context from ContextView
   - Formats prompt with system message
        │
        ▼
3. InferenceService.StartInferenceAsync()
   - Runs on background thread (Task.Run)
        │
        ▼
4. Initialize() - First time only
   - llama_backend_init()
   - LoadModelSafe() → llama_load_model_from_file
   - CreateContextSafe() → llama_new_context_with_model
        │
        ▼
5. RecreateContext() - Every query
   - llama_free() old context
   - CreateContextSafe() new context
        │
        ▼
6. Tokenize()
   - llama_tokenize() → int[] tokens
        │
        ▼
7. Prefill (Prompt Processing)
   - CreateBatch() with all prompt tokens
   - llama_decode() → processes entire prompt
        │
        ▼
8. Generation Loop
   ┌─────────────────────────────────────┐
   │ for (i = 0; i < maxTokens; i++)    │
   │   - llama_sampler_sample() → token │
   │   - Check for EOS token            │
   │   - Detokenize() → text            │
   │   - progress.Report(text)          │
   │   - llama_decode() single token    │
   └─────────────────────────────────────┘
        │
        ▼
9. Cleanup
   - FreeBatch()
   - FreeSampler()
   - _isGenerating = false
```

### Prompt Format (Dynamic)

The tool selects a system prompt based on user intent:

**1. Error Diagnosis (Rigid):**
```
[INST] You are a specific, deterministic Unity 2021.3+ C# expert. Fix errors and explain why.
RULES:
1. RESPONSE FORMAT: Diagnosis -> Fix -> Code Block.
...
TASK: Explain the following error context:
CONTEXT: {context} [/INST]
```

**2. Q&A / Ask (Flexible):**
```
[INST] You are a Unity C# Expert. Answer the user's question clearly.
...
TASK: Question: {user_input} [/INST]
```

---

## UI System

### UI Toolkit Structure

**UXML Layout:** `Assets/LocalAI/Editor/UI/Resources/LocalAIWindow.uxml`

```xml
<ui:VisualElement name="root">
    <!-- Header with status and settings -->
    <ui:VisualElement name="header-container" />
    
    <!-- Settings panel (hidden by default) -->
    <ui:VisualElement name="settings-container" />
    
    <!-- Main content -->
    <ui:VisualElement name="content-container">
        <!-- Response area -->
        <ui:VisualElement name="response-container" />
        
        <!-- Action buttons -->
        <ui:VisualElement name="action-bar-container" />
        
        <!-- Context panel -->
        <ui:VisualElement name="context-container" />
    </ui:VisualElement>
</ui:VisualElement>
```

**USS Styling:** `Assets/LocalAI/Editor/UI/Resources/LocalAIStyles.uss`

Uses Unity Editor native color palette:
- Background: `#383838`
- Panel: `#3c3c3c`
- Border: `#232323`
- Text: `#c4c4c4`

### View Classes

| Class | Purpose |
|-------|---------|
| `LocalAIEditorWindow` | Main EditorWindow, coordinates all views |
| `HeaderView` | Status indicator, model name, settings button |
| `SettingsView` | Model path, AI settings (context size, max tokens) |
| `ResponseView` | Displays AI response, copy/clear buttons |
| `ContextView` | Shows collected context, Manual Input (TextField) |
| `ActionBarView` | Ask, Explain Error, Explain Code, Generate buttons |

---

## Settings & Configuration

### LocalAISettings.cs

**Location:** `Assets/LocalAI/Editor/Services/LocalAISettings.cs`

```csharp
public static class LocalAISettings
{
    // Persisted via EditorPrefs
    public static uint ContextSize { get; set; }  // 1024-8192
    public static int MaxTokens { get; set; }     // 128-1024
    
    // Available options
    public static readonly uint[] ContextSizeOptions = { 1024, 2048, 4096, 8192 };
    public static readonly int[] MaxTokensOptions = { 128, 256, 512, 1024 };
}
```

### Threading Considerations

EditorPrefs can only be accessed from the main thread. Settings are cached before starting background inference:

```csharp
// Main thread - cache settings
_cachedContextSize = LocalAISettings.ContextSize;
_cachedMaxTokens = LocalAISettings.MaxTokens;

// Background thread - use cached values
await Task.Run(() => {
    CreateContextSafe(_model, _cachedContextSize, threads);
    // ...
});
```

---

## Extending the Tool

### Adding a New Action Button

1. **Add button to UXML:**
```xml
<ui:Button name="btn-refactor" text="Refactor" class="action-btn" />
```

2. **Hook up in ActionBarView.cs:**
```csharp
var refactorBtn = root.Q<Button>("btn-refactor");
refactorBtn.clicked += OnRefactor;

private void OnRefactor()
{
    string prompt = $"Refactor this code:\n{_contextView.GetContext()}";
    StartInference("refactor", prompt);
}
```

### Adding New Sampler Parameters

1. **Add to LocalAISettings.cs:**
```csharp
public static float Temperature { get; set; } = 0.7f;
```

2. **Add to SettingsView.cs:**
```csharp
var tempSlider = new Slider("Temperature", 0.0f, 2.0f);
tempSlider.value = LocalAISettings.Temperature;
tempSlider.RegisterValueChangedCallback(evt => {
    LocalAISettings.Temperature = evt.newValue;
});
```

3. **Use in InferenceService.cs:**
```csharp
// When creating sampler chain
LLMNativeBridge.llama_sampler_chain_add(chain, 
    LLMNativeBridge.llama_sampler_init_temp(_cachedTemperature));
```

### Supporting Additional Models

1. **Update ModelDownloadService.cs with new model URL**
2. **Adjust prompt format in ActionBarView.cs for model's expected format**
3. **Test context size compatibility**

---

## Error Handling

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `Failed to load model` | Invalid path or corrupted file | Re-download model |
| `Failed to create context` | Insufficient RAM | Reduce context size |
| `llama_decode failed` | Context full | Reduce prompt size or increase context |
| `Tokenization failed` | Invalid vocab | Check model file integrity |

### Debug Logging

Enable detailed logging by checking Debug.Log statements:

```csharp
Debug.Log($"[LocalAI] Loading model from: {modelPath}");
Debug.Log($"[LocalAI] Vocab size: {_vocabSize}, EOS token: {_eosToken}");
Debug.Log($"[LocalAI] Creating context with {threads} threads...");
```

---

## Performance Considerations

| Factor | Impact | Recommendation |
|--------|--------|----------------|
| Context Size | Memory usage | Use 4K default, 8K max |
| Model Load | 10-30 second delay | Load once, reuse |
| Context Recreation | ~100ms per query | Acceptable overhead |
| Token Generation | ~50-100ms per token | CPU-bound on most systems |
| GPU Acceleration | 5-10x faster | Enable Metal/CUDA if available |

---

## Semantic Search (Project Search)

The Semantic Search feature allows developers to search their codebase using natural language queries.

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ProjectSearchView                     │
│  [Search Input] [Re-Index] [Clear]                      │
│  [Results List] [AI Summary]                            │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                     SemanticIndex                        │
│  Coordinates: Scanner → Parser → Chunker → Embedder     │
└─────────────────────────────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
    ProjectScanner  CSharpParser    EmbeddingService
          │               │               │
          ▼               ▼               ▼
    [File List]    [Code Chunks]    [Vectors]
                          │               │
                          └───────┬───────┘
                                  ▼
                            VectorStore
                         (Binary Storage)
```

### Key Components

| Component | Purpose |
|-----------|---------|
| `ProjectScanner` | Scans folders for .cs files, computes MD5 hashes |
| `CSharpParser` | Extracts classes, methods, properties via regex |
| `CodeChunker` | Splits large code blocks (max 512 tokens) |
| `EmbeddingService` | Generates vectors via TF-IDF + code heuristics |
| `VectorStore` | Binary file storage with cosine similarity search |
| `SemanticIndex` | Orchestrates indexing pipeline |
| `RAGService` | Combines search results with LLM reasoning |

### Storage

Index data is stored in `Library/LocalAI/SemanticIndex/`:
- `vectors.bin` - Binary encoded embeddings
- `index_cache.json` - File hashes for incremental updates

### Query Flow

1. User enters natural language query
2. Query is embedded using `EmbeddingService`
3. `VectorStore.Search()` finds top-K similar chunks
4. `RAGService` builds prompt with retrieved context
5. LLM generates summary/answer
6. Results displayed with click-to-open functionality

### Settings

| Setting | Key | Default |
|---------|-----|---------|
| Max Files | `LocalAI_MaxIndexedFiles` | 5000 |
| Max Chunk Size | `LocalAI_MaxChunkSize` | 512 tokens |
| Auto Re-index | `LocalAI_AutoReindexOnChange` | false |
| Indexed Folders | `LocalAI_IndexedFolders` | "Assets/" |

---

## RAG Pipeline (NEW)

The Retrieval-Augmented Generation pipeline enhances both Project Search and Chat with advanced retrieval techniques.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     User Query                               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    QueryProcessor                            │
│  • Query Expansion ("movement" → "rigidbody velocity")      │
│  • Intent Classification (FindClass, Debug, HowTo, etc.)    │
│  • Unity Keyword Injection                                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   HybridSearchService                        │
│  • 70% Semantic Search (vector similarity)                  │
│  • 30% Keyword Search (BM25-style matching)                 │
│  • Intent-aware filtering                                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    ResultReranker                            │
│  • Keyword density scoring                                  │
│  • Recency bonus (recently modified files)                  │
│  • Code structure match (class vs method vs property)       │
│  • Deduplication                                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      RAGService                              │
│  • Context building with signatures                         │
│  • Intent-specific prompts                                  │
│  • LLM inference                                            │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Location | Purpose |
|-----------|----------|----------|
| `QueryProcessor` | `SemanticSearch/QueryProcessor.cs` | Query expansion, intent classification |
| `HybridSearchService` | `SemanticSearch/HybridSearchService.cs` | Combined semantic + keyword search |
| `ResultReranker` | `SemanticSearch/ResultReranker.cs` | Multi-signal relevance scoring |
| `RAGService` | `SemanticSearch/RAGService.cs` | Context building, prompt construction |

### Enhanced CodeChunk Metadata

```csharp
public struct CodeChunk
{
    // Existing fields
    public string FilePath;
    public string Name;
    public string Type;       // "class", "method", "property"
    public string Content;
    public string Summary;
    
    // NEW: Enhanced metadata
    public string Signature;   // "public void Move(Vector3 dir)"
    public string ReturnType;  // "void", "bool", etc.
    public string Parameters;  // Method parameters
}
```

### RAG Settings

| Setting | Key | Default | Description |
|---------|-----|---------|-------------|
| Enable RAG | `LocalAI_EnableRAG` | true | Add code context to Chat |
| Top K | `LocalAI_RAGTopK` | 3 | Chunks to retrieve |
| Min Relevance | `LocalAI_RAGMinRelevance` | 0.25 | Score threshold |

---

## License

MIT License - See LICENSE file for details.
