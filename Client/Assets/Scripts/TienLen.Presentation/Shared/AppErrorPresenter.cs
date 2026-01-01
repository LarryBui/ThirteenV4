using System;
using TienLen.Application.Errors;

namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Routes critical application errors to the Error scene.
    /// </summary>
    public sealed class AppErrorPresenter : IDisposable
    {
        private readonly AppErrorHandler _errorHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppErrorPresenter"/> class.
        /// </summary>
        /// <param name="errorHandler">Application error handler.</param>
        public AppErrorPresenter(AppErrorHandler errorHandler)
        {
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _errorHandler.OnCriticalError += HandleCriticalError;
        }

        public void Dispose()
        {
            _errorHandler.OnCriticalError -= HandleCriticalError;
        }

        private static void HandleCriticalError(CriticalError error)
        {
            if (error == null) return;

            string message = string.IsNullOrWhiteSpace(error.Message)
                ? "Unexpected error."
                : error.Message;

            ErrorContext.ShowError(message);
        }
    }
}
