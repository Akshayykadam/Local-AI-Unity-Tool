using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace LocalAI.Editor.Services
{
    public class ModelManager
    {
        public enum ModelState
        {
            NotInstalled,
            Downloading,
            Verifying,
            Ready,
            Error
        }

        public struct ModelMetadata
        {
            public string Id;
            public string Version;
            public string DownloadUrl;
            public string ExpectedChecksum; // SHA256
            public long SizeBytes;
        }

        // Hardcoded for V1, in future load from JSON
        public static readonly ModelMetadata DefaultModel = new ModelMetadata
        {
            Id = "mistral-7b-q4",
            Version = "1.0.0",
            DownloadUrl = "https://hf-mirror.com/TheBloke/Mistral-7B-Instruct-v0.1-GGUF/resolve/main/mistral-7b-instruct-v0.1.Q4_K_M.gguf",
            ExpectedChecksum = "INVALID_PLACEHOLDER", // Fill with real hash if known or ignore for proto
            SizeBytes = 4368438272 // Approx
        };

        public event Action<ModelState> OnStateChanged;
        public event Action<DownloadProgress> OnDownloadProgress;

        public ModelState CurrentState { get; private set; } = ModelState.NotInstalled;
        public string ErrorMessage { get; private set; }

        private readonly ModelDownloadService _downloadService;
        private CancellationTokenSource _downloadCts;

        public ModelManager()
        {
            _downloadService = new ModelDownloadService();
            CheckModelStatus();
        }

        public string GetModelDirectory()
        {
            string baseDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                baseDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");
            }

            return Path.Combine(baseDataPath, "LocalAIUnity", "models");
        }

        public string GetModelPath()
        {
            return Path.Combine(GetModelDirectory(), DefaultModel.Id + ".gguf");
        }

        public void CheckModelStatus()
        {
            string path = GetModelPath();
            if (File.Exists(path))
            {
                // Shallow check: existence. Deep check: checksum (expensive on startup, maybe separate action)
                SetState(ModelState.Ready);
            }
            else
            {
                SetState(ModelState.NotInstalled);
            }
        }

        public async void StartDownload()
        {
            if (CurrentState == ModelState.Downloading) return;

            Directory.CreateDirectory(GetModelDirectory());
            SetState(ModelState.Downloading);
            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<DownloadProgress>(p => OnDownloadProgress?.Invoke(p));
            string finalPath = GetModelPath();

            bool success = await _downloadService.DownloadFileAsync(DefaultModel.DownloadUrl, finalPath, progress, _downloadCts.Token);

            if (success)
            {
                SetState(ModelState.Verifying);
                // Optional: Verify checksum here. For now skip or assume good.
                SetState(ModelState.Ready);
            }
            else
            {
                if (_downloadCts.IsCancellationRequested)
                {
                    SetState(ModelState.NotInstalled);
                }
                else
                {
                    ErrorMessage = "Download failed.";
                    SetState(ModelState.Error);
                }
            }
        }

        public void CancelDownload()
        {
            if (_downloadCts != null)
            {
                _downloadCts.Cancel();
                _downloadCts = null;
            }
        }

        private void SetState(ModelState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
