using System.Threading;
using System.Threading.Tasks;

namespace TienLen.Application.Authentication
{
    /// <summary>
    /// Authentication boundary for the client.
    /// Implementations should perform the platform-specific auth flow and return a session token.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticate the current user (create if needed) and return an auth result.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the auth request.</param>
        Task<AuthResult> AuthenticateAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of a successful authentication request.
    /// </summary>
    public sealed class AuthResult
    {
        public string UserId { get; }

        public string Username { get; }

        public string SessionToken { get; }

        public AuthResult(string userId, string username, string sessionToken)
        {
            UserId = userId;
            Username = username;
            SessionToken = sessionToken;
        }
    }
}
