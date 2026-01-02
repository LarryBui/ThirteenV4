using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Represents a catalog-mapped application error raised from server responses.
    /// </summary>
    public sealed class TienLenAppException : Exception
    {
        public TienLenAppException(
            int appCode,
            int category,
            ErrorOutcome outcome,
            string message,
            string context = "",
            Exception innerException = null)
            : base(message ?? string.Empty, innerException)
        {
            AppCode = appCode;
            Category = category;
            Outcome = outcome;
            Context = context ?? string.Empty;
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
        /// Optional context (operation or subsystem). Avoid PII.
        /// </summary>
        public string Context { get; }
    }
}
