using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Handles application errors published by the application and exposes them to presenters.
    /// </summary>
    public sealed class AppErrorHandler : IDisposable
    {
        private readonly IAppErrorBus _errorBus;

        /// <summary>
        /// Fired when an application error is received from the application error bus.
        /// </summary>
        public event Action<AppError> OnAppError;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppErrorHandler"/> class.
        /// </summary>
        /// <param name="errorBus">Application error bus for critical error publishing.</param>
        public AppErrorHandler(IAppErrorBus errorBus)
        {
            _errorBus = errorBus ?? throw new ArgumentNullException(nameof(errorBus));
            _errorBus.AppErrorPublished += HandleAppError;
        }

        public void Dispose()
        {
            _errorBus.AppErrorPublished -= HandleAppError;
        }

        private void HandleAppError(AppError error)
        {
            OnAppError?.Invoke(error);
        }
    }
}
