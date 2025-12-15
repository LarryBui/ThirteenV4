namespace TienLen.Infrastructure.Config
{
    /// <summary>
    /// Provides application-level configuration values needed to connect to backend services.
    /// </summary>
    public interface ITienLenAppConfig
    {
        /// <summary>Transport scheme, typically http or https.</summary>
        string Scheme { get; }

        /// <summary>Hostname or IP of the backend.</summary>
        string Host { get; }

        /// <summary>Port to reach the backend endpoint.</summary>
        int Port { get; }

        /// <summary>Server key configured on the backend.</summary>
        string ServerKey { get; }

        /// <summary>Unique device identifier used for authentication.</summary>
        string DeviceId { get; }
    }
}
