using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Exception representing a server error with its raw payload.
    /// </summary>
    public sealed class ServerErrorException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerErrorException"/> class.
        /// </summary>
        /// <param name="error">Server error payload.</param>
        /// <param name="message">User-facing error message.</param>
        /// <param name="context">Optional context (operation or subsystem).</param>
        /// <param name="innerException">Inner exception.</param>
        public ServerErrorException(
            ServerErrorDto error,
            string message,
            string context = "",
            Exception innerException = null)
            : base(string.IsNullOrWhiteSpace(message) ? error?.Message ?? string.Empty : message, innerException)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Context = context ?? string.Empty;
        }

        /// <summary>
        /// Server error payload.
        /// </summary>
        public ServerErrorDto Error { get; }

        /// <summary>
        /// Optional context (operation or subsystem).
        /// </summary>
        public string Context { get; }
    }
}
