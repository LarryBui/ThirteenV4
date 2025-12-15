using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Domain.Services;
using TienLen.Infrastructure.Config;
using UnityEngine;

namespace TienLen.Infrastructure.Services
{
    /// <summary>
    /// Authenticates against Nakama using a device ID and opens a realtime socket for gameplay.
    /// </summary>
    public sealed class NakamaAuthenticationService : IAuthenticationService
    {
        private readonly ITienLenAppConfig _config;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private bool _socketEventsHooked;

        private ISession _session;
        private IClient _client;
        private ISocket _socket;

        public NakamaAuthenticationService(ITienLenAppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool IsAuthenticated => IsSessionValid() && IsSocketConnected();
        public string CurrentUserId => _session?.UserId;

        internal ISocket Socket => _socket;

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

                _client ??= CreateClient();

                if (!IsSessionValid())
                {
                    // AuthenticateDeviceAsync returns a Task, await it directly.
                    _session = await _client.AuthenticateDeviceAsync(_config.DeviceId, create: true);
                    Debug.Log("Authenticated with Nakama using device ID.");
                }

                await EnsureSocketAsync();
                OnAuthenticated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Authentication failed: {ex.Message}");
                OnAuthenticationFailed?.Invoke(ex.Message);
                throw;
            }
            finally
            {
                _authLock.Release();
            }
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
                Debug.Log("Connected to Nakama realtime socket.");
            }
            catch
            {
                _socket = null;
                _socketEventsHooked = false;
                throw;
            }
        }

        private static void HookSocketEvents(ISocket socket)
        {
            socket.Closed += reason => Debug.Log($"Nakama socket closed: {reason}");
            socket.ReceivedError += error => Debug.LogError($"Nakama socket error: {error.Message}");
        }

        private bool IsSessionValid() => _session != null && !_session.IsExpired;

        private bool IsSocketConnected() => _socket != null && _socket.IsConnected;
    }
}
