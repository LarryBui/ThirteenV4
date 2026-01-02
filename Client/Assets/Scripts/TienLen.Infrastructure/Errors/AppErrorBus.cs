using System;
using TienLen.Application.Errors;

namespace TienLen.Infrastructure.Errors
{
    /// <summary>
    /// In-memory implementation of the application error bus.
    /// </summary>
    public sealed class AppErrorBus : IAppErrorBus
    {
        public event Action<AppError> AppErrorPublished;

        public void Publish(AppError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            AppErrorPublished?.Invoke(error);
        }
    }
}
