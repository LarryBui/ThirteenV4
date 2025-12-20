using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TienLen.Application.Logging
{
    /// <summary>
    /// Provides access to structured application logging.
    /// </summary>
    public interface ILoggingService : IDisposable
    {
        /// <summary>
        /// Gets the full path of the active log file.
        /// </summary>
        string LogFilePath { get; }

        /// <summary>
        /// Creates a logger for the specified category name.
        /// </summary>
        /// <param name="categoryName">Logical category for the logger.</param>
        ILogger CreateLogger(string categoryName);

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        /// <typeparam name="T">Type used as the logger category.</typeparam>
        ILogger CreateLogger<T>();

        /// <summary>
        /// Begins a scope with structured fields applied to subsequent log entries.
        /// </summary>
        /// <param name="scopeValues">Key/value pairs to attach to the scope.</param>
        /// <returns>Disposable scope handle.</returns>
        IDisposable BeginScope(IReadOnlyDictionary<string, object> scopeValues);
    }
}
