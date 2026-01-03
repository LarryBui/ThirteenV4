using System.Collections.Generic;
using TienLen.Presentation.Shared;
using Proto = Tienlen.V1;

namespace TienLen.Presentation.GlobalMessage
{
    /// <summary>
    /// Presentation catalog for mapping server error codes to global message defaults.
    /// </summary>
    public static class GlobalMessageCatalog
    {
        private static readonly IReadOnlyDictionary<int, GlobalMessageCatalogEntry> Entries =
            new Dictionary<int, GlobalMessageCatalogEntry>
            {
                {
                    (int)Proto.ErrorCode.Unspecified,
                    new GlobalMessageCatalogEntry(
                        (int)Proto.ErrorCode.Unspecified,
                        (int)Proto.ErrorCategory.Unspecified,
                        MessageDisplayMode.Toast,
                        "An unexpected error occurred.",
                        "Fallback when no specific server error code is provided.")
                },
                {
                    (int)Proto.ErrorCode.MatchVipRequired,
                    new GlobalMessageCatalogEntry(
                        (int)Proto.ErrorCode.MatchVipRequired,
                        (int)Proto.ErrorCategory.Access,
                        MessageDisplayMode.Modal,
                        "VIP status required to create or join VIP matches",
                        "User attempted to create or join a VIP match without membership.")
                }
            };

        public static bool TryGet(int appCode, out GlobalMessageCatalogEntry entry)
        {
            return Entries.TryGetValue(appCode, out entry);
        }

        public static string ResolveMessage(int appCode)
        {
            if (TryGet(appCode, out var entry))
            {
                return entry.Message;
            }

            if (TryGet((int)Proto.ErrorCode.Unspecified, out var fallback))
            {
                return fallback.Message;
            }

            return "An unexpected error occurred.";
        }

        public static int ResolveCategory(int appCode, int category)
        {
            if (category > 0) return category;

            if (TryGet(appCode, out var entry))
            {
                return entry.Category;
            }

            if (TryGet((int)Proto.ErrorCode.Unspecified, out var fallback))
            {
                return fallback.Category;
            }

            return category;
        }

        public static MessageDisplayMode ResolveDisplayMode(int appCode)
        {
            if (TryGet(appCode, out var entry))
            {
                return entry.DisplayMode;
            }

            if (TryGet((int)Proto.ErrorCode.Unspecified, out var fallback))
            {
                return fallback.DisplayMode;
            }

            return MessageDisplayMode.Toast;
        }
    }

    public sealed class GlobalMessageCatalogEntry
    {
        public GlobalMessageCatalogEntry(
            int code,
            int category,
            MessageDisplayMode displayMode,
            string message,
            string notes)
        {
            Code = code;
            Category = category;
            DisplayMode = displayMode;
            Message = message ?? string.Empty;
            Notes = notes ?? string.Empty;
        }

        public int Code { get; }
        public int Category { get; }
        public MessageDisplayMode DisplayMode { get; }
        public string Message { get; }
        public string Notes { get; }
    }
}
