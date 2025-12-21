using System;
using System.IO;
using Microsoft.Extensions.Logging;
using UnityEngine;
// Alias avoids collision with the TienLen.Application namespace.
using UnityApplication = UnityEngine.Application;
using ZLogger;
using ZLogger.Unity;

namespace TienLen.Infrastructure.Logging
{
    /// <summary>
    /// Configures ZLogger providers for Unity and exposes the logger factory to the app.
    /// </summary>
    public sealed class ZLoggerService : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ZLoggerService> _internalLogger;

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
            _internalLogger = _loggerFactory.CreateLogger<ZLoggerService>();

            _internalLogger.LogInformation("Logging initialized. logFile={logFile}", LogFilePath);
            UnityApplication.quitting += HandleApplicationQuitting;
        }

        public void Dispose()
        {
            UnityApplication.quitting -= HandleApplicationQuitting;
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
            var logDirectory = Path.Combine(UnityApplication.persistentDataPath, "logs");
            Directory.CreateDirectory(logDirectory);
            var fileName = $"tienlen_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            return Path.Combine(logDirectory, fileName);
        }

        private void HandleApplicationQuitting()
        {
            Dispose();
        }

    }
}
