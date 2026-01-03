namespace TienLen.Application.Errors
{
    /// <summary>
    /// Represents a server error payload for client-side handling.
    /// </summary>
    public sealed class ServerErrorDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerErrorDto"/> class.
        /// </summary>
        /// <param name="appCode">Application error code.</param>
        /// <param name="category">Application error category.</param>
        /// <param name="message">Server-provided message, if any.</param>
        /// <param name="retryable">Whether the error is retryable.</param>
        /// <param name="correlationId">Optional correlation id for logs/traces.</param>
        /// <param name="rawPayload">Raw server payload, if available.</param>
        public ServerErrorDto(
            int appCode,
            int category,
            string message,
            bool retryable,
            string correlationId = "",
            string rawPayload = "")
        {
            AppCode = appCode;
            Category = category;
            Message = message ?? string.Empty;
            Retryable = retryable;
            CorrelationId = correlationId ?? string.Empty;
            RawPayload = rawPayload ?? string.Empty;
        }

        /// <summary>
        /// Application error code.
        /// </summary>
        public int AppCode { get; }

        /// <summary>
        /// Application error category.
        /// </summary>
        public int Category { get; }

        /// <summary>
        /// Server-provided message, if any.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Whether the error is retryable.
        /// </summary>
        public bool Retryable { get; }

        /// <summary>
        /// Optional correlation id for logs/traces.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Raw server payload, if available.
        /// </summary>
        public string RawPayload { get; }
    }
}
