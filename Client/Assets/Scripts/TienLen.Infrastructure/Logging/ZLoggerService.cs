using System;
using System.IO;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;
using ZLogger.Unity;

namespace TienLen.Infrastructure.Logging
{
    /// <summary>
    /// Configures ZLogger providers for Unity and exposes the logger factory to the app.
    /// Lifecycle is managed by VContainer (IDisposable).
    /// </summary>
    public sealed class ZLoggerService : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Gets the full path of the active log file.
        /// </summary>
        public string LogFilePath { get; }

        /// <summary>
        /// Gets the logger factory configured for the application.
        /// </summary>
        public ILoggerFactory LoggerFactory => _loggerFactory;

        /// <summary>
        /// Initializes the ZLogger pipeline for Unity.
        /// </summary>
        public ZLoggerService()
        {
            LogFilePath = BuildLogFilePath();

            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(ConfigureLogging);
            var internalLogger = _loggerFactory.CreateLogger<ZLoggerService>();

            internalLogger.LogInformation("Logging initialized. logFile={logFile}", LogFilePath);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        private void ConfigureLogging(ILoggingBuilder builder)
        {
            builder.SetMinimumLevel(Debug.isDebugBuild ? LogLevel.Debug : LogLevel.Information);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.AddZLoggerUnityDebug();
#endif

            builder.AddZLoggerFile(LogFilePath, options =>
            {
                options.UseJsonFormatter();
            });
        }

        private static string BuildLogFilePath()
        {
            var logDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            var fileName = $"tienlen_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            return Path.Combine(logDirectory, fileName);
        }
    }
}
