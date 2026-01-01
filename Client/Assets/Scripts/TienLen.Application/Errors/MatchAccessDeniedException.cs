using System;

namespace TienLen.Application.Errors
{
    /// <summary>
    /// Raised when the server denies access to a match operation.
    /// </summary>
    public sealed class MatchAccessDeniedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatchAccessDeniedException"/> class.
        /// </summary>
        /// <param name="message">User-safe message describing the denial.</param>
        /// <param name="statusCode">Server status code for the denial.</param>
        public MatchAccessDeniedException(string message, long statusCode)
            : base(string.IsNullOrWhiteSpace(message) ? "Access denied." : message)
        {
            StatusCode = statusCode;
            AppCode = 0;
            Category = 0;
            Retryable = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchAccessDeniedException"/> class.
        /// </summary>
        /// <param name="message">User-safe message describing the denial.</param>
        /// <param name="statusCode">Server status code for the denial.</param>
        /// <param name="innerException">The underlying exception.</param>
        public MatchAccessDeniedException(string message, long statusCode, Exception innerException)
            : base(string.IsNullOrWhiteSpace(message) ? "Access denied." : message, innerException)
        {
            StatusCode = statusCode;
            AppCode = 0;
            Category = 0;
            Retryable = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchAccessDeniedException"/> class.
        /// </summary>
        /// <param name="appCode">Application error code.</param>
        /// <param name="category">Application error category.</param>
        /// <param name="statusCode">Server status code for the denial.</param>
        /// <param name="message">Local fallback message describing the denial.</param>
        /// <param name="retryable">True if the operation is retryable.</param>
        public MatchAccessDeniedException(int appCode, int category, long statusCode, string message, bool retryable)
            : this(appCode, category, statusCode, message, retryable, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchAccessDeniedException"/> class.
        /// </summary>
        /// <param name="appCode">Application error code.</param>
        /// <param name="category">Application error category.</param>
        /// <param name="statusCode">Server status code for the denial.</param>
        /// <param name="message">Local fallback message describing the denial.</param>
        /// <param name="retryable">True if the operation is retryable.</param>
        /// <param name="innerException">The underlying exception.</param>
        public MatchAccessDeniedException(
            int appCode,
            int category,
            long statusCode,
            string message,
            bool retryable,
            Exception innerException)
            : base(string.IsNullOrWhiteSpace(message) ? "Access denied." : message, innerException)
        {
            AppCode = appCode;
            Category = category;
            StatusCode = statusCode;
            Retryable = retryable;
        }

        /// <summary>
        /// Server status code for the denial.
        /// </summary>
        public long StatusCode { get; }

        /// <summary>
        /// Application error code.
        /// </summary>
        public int AppCode { get; }

        /// <summary>
        /// Application error category.
        /// </summary>
        public int Category { get; }

        /// <summary>
        /// True when the operation is retryable.
        /// </summary>
        public bool Retryable { get; }
    }
}
