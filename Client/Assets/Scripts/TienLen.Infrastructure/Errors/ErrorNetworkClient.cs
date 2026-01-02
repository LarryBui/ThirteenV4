using System;
using TienLen.Application.Errors;

namespace TienLen.Infrastructure.Errors
{
    /// <summary>
    /// In-memory error source for network-related application errors.
    /// </summary>
    public sealed class ErrorNetworkClient : IErrorNetworkClient
    {
        public event Action<AppError> ErrorRaised;

        public void Raise(AppError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (error.Outcome == ErrorOutcome.InlineScene)
            {
                return;
            }
            ErrorRaised?.Invoke(error);
        }
    }
}
