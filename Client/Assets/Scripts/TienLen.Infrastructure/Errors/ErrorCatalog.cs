using System.Collections.Generic;
using Proto = Tienlen.V1;

namespace TienLen.Infrastructure.Errors
{
    /// <summary>
    /// Client-side catalog for mapping server error codes to messages and notes.
    /// </summary>
    internal static class ErrorCatalog
    {
        private static readonly IReadOnlyDictionary<int, ErrorCatalogEntry> Entries =
            new Dictionary<int, ErrorCatalogEntry>
            {
                {
                    (int)Proto.ErrorCode.Unspecified,
                    new ErrorCatalogEntry(
                        (int)Proto.ErrorCode.Unspecified,
                        (int)Proto.ErrorCategory.Unspecified,
                        "An unexpected error occurred.",
                        "Fallback when no specific server error code is provided.")
                },
                {
                    (int)Proto.ErrorCode.MatchVipRequired,
                    new ErrorCatalogEntry(
                        (int)Proto.ErrorCode.MatchVipRequired,
                        (int)Proto.ErrorCategory.Access,
                        "VIP status required to create or join VIP matches",
                        "User attempted to create or join a VIP match without membership.")
                }
            };

        internal static bool TryGet(int appCode, out ErrorCatalogEntry entry)
        {
            return Entries.TryGetValue(appCode, out entry);
        }
    }

    internal sealed class ErrorCatalogEntry
    {
        public ErrorCatalogEntry(int code, int category, string message, string notes)
        {
            Code = code;
            Category = category;
            Message = message ?? string.Empty;
            Notes = notes ?? string.Empty;
        }

        public int Code { get; }
        public int Category { get; }
        public string Message { get; }
        public string Notes { get; }
    }
}
