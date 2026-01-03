using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Severity classification for user-facing notifications.
    /// </summary>
    public enum UiNotificationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Fatal = 3
    }

    /// <summary>
    /// Display mode for user-facing notifications.
    /// </summary>
    public enum UiNotificationDisplayMode
    {
        Toast = 0,
        Modal = 1,
        Fullscreen = 2
    }

    /// <summary>
    /// Background rendering mode for overlay notifications.
    /// </summary>
    public enum UiBackgroundMode
    {
        Default = 0,
        None = 1,
        Blur = 2
    }

    /// <summary>
    /// Supported action types for overlay notifications.
    /// </summary>
    public enum UiActionKind
    {
        Retry = 0,
        Back = 1,
        Close = 2,
        Yes = 3,
        No = 4
    }

    /// <summary>
    /// Standard button set presets for notifications.
    /// </summary>
    public enum UiButtonSet
    {
        None = 0,
        CloseOnly = 1,
        YesNo = 2,
        RetryBack = 3,
        Custom = 4
    }

    /// <summary>
    /// Represents a UI action choice for a notification.
    /// </summary>
    public sealed class UiAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UiAction"/> class.
        /// </summary>
        /// <param name="kind">Action kind.</param>
        /// <param name="label">User-facing label for the action.</param>
        /// <param name="isPrimary">Whether this action is the primary choice.</param>
        public UiAction(UiActionKind kind, string label, bool isPrimary = false)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            IsPrimary = isPrimary;
        }

        /// <summary>
        /// Action kind.
        /// </summary>
        public UiActionKind Kind { get; }

        /// <summary>
        /// User-facing label for the action.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Whether this action is the primary choice.
        /// </summary>
        public bool IsPrimary { get; }
    }

    /// <summary>
    /// Represents a user-facing notification routed through the global overlay.
    /// </summary>
    public sealed class UiNotification
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UiNotification"/> class.
        /// </summary>
        /// <param name="severity">Severity of the notification.</param>
        /// <param name="displayMode">Display mode for the notification.</param>
        /// <param name="message">User-facing message body.</param>
        /// <param name="title">Optional title for the notification.</param>
        /// <param name="dedupeKey">Optional key for de-duplication.</param>
        /// <param name="appCode">Optional application error code.</param>
        /// <param name="category">Optional application error category.</param>
        /// <param name="correlationId">Optional correlation id for logs/traces.</param>
        /// <param name="backgroundMode">Background rendering mode.</param>
        /// <param name="autoDismissSeconds">Optional auto-dismiss duration in seconds.</param>
        /// <param name="actions">Optional action list.</param>
        /// <param name="buttonSet">Optional button set preset.</param>
        public UiNotification(
            UiNotificationSeverity severity,
            UiNotificationDisplayMode displayMode,
            string message,
            string title = "",
            string dedupeKey = "",
            int? appCode = null,
            int? category = null,
            string correlationId = "",
            UiBackgroundMode backgroundMode = UiBackgroundMode.Default,
            float? autoDismissSeconds = null,
            IReadOnlyList<UiAction> actions = null,
            UiButtonSet buttonSet = UiButtonSet.Custom)
        {
            if (autoDismissSeconds.HasValue && autoDismissSeconds.Value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(autoDismissSeconds), "Auto-dismiss seconds cannot be negative.");
            }

            Severity = severity;
            DisplayMode = displayMode;
            Message = message ?? string.Empty;
            Title = title ?? string.Empty;
            DedupeKey = dedupeKey ?? string.Empty;
            AppCode = appCode;
            Category = category;
            CorrelationId = correlationId ?? string.Empty;
            BackgroundMode = backgroundMode;
            AutoDismissSeconds = autoDismissSeconds;
            ButtonSet = buttonSet;
            Actions = actions == null || actions.Count == 0
                ? Array.Empty<UiAction>()
                : new ReadOnlyCollection<UiAction>(new List<UiAction>(actions));
        }

        /// <summary>
        /// Severity of the notification.
        /// </summary>
        public UiNotificationSeverity Severity { get; }

        /// <summary>
        /// Display mode for the notification.
        /// </summary>
        public UiNotificationDisplayMode DisplayMode { get; }

        /// <summary>
        /// Optional title for the notification.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// User-facing message body.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Optional key for de-duplication.
        /// </summary>
        public string DedupeKey { get; }

        /// <summary>
        /// Optional application error code.
        /// </summary>
        public int? AppCode { get; }

        /// <summary>
        /// Optional application error category.
        /// </summary>
        public int? Category { get; }

        /// <summary>
        /// Optional correlation id for logs/traces.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Background rendering mode.
        /// </summary>
        public UiBackgroundMode BackgroundMode { get; }

        /// <summary>
        /// Optional auto-dismiss duration in seconds.
        /// </summary>
        public float? AutoDismissSeconds { get; }

        /// <summary>
        /// Button set preset.
        /// </summary>
        public UiButtonSet ButtonSet { get; }

        /// <summary>
        /// Action list for the notification.
        /// </summary>
        public IReadOnlyList<UiAction> Actions { get; }

        /// <summary>
        /// Resolves the effective actions for this notification.
        /// </summary>
        public IReadOnlyList<UiAction> ResolveActions()
        {
            if (Actions != null && Actions.Count > 0)
            {
                return Actions;
            }

            return ButtonSet switch
            {
                UiButtonSet.CloseOnly => new[]
                {
                    new UiAction(UiActionKind.Close, "Close", isPrimary: true)
                },
                UiButtonSet.YesNo => new[]
                {
                    new UiAction(UiActionKind.Yes, "Yes", isPrimary: true),
                    new UiAction(UiActionKind.No, "No")
                },
                UiButtonSet.RetryBack => new[]
                {
                    new UiAction(UiActionKind.Retry, "Retry", isPrimary: true),
                    new UiAction(UiActionKind.Back, "Back")
                },
                _ => Array.Empty<UiAction>()
            };
        }
    }
}
