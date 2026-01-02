using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Handles application errors published by the application and exposes them to presenters.
    /// </summary>
    public sealed class AppErrorHandler : IDisposable
    {
        private readonly IErrorNetworkClient _errorNetworkClient;

        /// <summary>
        /// Fired when an application error is received from the application error bus.
        /// </summary>
        public event Action<AppError> OnAppError;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppErrorHandler"/> class.
        /// </summary>
        /// <param name="errorBus">Application error bus for critical error publishing.</param>
        public AppErrorHandler(IErrorNetworkClient errorNetworkClient)
        {
            _errorNetworkClient = errorNetworkClient ?? throw new ArgumentNullException(nameof(errorNetworkClient));
            _errorNetworkClient.ErrorRaised += HandleAppError;
        }

        public void Dispose()
        {
            _errorNetworkClient.ErrorRaised -= HandleAppError;
        }

        private void HandleAppError(AppError error)
        {
            OnAppError?.Invoke(error);
        }
    }
}
