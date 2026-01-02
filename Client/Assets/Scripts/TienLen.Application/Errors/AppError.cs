namespace TienLen.Application.Errors
{
    /// <summary>
    /// Represents an application error raised from server responses and catalog mappings.
    /// </summary>
    public sealed class AppError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppError"/> class.
        /// </summary>
        /// <param name="appCode">Application error code.</param>
        /// <param name="category">Application error category.</param>
        /// <param name="outcome">Presentation outcome for the error.</param>
        /// <param name="message">User-safe message describing the error.</param>
        /// <param name="context">Optional context (operation or subsystem). Avoid PII.</param>
        /// <param name="correlationId">Optional correlation id for logs/traces.</param>
        public AppError(int appCode, int category, ErrorOutcome outcome, string message, string context = "", string correlationId = "")
        {
            AppCode = appCode;
            Category = category;
            Outcome = outcome;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
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
        /// Presentation outcome for the error.
        /// </summary>
        public ErrorOutcome Outcome { get; }

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
