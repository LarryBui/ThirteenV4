using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Publishes critical errors for application-wide handling.
    /// </summary>
    public interface IAppErrorBus
    {
        /// <summary>
        /// Fired when an application error is published.
        /// </summary>
        event Action<AppError> AppErrorPublished;

        /// <summary>
        /// Publishes an application error to subscribers.
        /// </summary>
        /// <param name="error">The error to publish.</param>
        void Publish(AppError error);
    }
}
