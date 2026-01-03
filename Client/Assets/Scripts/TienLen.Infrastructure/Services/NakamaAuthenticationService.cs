using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nakama;
using Newtonsoft.Json;
using TienLen.Application;
using TienLen.Application.Session;
using TienLen.Infrastructure.Config;

namespace TienLen.Infrastructure.Services
{
    /// <summary>
    /// Authenticates against Nakama using a device ID and opens a realtime socket for gameplay.
    /// </summary>
    public sealed class NakamaAuthenticationService : IAuthenticationService
    {
        private readonly ITienLenAppConfig _config;
        private readonly IGameSessionContext _gameSessionContext; // Injected
        private readonly ILogger<NakamaAuthenticationService> _logger;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private bool _socketEventsHooked;

        private ISession _session;
        private IClient _client;
        private ISocket _socket;

        public NakamaAuthenticationService(
            ITienLenAppConfig config,
            IGameSessionContext gameSessionContext,
            ILogger<NakamaAuthenticationService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gameSessionContext = gameSessionContext ?? throw new ArgumentNullException(nameof(gameSessionContext));
            _logger = logger ?? NullLogger<NakamaAuthenticationService>.Instance;
        }

        public bool IsAuthenticated => IsSessionValid() && IsSocketConnected();
        public string CurrentUserId => _session?.UserId;
        public string CurrentUserDisplayName => _session?.Username;
        public int CurrentUserAvatarIndex => GetAvatarIndex(_session?.UserId);

        public ISocket Socket => _socket;
        public IClient Client => _client;
        public ISession Session => _session;

        public event Action OnAuthenticated;
        public event Action<string> OnAuthenticationFailed;

        /// <summary>
        /// Ensures the user is authenticated and a Nakama socket connection is active.
        /// Safe to call multiple times; subsequent calls reuse the existing session/socket.
        /// </summary>
        public async UniTask LoginAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                if (IsAuthenticated)
                {
                    OnAuthenticated?.Invoke();
                    return;
                }

                if (_config == null) _logger.LogError("NakamaAuth: Config is NULL!");
                _client ??= CreateClient();
                if (_client == null) _logger.LogError("NakamaAuth: Client creation failed (null).");

                if (!IsSessionValid())
                {
                    // AuthenticateDeviceAsync returns a Task, await it directly.
                    _session = await _client.AuthenticateDeviceAsync(_config.DeviceId, create: true);
                    
                    // Fetch full account details to get the wallet
                    var account = await _client.GetAccountAsync(_session);
                    var wallet = JsonConvert.DeserializeObject<Dictionary<string, long>>(account.Wallet);
                    long balance = 0;
                    if (wallet != null && wallet.ContainsKey("gold"))
                    {
                        balance = wallet["gold"];
                    }

                    // Update Game Session Context
                    _gameSessionContext.SetIdentity(_session.UserId, _session.Username, GetAvatarIndex(_session.UserId), balance);
                }

                await EnsureSocketAsync();
                OnAuthenticated?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed.");
                OnAuthenticationFailed?.Invoke(ex.Message);
                throw;
            }
            finally
            {
                _authLock.Release();
            }
        }

        public async UniTask<string> ExecuteRpcAsync(string id, string payload)
        {
            if (_socket == null)
            {
                throw new InvalidOperationException("Nakama socket is not available.");
            }

            var result = await _socket.RpcAsync(id, payload);
            return result.Payload;
        }

        private IClient CreateClient()
        {
            return new Client(_config.Scheme, _config.Host, _config.Port, _config.ServerKey, UnityWebRequestAdapter.Instance);
        }

        private async UniTask EnsureSocketAsync()
        {
            _socket ??= _client.NewSocket();

            if (!_socketEventsHooked && _socket != null)
            {
                HookSocketEvents(_socket);
                _socketEventsHooked = true;
            }

            if (IsSocketConnected())
            {
                return;
            }

            try
            {
                // ConnectAsync returns a Task.
                await _socket.ConnectAsync(_session);
            }
            catch
            {
                _socket = null;
                _socketEventsHooked = false;
                throw;
            }
        }

        private int GetAvatarIndex(string userId)
        {
            return 0; // Placeholder implementation
            if (string.IsNullOrEmpty(userId)) return 0;
            // Simple deterministic avatar selection based on UserId hash
            // This assumes we have a pool of avatars to pick from (e.g., 0-3 for 4 avatars)
            // Need to know the total number of available avatars. For now, let's assume 4.
            int hash = userId.GetHashCode();
            return Math.Abs(hash % 4); // Example: maps to indices 0, 1, 2, 3
        }

        private void HookSocketEvents(ISocket socket)
        {
            socket.Closed += () => _logger.LogWarning("Nakama socket closed.");
            socket.ReceivedError += error => _logger.LogError(error, "Nakama socket error.");
        }

        private bool IsSessionValid() => _session != null && !_session.IsExpired;

        private bool IsSocketConnected() => _socket != null && _socket.IsConnected;
    }
}
