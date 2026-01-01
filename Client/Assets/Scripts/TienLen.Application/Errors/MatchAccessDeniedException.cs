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
        }

        /// <summary>
        /// Server status code for the denial.
        /// </summary>
        public long StatusCode { get; }
    }
}
