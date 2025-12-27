using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace TienLen.Infrastructure.Config
{
    /// <summary>
    /// Static utility to load application configuration from the StreamingAssets folder.
    /// This allows changing server settings (Host, Port, etc.) without recompiling the C# code.
    /// </summary>
    /// <remarks>
    /// The loader looks for 'app_config.json' in the StreamingAssets folder. 
    /// If the file is missing or invalid, it gracefully falls back to hardcoded defaults in NakamaConfig.
    /// </remarks>
    public static class ConfigLoader
    {
        private const string ConfigFileName = "app_config.json";

        /// <summary>
        /// Attempts to load Nakama settings from StreamingAssets.
        /// </summary>
        /// <param name="deviceId">The unique device identifier to persist in the resulting config.</param>
        /// <returns>A populated NakamaConfig instance from JSON, or a default instance if loading fails.</returns>
        public static NakamaConfig Load(string deviceId)
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, ConfigFileName);

            if (!File.Exists(path))
            {
                Debug.Log($"[ConfigLoader] Config file not found at {path}. Using internal defaults.");
                return new NakamaConfig(deviceId);
            }

            try
            {
                string json = File.ReadAllText(path);
                var dto = JsonConvert.DeserializeObject<NakamaConfigDto>(json);

                if (dto == null)
                {
                    Debug.LogWarning("[ConfigLoader] Config file was empty or invalid. Using defaults.");
                    return new NakamaConfig(deviceId);
                }

                Debug.Log($"[ConfigLoader] Loaded config from {path} (Host: {dto.Host})");

                // 3. Apply Command Line Arguments Overrides (Highest Priority)
                // Usage: Game.exe -host 10.0.0.5 -port 8000
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Equals("-host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        dto.Host = args[i + 1];
                        Debug.Log($"[ConfigLoader] CLI Override: Host set to {dto.Host}");
                    }
                    if (args[i].Equals("-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        if (int.TryParse(args[i + 1], out int p))
                        {
                            dto.Port = p;
                            Debug.Log($"[ConfigLoader] CLI Override: Port set to {dto.Port}");
                        }
                    }
                    if (args[i].Equals("-scheme", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        dto.Scheme = args[i + 1];
                    }
                    if (args[i].Equals("-serverKey", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        dto.ServerKey = args[i + 1];
                    }
                }

                return new NakamaConfig(
                    deviceId: deviceId,
                    scheme: dto.Scheme,
                    host: dto.Host,
                    port: dto.Port,
                    serverKey: dto.ServerKey
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigLoader] Failed to parse config file: {ex.Message}");
                return new NakamaConfig(deviceId);
            }
        }

        [Serializable]
        private class NakamaConfigDto
        {
            public string Host { get; set; }
            public int Port { get; set; }
            public string Scheme { get; set; }
            public string ServerKey { get; set; }
        }
    }
}
