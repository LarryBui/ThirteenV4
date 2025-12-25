using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TienLen.Application.Speech;

namespace TienLen.Infrastructure.Speech
{
    /// <summary>
    /// Fallback speech-to-text service for unsupported platforms.
    /// </summary>
    public sealed class UnsupportedSpeechToTextService : ISpeechToTextService
    {
        /// <inheritdoc />
        public bool IsSupported => false;

        /// <inheritdoc />
        public bool IsListening => false;

        /// <inheritdoc />
        public UniTask<string> CaptureOnceAsync(System.Threading.CancellationToken cancellationToken)
        {
            return UniTask.FromException<string>(new System.NotSupportedException());
        }

        public event System.Action<string> OnPhraseRecognized;
        public void StartTranscribing() { }
        public void StopTranscribing() { }
    }
}
