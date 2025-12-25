using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application.Speech;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace TienLen.Infrastructure.Speech
{
    /// <summary>
    /// Windows-specific speech-to-text implementation backed by DictationRecognizer.
    /// </summary>
    public sealed class WindowsSpeechToTextService : ISpeechToTextService, IDisposable
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _recognizer;
        private UniTaskCompletionSource<string> _completionSource;
        private CancellationTokenRegistration _cancellationRegistration;
        private bool _isSupported = true;
        private int _completionState;
#endif

        private readonly ILogger<WindowsSpeechToTextService> _logger;
        private bool _isListening;
        private bool _isContinuous;

        /// <summary>
        /// Fired when a phrase is finalized during continuous transcription.
        /// </summary>
        public event Action<string> OnPhraseRecognized;

        /// <summary>
        /// Creates a new Windows speech-to-text service.
        /// </summary>
        /// <param name="logger">Logger for speech diagnostics.</param>
        public WindowsSpeechToTextService(ILogger<WindowsSpeechToTextService> logger)
        {
            _logger = logger ?? NullLogger<WindowsSpeechToTextService>.Instance;
        }

        /// <inheritdoc />
        public bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return _isSupported;
#else
                return false;
#endif
            }
        }

        /// <inheritdoc />
        public bool IsListening => _isListening;

        /// <inheritdoc />
        public UniTask<string> CaptureOnceAsync(CancellationToken cancellationToken)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_isListening)
            {
                return UniTask.FromException<string>(new InvalidOperationException("Speech capture is already running."));
            }

            if (!_isSupported)
            {
                return UniTask.FromException<string>(new NotSupportedException("Speech capture is not supported on this device."));
            }

            try
            {
                EnsureRecognizer();
            }
            catch (Exception ex)
            {
                return UniTask.FromException<string>(new InvalidOperationException($"Speech capture failed to initialize: {ex.Message}", ex));
            }
            _isListening = true;
            _completionState = 0;
            _completionSource = new UniTaskCompletionSource<string>();
            _cancellationRegistration = cancellationToken.Register(() =>
                FinishCaptureAsync(isSuccess: false, result: null, failure: new OperationCanceledException()).Forget());

            try
            {
                _recognizer.Start();
            }
            catch (Exception ex)
            {
                FinishCaptureAsync(isSuccess: false, result: null, failure: ex).Forget();
            }

            return _completionSource.Task;
#else
            return UniTask.FromException<string>(new NotSupportedException("Speech capture is not supported on this platform."));
#endif
        }

        public void StartTranscribing()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_isListening) return;
            try
            {
                UnityEngine.Debug.Log("[WindowsSTT] Starting continuous transcription...");
                EnsureRecognizer();
                _isListening = true;
                _isContinuous = true;
                _recognizer.Start();
                UnityEngine.Debug.Log($"[WindowsSTT] Recognizer started. Status: {_recognizer.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start continuous transcription.");
                _isListening = false;
            }
#endif
        }

        public void StopTranscribing()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!_isListening) return;
            CleanupCaptureState();
            _isContinuous = false;
#endif
        }

        public void Dispose()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            CleanupRecognizer();
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void EnsureRecognizer()
        {
            if (_recognizer != null) return;

            _recognizer = new DictationRecognizer();
            _recognizer.DictationResult += HandleResult;
            _recognizer.DictationComplete += HandleComplete;
            _recognizer.DictationError += HandleError;
        }

        private void HandleResult(string text, ConfidenceLevel confidence)
        {
            UnityEngine.Debug.Log($"[WindowsSTT] HandleResult: '{text}' (Confidence: {confidence})");
            if (_isContinuous)
            {
                OnPhraseRecognized?.Invoke(text);
            }
            else
            {
                FinishCaptureAsync(isSuccess: true, result: text, failure: null).Forget();
            }
        }

        private void HandleComplete(DictationCompletionCause cause)
        {
            if (!_isListening) return;

            if (cause == DictationCompletionCause.Complete || cause == DictationCompletionCause.TimeoutExceeded)
            {
                FinishCaptureAsync(isSuccess: true, result: string.Empty, failure: null).Forget();
                return;
            }

            FinishCaptureAsync(isSuccess: false, result: null, failure: new InvalidOperationException($"Speech capture ended: {cause}."))
                .Forget();
        }

        private void HandleError(string error, int hresult)
        {
            UnityEngine.Debug.LogError($"[WindowsSTT] Error: {error} (HResult: {hresult})");
            if (hresult == unchecked((int)0x80045509))
            {
                _isSupported = false;
            }

            FinishCaptureAsync(isSuccess: false, result: null,
                    failure: new InvalidOperationException($"Speech capture error: {error} ({hresult})."))
                .Forget();
        }

        /// <summary>
        /// Finalizes a speech capture once, marshaling to the main thread for cleanup.
        /// </summary>
        private async UniTaskVoid FinishCaptureAsync(bool isSuccess, string result, Exception failure)
        {
            if (!_isListening) return;
            if (Interlocked.Exchange(ref _completionState, 1) == 1) return;

            await UniTask.SwitchToMainThread();

            if (isSuccess)
            {
                _completionSource?.TrySetResult(result ?? string.Empty);
            }
            else
            {
                if (failure is OperationCanceledException)
                {
                    _completionSource?.TrySetCanceled();
                }
                else
                {
                    _logger.LogWarning(failure, "Speech capture cancelled.");
                    _completionSource?.TrySetException(failure ?? new InvalidOperationException("Speech capture cancelled."));
                }
            }

            CleanupCaptureState();
        }

        private void CleanupCaptureState()
        {
            _isListening = false;
            _cancellationRegistration.Dispose();
            _completionSource = null;

            if (_recognizer != null && _recognizer.Status == SpeechSystemStatus.Running)
            {
                _recognizer.Stop();
            }
        }

        private void CleanupRecognizer()
        {
            if (_recognizer == null) return;

            if (_recognizer.Status == SpeechSystemStatus.Running)
            {
                _recognizer.Stop();
            }

            _recognizer.DictationResult -= HandleResult;
            _recognizer.DictationComplete -= HandleComplete;
            _recognizer.DictationError -= HandleError;
            _recognizer.Dispose();
            _recognizer = null;
        }
#endif
    }
}
