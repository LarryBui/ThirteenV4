using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using TienLen.Application.Authentication;

namespace TienLen.Infrastructure.Authentication
{
    /// <summary>
    /// Nakama-backed authentication service that creates a device user and returns a session token.
    /// </summary>
    public sealed class NakamaAuthenticationService : IAuthenticationService, INakamaSessionProvider
    {
        private readonly IClient _client;
        private readonly Func<string> _deviceIdProvider;

        private ISession _session;

        public NakamaAuthenticationService(IClient client, Func<string> deviceIdProvider)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _deviceIdProvider = deviceIdProvider ?? throw new ArgumentNullException(nameof(deviceIdProvider));
        }

        public ISession Session => _session ?? throw new InvalidOperationException("AuthenticateAsync must be called before accessing Session.");

        public async Task<AuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var deviceId = _deviceIdProvider.Invoke();
            _session = await _client.AuthenticateDeviceAsync(deviceId, username: null, create: true, cancellationToken: cancellationToken);

            return new AuthResult(_session.UserId, _session.Username, _session.AuthToken);
        }
    }
}
