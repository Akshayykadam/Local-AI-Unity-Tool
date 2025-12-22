using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAI.Editor.Services
{
    /// <summary>
    /// Common interface for all inference services (local and cloud).
    /// </summary>
    public interface IInferenceService
    {
        /// <summary>
        /// Starts inference with the given prompt, streaming tokens via progress.
        /// </summary>
        /// <param name="prompt">The user prompt to process.</param>
        /// <param name="progress">Progress reporter for streaming tokens.</param>
        /// <param name="token">Cancellation token to stop inference.</param>
        Task StartInferenceAsync(string prompt, IProgress<string> progress, CancellationToken token);
        
        /// <summary>
        /// Cleans up any resources used by the service.
        /// </summary>
        void Dispose();
        
        /// <summary>
        /// Whether this service is ready to perform inference.
        /// </summary>
        bool IsReady { get; }
        
        /// <summary>
        /// Display name for UI.
        /// </summary>
        string DisplayName { get; }
    }
}
