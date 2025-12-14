using System;
using System.Threading.Tasks;
using Nakama;
using TienLen.Domain.Services;
using UnityEngine;

namespace TienLen.Infrastructure.Services
{
    public class NakamaConfig
    {
        public string Scheme = "http";
        public string Host = "127.0.0.1";
        public int Port = 7350;
        public string ServerKey = "defaultkey";
    }

    public class NakamaAuthenticationService : IAuthenticationService
    {
        private readonly NakamaConfig _config;

        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public ISocket Socket { get; private set; }

        public NakamaAuthenticationService(NakamaConfig config)
        {
            _config = config;
        }

        public async Task AuthenticateAndConnectAsync()
        {
            try
            {
                Client = new Client(_config.Scheme, _config.Host, _config.Port, _config.ServerKey);

                var deviceId = GetDeviceId();
                // TODO: Add proper logging or event bus for "Authenticating..." status
                Session = await Client.AuthenticateDeviceAsync(deviceId, null, create: true);

                Socket = global::Nakama.Socket.From(Client);
                await Socket.ConnectAsync(Session);
                
                // TODO: Add proper logging or event bus for "Connected" status
                Debug.Log("Nakama Connected.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Nakama auth/connect failed: {ex}");
                throw;
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