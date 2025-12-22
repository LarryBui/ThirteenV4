using System.Threading;
using Cysharp.Threading.Tasks;

namespace TienLen.Application.Speech
{
    /// <summary>
    /// Abstraction for speech-to-text capture using native device capabilities.
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>
        /// True when the current platform supports speech-to-text capture.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// True while a speech capture session is active.
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// Captures a single speech segment and returns the transcribed text.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel listening.</param>
        UniTask<string> CaptureOnceAsync(CancellationToken cancellationToken);
    }
}
