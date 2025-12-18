using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LocalAI.Runtime.Native;
using UnityEngine;

namespace LocalAI.Editor.Services
{
    public class InferenceService
    {
        private bool _isGenerating = false;
        private IntPtr _model = IntPtr.Zero;
        private IntPtr _ctx = IntPtr.Zero;
        private IntPtr _vocab = IntPtr.Zero;
        private int _vocabSize = 0;
        private int _eosToken = -1;
        
        // Cached settings (must be read on main thread)
        private uint _cachedContextSize;
        private int _cachedMaxTokens;

        public async Task StartInferenceAsync(string prompt, string modelPath, IProgress<string> progress, CancellationToken token)
        {
            if (_isGenerating)
            {
                progress?.Report("[Warning] Inference already in progress.\n");
                return;
            }
            
            _isGenerating = true;
            
            // Cache settings on main thread BEFORE starting background task
            _cachedContextSize = LocalAISettings.ContextSize;
            _cachedMaxTokens = LocalAISettings.MaxTokens;

            await Task.Run(() =>
            {
                try
                {
                    // 1. Initialize Model & Context
                    if (!Initialize(modelPath))
                    {
                        progress?.Report("[Error] Failed to initialize model.\n");
                        return;
                    }
                    
                    // 1.5 Recreate context for fresh inference (frees old KV cache)
                    if (!RecreateContext())
                    {
                        progress?.Report("[Error] Failed to recreate context.\n");
                        return;
                    }

                    // 2. Tokenize Prompt
                    int[] tokens = Tokenize(prompt);
                    if (tokens == null || tokens.Length == 0)
                    {
                        progress?.Report("[Error] Tokenization failed.\n");
                        return;
                    }

                    // Tokenization successful - start generation silently

                    // 3. Prefill (Prompt Processing)
                    int[] positions = new int[tokens.Length];
                    bool[] logits = new bool[tokens.Length];
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        positions[i] = i;
                        logits[i] = (i == tokens.Length - 1); // Only compute logits for last token
                    }

                    LLMNativeBridge.NativeLlamaBatch batch = LLMNativeBridge.CreateBatch(tokens, positions, logits);
                    int decodeResult = LLMNativeBridge.llama_decode(_ctx, batch);
                    LLMNativeBridge.FreeBatch(batch);

                    if (decodeResult != 0)
                    {
                        progress?.Report($"[Error] llama_decode failed with code {decodeResult}\n");
                        return;
                    }

                    int nPast = tokens.Length;

                    // 4. Generation Loop
                    int maxTokens = _cachedMaxTokens;
                    for (int i = 0; i < maxTokens; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            progress?.Report("\n[Cancelled]");
                            break;
                        }

                        // Sample: Greedy (argmax)
                        float[] logitsArray = LLMNativeBridge.GetLogitsSafe(_ctx, _vocabSize);
                        if (logitsArray == null)
                        {
                            progress?.Report("\n[Error] Failed to get logits.");
                            break;
                        }

                        int nextToken = 0;
                        float maxVal = float.MinValue;
                        for (int v = 0; v < _vocabSize; v++)
                        {
                            if (logitsArray[v] > maxVal)
                            {
                                maxVal = logitsArray[v];
                                nextToken = v;
                            }
                        }

                        // Check EOS
                        if (nextToken == _eosToken)
                        {
                            // End of sequence - stop silently
                            break;
                        }

                        // Debug Output
                        Debug.Log($"[LocalAI] Token: {nextToken}, MaxLogit: {maxVal}, Piece: '{TokenToPiece(nextToken)}'");

                        // Detokenize & Report
                        string piece = TokenToPiece(nextToken);
                        progress?.Report(piece);

                        // Decode Next Token
                        int[] nextTokens = new int[] { nextToken };
                        int[] nextPositions = new int[] { nPast };
                        bool[] nextLogits = new bool[] { true };

                        LLMNativeBridge.NativeLlamaBatch nextBatch = LLMNativeBridge.CreateBatch(nextTokens, nextPositions, nextLogits);
                        int result = LLMNativeBridge.llama_decode(_ctx, nextBatch);
                        LLMNativeBridge.FreeBatch(nextBatch);

                        if (result != 0)
                        {
                            progress?.Report($"\n[Error] Decode failed at token {i}");
                            break;
                        }

                        nPast++;
                    }
                }
                catch (DllNotFoundException ex)
                {
                    progress?.Report($"\n[Error] Native library not found. Run 'Tools > Local AI > Install Native Libraries' first.\n{ex.Message}");
                }
                catch (Exception ex)
                {
                    progress?.Report($"\n[Exception] {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _isGenerating = false;
                }
            }, token);
        }

        private bool Initialize(string modelPath)
        {
            if (_model != IntPtr.Zero) return true; // Already initialized

            try
            {
                Debug.Log("[LocalAI] Initializing llama backend...");
                LLMNativeBridge.llama_backend_init();

                Debug.Log($"[LocalAI] Loading model from: {modelPath}");
                _model = LLMNativeBridge.LoadModelSafe(modelPath);
                
                if (_model == IntPtr.Zero)
                {
                    Debug.LogError("[LocalAI] Failed to load model.");
                    return false;
                }

                _vocab = LLMNativeBridge.llama_model_get_vocab(_model);
                _vocabSize = LLMNativeBridge.llama_vocab_n_tokens(_vocab);
                _eosToken = LLMNativeBridge.llama_vocab_eos(_vocab);

                Debug.Log($"[LocalAI] Vocab size: {_vocabSize}, EOS token: {_eosToken}");

                int threads = Math.Max(1, Environment.ProcessorCount / 2);
                Debug.Log($"[LocalAI] Creating context with {threads} threads...");
                _ctx = LLMNativeBridge.CreateContextSafe(_model, _cachedContextSize, threads);

                if (_ctx == IntPtr.Zero)
                {
                    Debug.LogError("[LocalAI] Failed to create context.");
                    return false;
                }

                Debug.Log("[LocalAI] Model loaded successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] Initialize error: {ex.Message}");
                return false;
            }
        }
        
        private bool RecreateContext()
        {
            try
            {
                // Free old context if exists
                if (_ctx != IntPtr.Zero)
                {
                    LLMNativeBridge.llama_free(_ctx);
                    _ctx = IntPtr.Zero;
                }
                
                // Create fresh context
                int threads = Math.Max(1, Environment.ProcessorCount / 2);
                _ctx = LLMNativeBridge.CreateContextSafe(_model, _cachedContextSize, threads);
                
                return _ctx != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAI] RecreateContext error: {ex.Message}");
                return false;
            }
        }

        private int[] Tokenize(string text)
        {
            if (_vocab == IntPtr.Zero) return null;

            int maxTokens = text.Length + 128;
            int[] buffer = new int[maxTokens];

            int n = LLMNativeBridge.llama_tokenize(_vocab, text, text.Length, buffer, maxTokens, true, false);

            if (n < 0)
            {
                // Need more space
                maxTokens = -n + 16;
                buffer = new int[maxTokens];
                n = LLMNativeBridge.llama_tokenize(_vocab, text, text.Length, buffer, maxTokens, true, false);
            }

            if (n <= 0) return null;

            int[] result = new int[n];
            Array.Copy(buffer, result, n);
            return result;
        }

        private string TokenToPiece(int token)
        {
            if (_vocab == IntPtr.Zero) return "";

            byte[] buf = new byte[128];
            int n = LLMNativeBridge.llama_token_to_piece(_vocab, token, buf, buf.Length, 0, false);

            if (n <= 0) return "";

            return System.Text.Encoding.UTF8.GetString(buf, 0, n);
        }

        public void Dispose()
        {
            if (_ctx != IntPtr.Zero)
            {
                LLMNativeBridge.llama_free(_ctx);
                _ctx = IntPtr.Zero;
            }
            
            if (_model != IntPtr.Zero)
            {
                LLMNativeBridge.llama_free_model(_model);
                _model = IntPtr.Zero;
            }

            LLMNativeBridge.llama_backend_free();
        }
    }
}
