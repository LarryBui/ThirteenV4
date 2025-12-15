using System;

namespace TienLen.Infrastructure.Config
{
    /// <summary>
    /// Immutable Nakama client configuration used to authenticate and open sockets.
    /// Hard-coded defaults live here to keep wiring minimal in the lifetime scope.
    /// </summary>
    public sealed class NakamaConfig : ITienLenAppConfig
    {
        public const string DefaultScheme = "http";
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 7350;
        public const string DefaultServerKey = "defaultkey";

        public NakamaConfig(
            string deviceId,
            string scheme = DefaultScheme,
            string host = DefaultHost,
            int port = DefaultPort,
            string serverKey = DefaultServerKey)
        {
            Scheme = string.IsNullOrWhiteSpace(scheme) ? DefaultScheme : scheme;
            Host = string.IsNullOrWhiteSpace(host) ? DefaultHost : host;
            Port = port;
            ServerKey = string.IsNullOrWhiteSpace(serverKey) ? DefaultServerKey : serverKey;
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? Guid.NewGuid().ToString() : deviceId;
        }

        /// <inheritdoc />
        public string Scheme { get; }

        /// <inheritdoc />
        public string Host { get; }

        /// <inheritdoc />
        public int Port { get; }

        /// <inheritdoc />
        public string ServerKey { get; }

        /// <inheritdoc />
        public string DeviceId { get; }
    }
}
