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
#endif

        private readonly ILogger<WindowsSpeechToTextService> _logger;
        private bool _isListening;

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
                return true;
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

            EnsureRecognizer();
            _isListening = true;
            _completionSource = new UniTaskCompletionSource<string>();
            _cancellationRegistration = cancellationToken.Register(() => CancelCapture("Speech capture cancelled."));

            try
            {
                _recognizer.Start();
            }
            catch (Exception ex)
            {
                CancelCapture($"Speech capture failed to start: {ex.Message}");
            }

            return _completionSource.Task;
#else
            return UniTask.FromException<string>(new NotSupportedException("Speech capture is not supported on this platform."));
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
            CompleteCapture(text);
        }

        private void HandleComplete(DictationCompletionCause cause)
        {
            if (!_isListening) return;

            if (cause == DictationCompletionCause.Complete)
            {
                CompleteCapture(string.Empty);
                return;
            }

            CancelCapture($"Speech capture ended: {cause}.");
        }

        private void HandleError(string error, int hresult)
        {
            CancelCapture($"Speech capture error: {error} ({hresult}).");
        }

        private void CompleteCapture(string text)
        {
            if (!_isListening) return;

            _completionSource?.TrySetResult(text ?? string.Empty);
            CleanupCaptureState();
        }

        private void CancelCapture(string reason)
        {
            if (!_isListening) return;

            _logger.LogWarning("Speech capture cancelled. reason={reason}", reason);
            _completionSource?.TrySetException(new InvalidOperationException(reason));
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
