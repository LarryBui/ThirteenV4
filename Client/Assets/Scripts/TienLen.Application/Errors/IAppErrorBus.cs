using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Publishes critical errors for application-wide handling.
    /// </summary>
    public interface IAppErrorBus
    {
        /// <summary>
        /// Fired when a critical error is published.
        /// </summary>
        event Action<CriticalError> CriticalErrorPublished;

        /// <summary>
        /// Publishes a critical error to subscribers.
        /// </summary>
        /// <param name="error">The error to publish.</param>
        void Publish(CriticalError error);
    }
}
