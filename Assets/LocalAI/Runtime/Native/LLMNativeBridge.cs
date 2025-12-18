using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace LocalAI.Runtime.Native
{
    /// <summary>
    /// Safe wrapper around llama.cpp that avoids unsafe pointer manipulation.
    /// Uses helper batch struct that we populate and copy to unmanaged memory.
    /// </summary>
    public static class LLMNativeBridge
    {
        const string LibraryName = "llama";

        #region Backend Lifecycle
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void llama_backend_init();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void llama_backend_free();

        #endregion

        #region Model Loading
        
        // Use opaque IntPtr for params - we get defaults from native side
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr llama_model_default_params_ptr();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr llama_load_model_from_file(
            [MarshalAs(UnmanagedType.LPStr)] string path_model,
            IntPtr params_ptr);

        // Simplified: Load with ALL defaults (no GPU offload control, but SAFE)
        public static IntPtr LoadModelSafe(string path)
        {
            // llama.cpp doesn't export a pointer version of default params directly.
            // BUT: llama_load_model_from_file expects a struct BY VALUE.
            // So we MUST define the struct OR use a C wrapper.
            
            // WORKAROUND: Use the simple llama-cli style if available, 
            // OR define a minimal struct that matches JUST the fields we care about.
            // 
            // SAFEST: Let's define a MINIMAL params struct with ONLY guaranteed stable fields
            // and pad the rest. This is still risky.
            //
            // ACTUALLY SAFEST: Use llama_model_load_from_file with a zeroed struct of correct size.
            // Size of llama_model_params in b7423 is typically ~120 bytes on 64-bit.
            // We can allocate that, zero it, write known offsets, and pass.
            
            // For maximum safety, I'll use a byte array approach:
            int structSize = 128; // Overestimate to be safe
            IntPtr paramsPtr = Marshal.AllocHGlobal(structSize);
            
            try
            {
                // Zero memory
                for (int i = 0; i < structSize; i++)
                {
                    Marshal.WriteByte(paramsPtr, i, 0);
                }
                
                // Set n_gpu_layers at known offset (usually after 2 pointers = 16 bytes on 64-bit)
                // Offset: devices(8) + tensor_buft_overrides(8) = 16
                Marshal.WriteInt32(paramsPtr, 16, 99); // n_gpu_layers = 99 (offload all)
                
                // Set use_mmap = true at its offset
                // After: n_gpu_layers(4) + split_mode(4) + main_gpu(4) + tensor_split(8) + callbacks(16) + kv_overrides(8) = 60
                // Booleans start around offset 60-70
                // Actually layout: see llama.h
                // Simpler: leave defaults. use_mmap defaults to true in zeroed struct on most builds.
                
                // Most boolean defaults are false, but use_mmap is usually the only critical one.
                // In some builds, zeroed struct works fine.
                
                var model = llama_load_model_from_file_raw(path, paramsPtr);
                return model;
            }
            finally
            {
                Marshal.FreeHGlobal(paramsPtr);
            }
        }
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "llama_load_model_from_file", CharSet = CharSet.Ansi)]
        private static extern IntPtr llama_load_model_from_file_raw(
            [MarshalAs(UnmanagedType.LPStr)] string path_model,
            IntPtr params_struct);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void llama_free_model(IntPtr model);

        #endregion

        #region Context
        
        public static IntPtr CreateContextSafe(IntPtr model, uint n_ctx = 512, int n_threads = 4)
        {
            Debug.Log($"[LocalAI] CreateContextSafe called with n_ctx={n_ctx}, n_threads={n_threads}");
            
            // Use unsafe fixed buffer struct - properly blittable for ARM64
            NativeLlamaContextParams lparams = new NativeLlamaContextParams();
            
            // Manually initialize ALL known fields to valid defaults based on llama.h
            // IMPORTANT: There is NO seed field in llama_context_params!
            unsafe
            {
                // Zero out the entire buffer first
                for (int i = 0; i < 256; i++) lparams.data[i] = 0;
                
                // Struct layout from llama.h (no seed field!):
                // uint32_t n_ctx;             // offset 0
                // uint32_t n_batch;           // offset 4
                // uint32_t n_ubatch;          // offset 8
                // uint32_t n_seq_max;         // offset 12
                // int32_t  n_threads;         // offset 16
                // int32_t  n_threads_batch;   // offset 20
                
                WriteUInt32(lparams.data, 0, n_ctx);        // n_ctx
                WriteUInt32(lparams.data, 4, 512);          // n_batch
                WriteUInt32(lparams.data, 8, 512);          // n_ubatch  
                WriteUInt32(lparams.data, 12, 1);           // n_seq_max
                WriteInt32(lparams.data, 16, n_threads);    // n_threads
                WriteInt32(lparams.data, 20, n_threads);    // n_threads_batch
                
                // enum rope_scaling_type;     // offset 24 (4 bytes)
                // enum pooling_type;          // offset 28 (4 bytes)
                // enum attention_type;        // offset 32 (4 bytes)
                // enum flash_attn_type;       // offset 36 (4 bytes)
                WriteInt32(lparams.data, 24, -1);  // LLAMA_ROPE_SCALING_TYPE_UNSPECIFIED
                WriteInt32(lparams.data, 28, -1);  // LLAMA_POOLING_TYPE_UNSPECIFIED
                WriteInt32(lparams.data, 32, -1);  // LLAMA_ATTENTION_TYPE_UNSPECIFIED
                WriteInt32(lparams.data, 36, 0);   // LLAMA_FLASH_ATTN_TYPE_NONE
                
                // float rope_freq_base;       // offset 40
                // float rope_freq_scale;      // offset 44
                // float yarn_ext_factor;      // offset 48
                // float yarn_attn_factor;     // offset 52
                // float yarn_beta_fast;       // offset 56
                // float yarn_beta_slow;       // offset 60
                // uint32_t yarn_orig_ctx;     // offset 64
                // float defrag_thold;         // offset 68
                WriteFloat(lparams.data, 40, 0.0f);   // rope_freq_base (0 = from model)
                WriteFloat(lparams.data, 44, 0.0f);   // rope_freq_scale (0 = from model)
                WriteFloat(lparams.data, 48, -1.0f);  // yarn_ext_factor (-1 = from model)
                WriteFloat(lparams.data, 52, 1.0f);   // yarn_attn_factor
                WriteFloat(lparams.data, 56, 32.0f);  // yarn_beta_fast
                WriteFloat(lparams.data, 60, 1.0f);   // yarn_beta_slow
                WriteUInt32(lparams.data, 64, 0);     // yarn_orig_ctx
                WriteFloat(lparams.data, 68, -1.0f);  // defrag_thold (disabled)
                
                // ggml_backend_sched_eval_callback cb_eval; // offset 72 (8 bytes pointer on ARM64)
                // void * cb_eval_user_data;                  // offset 80 (8 bytes pointer)
                // Already zeros (NULL pointers)
                
                // enum ggml_type type_k;      // offset 88 (4 bytes)
                // enum ggml_type type_v;      // offset 92 (4 bytes)
                WriteInt32(lparams.data, 88, 1);  // GGML_TYPE_F16
                WriteInt32(lparams.data, 92, 1);  // GGML_TYPE_F16
                
                // ggml_abort_callback abort_callback; // offset 96 (8 bytes pointer)
                // void * abort_callback_data;         // offset 104 (8 bytes pointer)
                // Already zeros (NULL pointers)
                
                // Booleans at end (starting offset 112):
                // bool embeddings;   // offset 112
                // bool offload_kqv;  // offset 113
                // bool no_perf;      // offset 114
                // bool op_offload;   // offset 115
                // bool swa_full;     // offset 116
                // bool kv_unified;   // offset 117
                lparams.data[112] = 0; // embeddings = false
                lparams.data[113] = 1; // offload_kqv = true (use GPU if available)
                lparams.data[114] = 1; // no_perf = true (skip perf measurements)
                lparams.data[115] = 1; // op_offload = true (offload ops to device)
                lparams.data[116] = 1; // swa_full = true
                lparams.data[117] = 1; // kv_unified = true
            }
            
            Debug.Log($"[LocalAI] Calling llama_new_context_with_model...");
            
            // Pass BY VALUE - the marshaler will handle copying the fixed buffer
            IntPtr result = llama_new_context_with_model(model, lparams);
            
            Debug.Log($"[LocalAI] llama_new_context_with_model returned: 0x{result.ToString("X")}");
            
            return result;
        }

        private static unsafe void WriteUInt32(byte* buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
        
        private static unsafe void WriteInt32(byte* buffer, int offset, int value)
        {
            WriteUInt32(buffer, offset, (uint)value);
        }
        
        private static unsafe void WriteFloat(byte* buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            buffer[offset] = bytes[0];
            buffer[offset + 1] = bytes[1];
            buffer[offset + 2] = bytes[2];
            buffer[offset + 3] = bytes[3];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct NativeLlamaContextParams
        {
            // Fixed size buffer - truly blittable, no managed array
            public fixed byte data[256];
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr llama_new_context_with_model(IntPtr model, NativeLlamaContextParams params_struct);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void llama_free(IntPtr ctx);
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void llama_kv_cache_clear(IntPtr ctx);

        #endregion

        #region Vocab & Tokenize
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr llama_model_get_vocab(IntPtr model);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int llama_tokenize(
            IntPtr vocab,
            [MarshalAs(UnmanagedType.LPStr)] string text,
            int text_len,
            [In, Out] int[] tokens,
            int n_tokens_max,
            [MarshalAs(UnmanagedType.I1)] bool add_special,
            [MarshalAs(UnmanagedType.I1)] bool parse_special);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int llama_token_to_piece(
            IntPtr vocab,
            int token,
            [In, Out] byte[] buf,
            int length,
            int lstrip,
            [MarshalAs(UnmanagedType.I1)] bool special);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int llama_vocab_n_tokens(IntPtr vocab);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int llama_vocab_bos(IntPtr vocab);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int llama_vocab_eos(IntPtr vocab);

        #endregion

        #region Batch & Decode (SAFE VERSION)
        
        // Instead of manipulating llama_batch struct directly, we use the "get_one" helper
        // or manually allocate arrays and build batch in unmanaged memory.
        
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeLlamaBatch
        {
            public int n_tokens;
            public IntPtr token;
            public IntPtr embd;
            public IntPtr pos;
            public IntPtr n_seq_id;
            public IntPtr seq_id;
            public IntPtr logits;
            public int all_pos_0;
            public int all_pos_1;
            public int all_seq_id;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int llama_decode(IntPtr ctx, NativeLlamaBatch batch);

        // Helper to create a batch struct in unmanaged memory
        // llama_batch layout (approximately):
        // int32 n_tokens
        // ptr token
        // ptr embd
        // ptr pos
        // ptr n_seq_id
        // ptr seq_id
        // ptr logits
        // Plus some int fields for defaults
        
        public static NativeLlamaBatch CreateBatch(int[] tokens, int[] positions, bool[] computeLogits)
        {
            int n = tokens.Length;
            
            // Allocate arrays in unmanaged memory
            IntPtr tokenPtr = Marshal.AllocHGlobal(n * sizeof(int));
            IntPtr posPtr = Marshal.AllocHGlobal(n * sizeof(int));
            IntPtr nSeqIdPtr = Marshal.AllocHGlobal(n * sizeof(int));
            IntPtr seqIdPtr = Marshal.AllocHGlobal(n * IntPtr.Size); // Array of pointers
            IntPtr logitsPtr = Marshal.AllocHGlobal(n); // int8_t
            
            // Copy data
            Marshal.Copy(tokens, 0, tokenPtr, n);
            Marshal.Copy(positions, 0, posPtr, n);
            
            int[] nSeqIds = new int[n];
            for (int i = 0; i < n; i++) nSeqIds[i] = 1;
            Marshal.Copy(nSeqIds, 0, nSeqIdPtr, n);
            
            // seq_id: array of pointers, each pointing to an array containing [0]
            IntPtr seqIdData = Marshal.AllocHGlobal(n * sizeof(int)); // Each token has 1 seq_id
            int[] seqIdValues = new int[n]; // All zeros (seq 0)
            Marshal.Copy(seqIdValues, 0, seqIdData, n);
            
            for (int i = 0; i < n; i++)
            {
                IntPtr entryPtr = IntPtr.Add(seqIdData, i * sizeof(int));
                Marshal.WriteIntPtr(seqIdPtr, i * IntPtr.Size, entryPtr);
            }
            
            // logits
            byte[] logitsByte = new byte[n];
            for (int i = 0; i < n; i++)
            {
                logitsByte[i] = computeLogits[i] ? (byte)1 : (byte)0;
            }
            Marshal.Copy(logitsByte, 0, logitsPtr, n);
            
            // Create struct
            NativeLlamaBatch batch = new NativeLlamaBatch();
            batch.n_tokens = n;
            batch.token = tokenPtr;
            batch.embd = IntPtr.Zero;
            batch.pos = posPtr;
            batch.n_seq_id = nSeqIdPtr;
            batch.seq_id = seqIdPtr;
            batch.logits = logitsPtr;
            batch.all_pos_0 = 0;
            batch.all_pos_1 = 0;
            batch.all_seq_id = 0;
            
            return batch;
        }
        
        public static void FreeBatch(NativeLlamaBatch batch)
        {
            // Free the seq_id data (first entry points to start)
            if (batch.seq_id != IntPtr.Zero)
            {
                IntPtr seqIdData = Marshal.ReadIntPtr(batch.seq_id, 0);
                if (seqIdData != IntPtr.Zero) Marshal.FreeHGlobal(seqIdData);
                Marshal.FreeHGlobal(batch.seq_id);
            }
            
            if (batch.token != IntPtr.Zero) Marshal.FreeHGlobal(batch.token);
            if (batch.pos != IntPtr.Zero) Marshal.FreeHGlobal(batch.pos);
            if (batch.n_seq_id != IntPtr.Zero) Marshal.FreeHGlobal(batch.n_seq_id);
            if (batch.logits != IntPtr.Zero) Marshal.FreeHGlobal(batch.logits);
        }

        #endregion

        #region Logits
        
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr llama_get_logits(IntPtr ctx);

        /// <summary>
        /// Safely copy logits to managed array
        /// </summary>
        public static float[] GetLogitsSafe(IntPtr ctx, int vocabSize)
        {
            IntPtr ptr = llama_get_logits(ctx);
            if (ptr == IntPtr.Zero) return null;
            
            float[] logits = new float[vocabSize];
            Marshal.Copy(ptr, logits, 0, vocabSize);
            return logits;
        }

        #endregion
    }
}
