using System.Threading.Tasks;
using Nakama;

namespace TienLen.Domain.Services
{
    /// <summary>
    /// Handles authentication against the backend and exposes the Nakama connection objects.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Current Nakama session; null until authentication completes.
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// Nakama client used for REST calls and socket creation.
        /// </summary>
        IClient Client { get; }

        /// <summary>
        /// Active Nakama realtime socket connection.
        /// </summary>
        ISocket Socket { get; }

        /// <summary>
        /// Authenticates the user and ensures the socket is connected. Safe to call multiple times.
        /// </summary>
        Task AuthenticateAndConnectAsync();
    }
}
