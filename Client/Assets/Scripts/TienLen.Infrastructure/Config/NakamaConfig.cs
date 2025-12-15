using System;

namespace TienLen.Infrastructure.Config
{
    /// <summary>
    /// Immutable Nakama client configuration used to authenticate and open sockets.
    /// </summary>
    public sealed class NakamaConfig
    {
        public NakamaConfig(string scheme, string host, int port, string serverKey, string deviceId)
        {
            Scheme = string.IsNullOrWhiteSpace(scheme) ? "http" : scheme;
            Host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
            Port = port;
            ServerKey = string.IsNullOrWhiteSpace(serverKey) ? "defaultkey" : serverKey;
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId;
        }

        /// <summary>
        /// Transport scheme, typically http or https.
        /// </summary>
        public string Scheme { get; }

        /// <summary>
        /// Hostname or IP of the Nakama server.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Port to reach the Nakama HTTP endpoint.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Server key configured on the Nakama server.
        /// </summary>
        public string ServerKey { get; }

        /// <summary>
        /// Unique device identifier used for device authentication.
        /// </summary>
        public string DeviceId { get; }
    }
}
