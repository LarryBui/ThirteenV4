using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly NakamaConfig _config;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private bool _socketEventsHooked;

        public NakamaAuthenticationService(NakamaConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ISession Session { get; private set; }
        public IClient Client { get; private set; }
        public ISocket Socket { get; private set; }

        /// <summary>
        /// Ensures the user is authenticated and a Nakama socket connection is active.
        /// Safe to call multiple times; subsequent calls reuse the existing session/socket.
        /// </summary>
        public async Task AuthenticateAndConnectAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                if (IsSessionValid() && IsSocketConnected())
                {
                    return;
                }

                Client ??= CreateClient();

                if (!IsSessionValid())
                {
                    Session = await Client.AuthenticateDeviceAsync(_config.DeviceId, create: true);
                    Debug.Log("Authenticated with Nakama using device ID.");
                }

                await EnsureSocketAsync();
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

        private async Task EnsureSocketAsync()
        {
            Socket ??= Client.NewSocket();

            if (!_socketEventsHooked && Socket != null)
            {
                HookSocketEvents(Socket);
                _socketEventsHooked = true;
            }

            if (IsSocketConnected())
            {
                return;
            }

            try
            {
                await Socket.ConnectAsync(Session);
                Debug.Log("Connected to Nakama realtime socket.");
            }
            catch
            {
                Socket = null;
                _socketEventsHooked = false;
                throw;
            }
        }

        private static void HookSocketEvents(ISocket socket)
        {
            socket.Closed += reason => Debug.Log($"Nakama socket closed: {reason}");
            socket.ReceivedError += error => Debug.LogError($"Nakama socket error: {error.Message}");
        }

        private bool IsSessionValid() => Session != null && !Session.IsExpired;

        private bool IsSocketConnected() => Socket != null && Socket.IsConnected;
    }
}
