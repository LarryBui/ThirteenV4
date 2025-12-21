using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TienLen.Application.Logging
{
    /// <summary>
    /// Provides logging helpers for serializing and logging object snapshots.
    /// </summary>
    public static class LoggingExtensions
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false
        };

        /// <summary>
        /// Serializes an object to JSON and logs it using the specified level and label.
        /// </summary>
        /// <param name="logger">Logger instance used to write the entry.</param>
        /// <param name="level">Log level for the entry.</param>
        /// <param name="label">Short label describing the payload.</param>
        /// <param name="value">Object to serialize and log.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        public static void LogObject(
            this ILogger logger,
            LogLevel level,
            string label,
            object value,
            JsonSerializerOptions options = null)
        {
            if (logger == null) return;
            if (!logger.IsEnabled(level)) return;

            var payload = SerializeObject(value, options ?? DefaultJsonOptions);
            logger.Log(level, "{label} {payload}", label, payload);
        }

        /// <summary>
        /// Serializes an object to JSON and logs it at Debug level.
        /// </summary>
        /// <param name="logger">Logger instance used to write the entry.</param>
        /// <param name="label">Short label describing the payload.</param>
        /// <param name="value">Object to serialize and log.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        public static void LogObjectDebug(
            this ILogger logger,
            string label,
            object value,
            JsonSerializerOptions options = null)
        {
            LogObject(logger, LogLevel.Debug, label, value, options);
        }

        private static string SerializeObject(object value, JsonSerializerOptions options)
        {
            if (value == null) return "null";

            try
            {
                return JsonSerializer.Serialize(value, options);
            }
            catch (Exception ex)
            {
                return $"<json-serialize-failed: {ex.GetType().Name}: {ex.Message}>";
            }
        }
    }
}
