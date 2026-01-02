using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Emits application errors raised from network failures.
    /// </summary>
    public interface IErrorNetworkClient
    {
        /// <summary>
        /// Fired when an application error is raised.
        /// </summary>
        event Action<AppError> ErrorRaised;

        /// <summary>
        /// Raises an application error to subscribers.
        /// </summary>
        /// <param name="error">The error to publish.</param>
        void Raise(AppError error);
    }
}
