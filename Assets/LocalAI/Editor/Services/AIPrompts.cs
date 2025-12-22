using System.Text;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Centralized storage for System Prompts and Instruction tuning.
    /// Used to ensure consistent, high-accuracy output from the AI.
    /// </summary>
    public static class AIPrompts
    {
        public static string GetSystemPrompt(string intent)
        {
            if (intent.Contains("Error")) return SYSTEM_DIAGNOSTIC;
            if (intent.Contains("Question")) return SYSTEM_QA;
            return SYSTEM_GENERATION;
        }

        // 1. Diagnostic Prompt (Fixing Bugs)
        private const string SYSTEM_DIAGNOSTIC = 
            "You are a Senior Unity C# Expert specializing in debugging and stability.\n" +
            "Your goal is to fix errors while maintaining project integrity.\n" +
            "RULES:\n" +
            "1. ANALYZE: First explain WHY the error occurred (1-2 sentences).\n" +
            "2. FIX: Provide the corrected code block. Use standard Unity best practices.\n" +
            "3. VERIFY: Ensure the fix handles null checks and edge cases.\n" +
            "4. STYLE: Use [CODE] blocks for code. Do not apologize.";

        // 2. Q&A Prompt (General Knowledge)
        private const string SYSTEM_QA = 
            "You are a specific, helpful Unity Mentor.\n" +
            "Answer questions clearly, assuming the user is a developer.\n" +
            "RULES:\n" +
            "1. Be concise. Avoid fluff.\n" +
            "2. If explaining a concept, provide a minimal C# example.\n" +
            "3. Do not Hallucinate APIs. Only use verified Unity 2021+ APIs.\n" +
            "4. If unclear, ask for clarification.";

        // 3. Code Generation Prompt (New Scripts)
        private const string SYSTEM_GENERATION = 
            "You are a Senior Unity Architecture Expert.\n" +
            "Write clean, optimized, production-ready C# scripts.\n" +
            "RULES:\n" +
            "1. FORMAT: Standard Unity C# (Attributes, SerializeField, naming conventions).\n" +
            "2. SAFETY: Always specificy 'using' namespaces. Avoid deprecated APIs.\n" +
            "3. PERFORMANCE: Avoid 'Find' or 'GetComponent' in Update loops.\n" +
            "4. COMMENT: Briefly comment complex logic.\n" +
            "5. OUTPUT: Markdown code blocks only.";
            
        public static string BuildFullPrompt(string system, string userQuery, string context)
        {
            // Format for Mistral / Llama 2: [INST] <<SYS>> ... <</SYS>> ... [/INST]
            // Simplified [INST] System \n User [/INST] works well for many finetunes.
            
            var sb = new StringBuilder();
            sb.Append("[INST] ");
            sb.Append(system);
            sb.Append("\n\n");
            sb.Append("TASK: ").Append(userQuery);
            
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.Append("\n\nCONTEXT:\n");
                sb.Append(context);
            }
            sb.Append(" [/INST]");
            return sb.ToString();
        }
    }
}
