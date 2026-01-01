using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Handles critical errors published by the application and exposes them to presenters.
    /// </summary>
    public sealed class AppErrorHandler : IDisposable
    {
        private readonly IAppErrorBus _errorBus;

        /// <summary>
        /// Fired when a critical error is received from the application error bus.
        /// </summary>
        public event Action<CriticalError> OnCriticalError;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppErrorHandler"/> class.
        /// </summary>
        /// <param name="errorBus">Application error bus for critical error publishing.</param>
        public AppErrorHandler(IAppErrorBus errorBus)
        {
            _errorBus = errorBus ?? throw new ArgumentNullException(nameof(errorBus));
            _errorBus.CriticalErrorPublished += HandleCriticalError;
        }

        public void Dispose()
        {
            _errorBus.CriticalErrorPublished -= HandleCriticalError;
        }

        private void HandleCriticalError(CriticalError error)
        {
            OnCriticalError?.Invoke(error);
        }
    }
}
