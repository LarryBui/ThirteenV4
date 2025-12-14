using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using TienLen.Presentation;

namespace TienLen.Infrastructure
{
    /// <summary>
    /// Bootstraps Nakama client/session/socket on app start with a fresh device user each run.
    /// Updates Home UI progress and enables Play/Quit after auth connects.
    /// </summary>
    public sealed class NakamaAuthBootstrap : MonoBehaviour
    {
        [Header("Nakama")]
        [SerializeField] private string scheme = "http";
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private string serverKey = "defaultkey";

        [Header("UI")]
        [SerializeField] private HomeUIController homeUI;

        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public ISocket Socket { get; private set; }

        private async void Start()
        {
            if (homeUI == null)
            {
                homeUI = FindFirstObjectByType<HomeUIController>();
            }

            homeUI?.ShowAuthProgress(0.05f, "Connecting…");
            await AuthenticateAndConnect();
        }

        private async Task AuthenticateAndConnect()
        {
            try
            {
                Client = new Client(scheme, host, port, serverKey);

                var deviceId = GetDeviceId();
                homeUI?.ShowAuthProgress(0.25f, "Authenticating…");
                Session = await Client.AuthenticateDeviceAsync(deviceId, null, create: true);

                homeUI?.ShowAuthProgress(0.55f, "Connecting socket…");
                Socket = global::Nakama.Socket.From(Client);
                await Socket.ConnectAsync(Session);

                homeUI?.OnAuthComplete();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Nakama auth/connect failed: {ex}");
                homeUI?.OnAuthFailed($"Auth failed: {ex.Message}");
            }
        }

        private string GetDeviceId()
        {
            var id = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
            {
                id = Guid.NewGuid().ToString();
            }
            return id;
        }
    }
}

