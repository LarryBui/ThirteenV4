using System;
using Cysharp.Threading.Tasks;

namespace TienLen.Domain.Services
{
    /// <summary>
    /// Handles authentication against the backend.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// True if the user is currently authenticated and connected to the backend.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// The User ID of the currently authenticated user. Null if not authenticated.
        /// </summary>
        string CurrentUserId { get; }

        /// <summary>
        /// Fired when authentication completes successfully.
        /// </summary>
        event Action OnAuthenticated;

        /// <summary>
        /// Fired when authentication fails, providing the error message.
        /// </summary>
        event Action<string> OnAuthenticationFailed;

        /// <summary>
        /// Authenticates the user and connects to the backend. Safe to call multiple times.
        /// </summary>
        UniTask LoginAsync();
    }
}
