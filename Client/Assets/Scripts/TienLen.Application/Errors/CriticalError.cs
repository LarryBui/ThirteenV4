namespace TienLen.Application.Errors
{
    /// <summary>
    /// Represents a critical application error that should trigger user-facing recovery.
    /// </summary>
    public sealed class CriticalError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CriticalError"/> class.
        /// </summary>
        /// <param name="code">Short error code for grouping.</param>
        /// <param name="message">User-safe message describing the error.</param>
        /// <param name="context">Optional context (operation or subsystem). Avoid PII.</param>
        /// <param name="correlationId">Optional correlation id for logs/traces.</param>
        public CriticalError(string code, string message, string context = "", string correlationId = "")
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }

        /// <summary>
        /// Short error code for grouping.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// User-safe message describing the error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Optional context (operation or subsystem). Avoid PII.
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// Optional correlation id for logs/traces.
        /// </summary>
        public string CorrelationId { get; }
    }
}
