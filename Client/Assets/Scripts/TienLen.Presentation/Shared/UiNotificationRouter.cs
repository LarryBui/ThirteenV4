using System;
using System.Collections.Generic;
using TienLen.Application.Errors;
using TienLen.Presentation.GlobalMessage;
using Proto = Tienlen.V1;

namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Maps application errors and system events to user-facing notifications.
    /// </summary>
    public static class UiNotificationRouter
    {
        private const float InfoToastSeconds = 2.5f;
        private const float WarningToastSeconds = 4f;
        private const string UnknownErrorMessage = "An unexpected error occurred.";

        /// <summary>
        /// Creates a notification from a server error exception.
        /// </summary>
        /// <param name="exception">Server error exception to route.</param>
        /// <param name="title">Optional notification title.</param>
        public static UiNotification FromServerError(ServerErrorException exception, string title = "")
        {
            if (exception == null || exception.Error == null)
            {
                return CreateFallbackError(title);
            }

            var error = exception.Error;
            var appCode = error.AppCode;
            var category = GlobalMessageCatalog.ResolveCategory(appCode, error.Category);
            var displayMode = ResolveDisplayMode(GlobalMessageCatalog.ResolveDisplayMode(appCode));
            var severity = ResolveSeverity(category);
            var dedupeKey = ResolveErrorDedupeKey(appCode);
            var backgroundMode = ResolveBackgroundMode(displayMode);
            var autoDismiss = ResolveAutoDismissSeconds(displayMode, severity);
            var message = !string.IsNullOrWhiteSpace(error.Message)
                ? error.Message
                : GlobalMessageCatalog.ResolveMessage(appCode);

            return new UiNotification(
                severity,
                displayMode,
                message,
                title,
                dedupeKey,
                appCode,
                category,
                error.CorrelationId ?? string.Empty,
                backgroundMode,
                autoDismiss,
                actions: null,
                buttonSet: ResolveButtonSet(displayMode));
        }

        /// <summary>
        /// Creates a notification for connection status updates.
        /// </summary>
        /// <param name="message">User-facing status message.</param>
        /// <param name="isBlocking">Whether the status should block the UI.</param>
        public static UiNotification CreateConnectionStatus(string message, bool isBlocking)
        {
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "Connection issue." : message;
            var displayMode = isBlocking ? UiNotificationDisplayMode.Modal : UiNotificationDisplayMode.Toast;
            var severity = isBlocking ? UiNotificationSeverity.Error : UiNotificationSeverity.Info;

            return new UiNotification(
                severity,
                displayMode,
                normalizedMessage,
                string.Empty,
                "connection.status",
                null,
                null,
                string.Empty,
                ResolveBackgroundMode(displayMode),
                ResolveAutoDismissSeconds(displayMode, severity),
                actions: null,
                buttonSet: ResolveButtonSet(displayMode));
        }

        /// <summary>
        /// Creates a notification for matchmaking status updates.
        /// </summary>
        /// <param name="message">User-facing status message.</param>
        public static UiNotification CreateMatchmakingStatus(string message)
        {
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "Matchmaking in progress..." : message;

            return new UiNotification(
                UiNotificationSeverity.Info,
                UiNotificationDisplayMode.Toast,
                normalizedMessage,
                string.Empty,
                "matchmaking.status",
                null,
                null,
                string.Empty,
                UiBackgroundMode.None,
                InfoToastSeconds,
                actions: null,
                buttonSet: UiButtonSet.None);
        }

        /// <summary>
        /// Creates a notification for system announcements (e.g., maintenance).
        /// </summary>
        /// <param name="message">User-facing announcement message.</param>
        /// <param name="isBlocking">Whether the announcement should be fullscreen.</param>
        public static UiNotification CreateSystemAnnouncement(string message, bool isBlocking)
        {
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "System notice." : message;
            var displayMode = isBlocking ? UiNotificationDisplayMode.Fullscreen : UiNotificationDisplayMode.Toast;
            var severity = isBlocking ? UiNotificationSeverity.Fatal : UiNotificationSeverity.Info;

            return new UiNotification(
                severity,
                displayMode,
                normalizedMessage,
                string.Empty,
                "system.announcement",
                null,
                null,
                string.Empty,
                ResolveBackgroundMode(displayMode),
                ResolveAutoDismissSeconds(displayMode, severity),
                actions: null,
                buttonSet: ResolveButtonSet(displayMode));
        }

        private static UiNotification CreateFallbackError(string title)
        {
            return new UiNotification(
                UiNotificationSeverity.Error,
                UiNotificationDisplayMode.Modal,
                UnknownErrorMessage,
                title,
                "error.unknown",
                null,
                null,
                string.Empty,
                UiBackgroundMode.Blur,
                null,
                actions: null,
                buttonSet: UiButtonSet.RetryBack);
        }

        private static UiNotificationDisplayMode ResolveDisplayMode(MessageDisplayMode displayMode)
        {
            return displayMode switch
            {
                MessageDisplayMode.Modal => UiNotificationDisplayMode.Modal,
                MessageDisplayMode.Fullscreen => UiNotificationDisplayMode.Fullscreen,
                _ => UiNotificationDisplayMode.Toast
            };
        }

        private static UiNotificationSeverity ResolveSeverity(int category)
        {
            return (Proto.ErrorCategory)category switch
            {
                Proto.ErrorCategory.Validation => UiNotificationSeverity.Warning,
                Proto.ErrorCategory.NotFound => UiNotificationSeverity.Warning,
                Proto.ErrorCategory.Conflict => UiNotificationSeverity.Warning,
                Proto.ErrorCategory.Internal => UiNotificationSeverity.Fatal,
                Proto.ErrorCategory.Auth => UiNotificationSeverity.Error,
                Proto.ErrorCategory.Access => UiNotificationSeverity.Error,
                Proto.ErrorCategory.Transient => UiNotificationSeverity.Error,
                _ => UiNotificationSeverity.Error
            };
        }

        private static UiBackgroundMode ResolveBackgroundMode(UiNotificationDisplayMode displayMode)
        {
            return displayMode == UiNotificationDisplayMode.Toast
                ? UiBackgroundMode.None
                : UiBackgroundMode.Blur;
        }

        private static float? ResolveAutoDismissSeconds(UiNotificationDisplayMode displayMode, UiNotificationSeverity severity)
        {
            if (displayMode != UiNotificationDisplayMode.Toast) return null;

            return severity switch
            {
                UiNotificationSeverity.Info => InfoToastSeconds,
                UiNotificationSeverity.Warning => WarningToastSeconds,
                _ => WarningToastSeconds
            };
        }

        private static string ResolveErrorDedupeKey(int appCode)
        {
            return appCode > 0 ? $"error.{appCode}" : "error.unknown";
        }

        private static UiButtonSet ResolveButtonSet(UiNotificationDisplayMode displayMode)
        {
            return displayMode == UiNotificationDisplayMode.Toast
                ? UiButtonSet.None
                : UiButtonSet.RetryBack;
        }
    }
}
