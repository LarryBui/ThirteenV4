using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnityEngine;
// Alias avoids collision with the TienLen.Application namespace.
using UnityApplication = UnityEngine.Application;
using ZLogger;
using ZLogger.Formatters;
using ZLogger.Unity;

namespace TienLen.Infrastructure.Logging
{
    /// <summary>
    /// Configures ZLogger providers for Unity and exposes the logger factory to the app.
    /// </summary>
    public sealed class ZLoggerService : IDisposable
    {
        private static readonly JsonEncodedText AppNameKey = JsonEncodedText.Encode("appName");
        private static readonly JsonEncodedText AppVersionKey = JsonEncodedText.Encode("appVersion");
        private static readonly JsonEncodedText BuildTypeKey = JsonEncodedText.Encode("buildType");
        private static readonly JsonEncodedText PlatformKey = JsonEncodedText.Encode("platform");
        private static readonly JsonEncodedText UnityVersionKey = JsonEncodedText.Encode("unityVersion");
        private static readonly JsonEncodedText DeviceModelKey = JsonEncodedText.Encode("deviceModel");
        private static readonly JsonEncodedText OperatingSystemKey = JsonEncodedText.Encode("operatingSystem");
        private static readonly JsonEncodedText SessionIdKey = JsonEncodedText.Encode("sessionId");
        private static readonly JsonEncodedText ThreadIdKey = JsonEncodedText.Encode("threadId");
        private static readonly JsonEncodedText ThreadNameKey = JsonEncodedText.Encode("threadName");
        private static readonly JsonEncodedText ThreadPoolKey = JsonEncodedText.Encode("threadPool");

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ZLoggerService> _internalLogger;
        private readonly LogEnvironment _environment;

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
            _environment = LogEnvironment.Create();
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
            builder.AddZLoggerUnityDebug(options =>
            {
                options.IncludeScopes = true;
                options.UsePlainTextFormatter();
            });
#endif

            builder.AddZLoggerFile(LogFilePath, options =>
            {
                options.IncludeScopes = true;
                options.CaptureThreadInfo = true;
                options.UseJsonFormatter(formatter =>
                {
                    formatter.IncludeProperties = IncludeProperties.All;
                    formatter.UseUtcTimestamp = true;
                    formatter.AdditionalFormatter = WriteEnvironmentFields;
                });
            });
        }

        private void WriteEnvironmentFields(Utf8JsonWriter writer, in LogInfo info)
        {
            _environment.WriteTo(writer);

            if (info.ThreadInfo.ThreadId >= 0)
            {
                writer.WriteNumber(ThreadIdKey, info.ThreadInfo.ThreadId);
                if (!string.IsNullOrWhiteSpace(info.ThreadInfo.ThreadName))
                {
                    writer.WriteString(ThreadNameKey, info.ThreadInfo.ThreadName);
                }
                writer.WriteBoolean(ThreadPoolKey, info.ThreadInfo.IsThreadPoolThread);
            }
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

        private readonly struct LogEnvironment
        {
            public readonly string AppName;
            public readonly string AppVersion;
            public readonly string BuildType;
            public readonly string Platform;
            public readonly string UnityVersion;
            public readonly string DeviceModel;
            public readonly string OperatingSystem;
            public readonly string SessionId;

            private LogEnvironment(
                string appName,
                string appVersion,
                string buildType,
                string platform,
                string unityVersion,
                string deviceModel,
                string operatingSystem,
                string sessionId)
            {
                AppName = appName;
                AppVersion = appVersion;
                BuildType = buildType;
                Platform = platform;
                UnityVersion = unityVersion;
                DeviceModel = deviceModel;
                OperatingSystem = operatingSystem;
                SessionId = sessionId;
            }

            public static LogEnvironment Create()
            {
                return new LogEnvironment(
                    UnityApplication.productName ?? "Unknown",
                    UnityApplication.version ?? "0.0.0",
                    ResolveBuildType(),
                    UnityApplication.platform.ToString(),
                    UnityApplication.unityVersion,
                    SystemInfo.deviceModel ?? "Unknown",
                    SystemInfo.operatingSystem ?? "Unknown",
                    Guid.NewGuid().ToString("N"));
            }

            public void WriteTo(Utf8JsonWriter writer)
            {
                writer.WriteString(AppNameKey, AppName);
                writer.WriteString(AppVersionKey, AppVersion);
                writer.WriteString(BuildTypeKey, BuildType);
                writer.WriteString(PlatformKey, Platform);
                writer.WriteString(UnityVersionKey, UnityVersion);
                writer.WriteString(DeviceModelKey, DeviceModel);
                writer.WriteString(OperatingSystemKey, OperatingSystem);
                writer.WriteString(SessionIdKey, SessionId);
            }

            private static string ResolveBuildType()
            {
                if (UnityApplication.isEditor)
                {
                    return "Editor";
                }

                return Debug.isDebugBuild ? "Development" : "Release";
            }
        }
    }
}
