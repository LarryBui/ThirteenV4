using System;
using TienLen.Application.Errors;

namespace TienLen.Infrastructure.Errors
{
    /// <summary>
    /// In-memory implementation of the application error bus.
    /// </summary>
    public sealed class AppErrorBus : IAppErrorBus
    {
        public event Action<CriticalError> CriticalErrorPublished;

        public void Publish(CriticalError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            CriticalErrorPublished?.Invoke(error);
        }
    }
}
