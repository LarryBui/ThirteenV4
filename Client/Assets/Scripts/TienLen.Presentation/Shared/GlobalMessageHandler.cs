using System;
using System.Collections.Generic;

namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Coordinates global messages (toast, modal, fullscreen) for the presentation layer.
    /// </summary>
    public sealed class GlobalMessageHandler
    {
        private const int ToastQueueLimit = 3;
        private static readonly TimeSpan ToastThrottleWindow = TimeSpan.FromSeconds(2);

        private readonly Queue<UiNotification> _toastQueue = new Queue<UiNotification>(ToastQueueLimit);
        private UiNotification _activeToast;
        private UiNotification _activeModal;
        private UiNotification _activeFullscreen;
        private DateTimeOffset _lastToastAt = DateTimeOffset.MinValue;
        private string _lastToastDedupeKey = string.Empty;

        /// <summary>
        /// Raised when the active message state changes.
        /// </summary>
        public event Action<GlobalMessageSnapshot> OnChanged;

        /// <summary>
        /// Raised when the user selects the Retry action.
        /// </summary>
        public event Action<UiNotification> RetryRequested;

        /// <summary>
        /// Raised when the user selects the Back action.
        /// </summary>
        public event Action<UiNotification> BackRequested;

        /// <summary>
        /// Gets the latest snapshot of active and queued messages.
        /// </summary>
        public GlobalMessageSnapshot GetSnapshot()
        {
            return new GlobalMessageSnapshot(
                _activeToast,
                _activeModal,
                _activeFullscreen,
                _toastQueue.ToArray());
        }

        /// <summary>
        /// Publishes a new message into the global queue.
        /// </summary>
        /// <param name="notification">Notification to publish.</param>
        public void Publish(UiNotification notification)
        {
            if (notification == null) return;

            switch (notification.DisplayMode)
            {
                case UiNotificationDisplayMode.Fullscreen:
                    PublishFullscreen(notification);
                    break;
                case UiNotificationDisplayMode.Modal:
                    PublishModal(notification);
                    break;
                default:
                    PublishToast(notification);
                    break;
            }
        }

        /// <summary>
        /// Dismisses the active toast and promotes the next queued toast if available.
        /// </summary>
        public void DismissActiveToast()
        {
            if (_activeToast == null) return;

            _activeToast = null;
            PromoteNextToast();
            RaiseChanged();
        }

        /// <summary>
        /// Dismisses the active modal or fullscreen message.
        /// </summary>
        public void DismissActiveBlocking()
        {
            if (_activeFullscreen != null)
            {
                _activeFullscreen = null;
                PromoteNextToast();
                RaiseChanged();
                return;
            }

            if (_activeModal != null)
            {
                _activeModal = null;
                PromoteNextToast();
                RaiseChanged();
            }
        }

        /// <summary>
        /// Requests a retry for the currently active blocking message.
        /// </summary>
        public void RequestRetry()
        {
            var active = _activeFullscreen ?? _activeModal;
            if (active != null)
            {
                RetryRequested?.Invoke(active);
                DismissActiveBlocking();
            }
        }

        /// <summary>
        /// Requests a back navigation for the currently active blocking message.
        /// </summary>
        public void RequestBack()
        {
            var active = _activeFullscreen ?? _activeModal;
            if (active != null)
            {
                BackRequested?.Invoke(active);
                DismissActiveBlocking();
            }
        }

        private void PublishFullscreen(UiNotification notification)
        {
            if (IsSameDedupeKey(_activeFullscreen, notification))
            {
                _activeFullscreen = notification;
                RaiseChanged();
                return;
            }

            _activeFullscreen = notification;
            _activeModal = null;
            _activeToast = null;
            _toastQueue.Clear();
            RaiseChanged();
        }

        private void PublishModal(UiNotification notification)
        {
            if (_activeFullscreen != null)
            {
                return;
            }

            if (IsSameDedupeKey(_activeModal, notification))
            {
                _activeModal = notification;
                RaiseChanged();
                return;
            }

            _activeModal = notification;
            _activeToast = null;
            _toastQueue.Clear();
            RaiseChanged();
        }

        private void PublishToast(UiNotification notification)
        {
            if (ShouldThrottleToast(notification))
            {
                return;
            }

            if (TryReplaceToast(notification))
            {
                TrackToast(notification);
                RaiseChanged();
                return;
            }

            if (_activeFullscreen == null && _activeModal == null && _activeToast == null)
            {
                _activeToast = notification;
                TrackToast(notification);
                RaiseChanged();
                return;
            }

            EnqueueToast(notification);
            TrackToast(notification);
            RaiseChanged();
        }

        private void PromoteNextToast()
        {
            if (_activeFullscreen != null || _activeModal != null) return;
            if (_activeToast != null) return;
            if (_toastQueue.Count == 0) return;

            _activeToast = _toastQueue.Dequeue();
        }

        private void EnqueueToast(UiNotification notification)
        {
            while (_toastQueue.Count >= ToastQueueLimit)
            {
                _toastQueue.Dequeue();
            }

            _toastQueue.Enqueue(notification);
        }

        private bool TryReplaceToast(UiNotification notification)
        {
            if (IsSameDedupeKey(_activeToast, notification))
            {
                _activeToast = notification;
                TrackToast(notification);
                return true;
            }

            if (string.IsNullOrWhiteSpace(notification.DedupeKey))
            {
                return false;
            }

            if (_toastQueue.Count == 0)
            {
                return false;
            }

            var replaced = false;
            var buffer = new Queue<UiNotification>(_toastQueue.Count);
            while (_toastQueue.Count > 0)
            {
                var current = _toastQueue.Dequeue();
                if (!replaced && IsSameDedupeKey(current, notification))
                {
                    buffer.Enqueue(notification);
                    replaced = true;
                }
                else
                {
                    buffer.Enqueue(current);
                }
            }

            while (buffer.Count > 0)
            {
                _toastQueue.Enqueue(buffer.Dequeue());
            }

            return replaced;
        }

        private bool ShouldThrottleToast(UiNotification notification)
        {
            if (string.IsNullOrWhiteSpace(notification.DedupeKey))
            {
                return false;
            }

            if (!string.Equals(notification.DedupeKey, _lastToastDedupeKey, StringComparison.Ordinal))
            {
                return false;
            }

            return DateTimeOffset.UtcNow - _lastToastAt < ToastThrottleWindow;
        }

        private void TrackToast(UiNotification notification)
        {
            _lastToastAt = DateTimeOffset.UtcNow;
            _lastToastDedupeKey = notification.DedupeKey ?? string.Empty;
        }

        private static bool IsSameDedupeKey(UiNotification current, UiNotification incoming)
        {
            if (current == null || incoming == null) return false;
            if (string.IsNullOrWhiteSpace(incoming.DedupeKey)) return false;
            return string.Equals(current.DedupeKey, incoming.DedupeKey, StringComparison.Ordinal);
        }

        private void RaiseChanged()
        {
            OnChanged?.Invoke(GetSnapshot());
        }
    }

    /// <summary>
    /// Snapshot of the current global message state.
    /// </summary>
    public sealed class GlobalMessageSnapshot
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalMessageSnapshot"/> class.
        /// </summary>
        /// <param name="activeToast">Active toast message.</param>
        /// <param name="activeModal">Active modal message.</param>
        /// <param name="activeFullscreen">Active fullscreen message.</param>
        /// <param name="queuedToasts">Queued toast messages.</param>
        public GlobalMessageSnapshot(
            UiNotification activeToast,
            UiNotification activeModal,
            UiNotification activeFullscreen,
            IReadOnlyList<UiNotification> queuedToasts)
        {
            ActiveToast = activeToast;
            ActiveModal = activeModal;
            ActiveFullscreen = activeFullscreen;
            QueuedToasts = queuedToasts ?? Array.Empty<UiNotification>();
        }

        /// <summary>
        /// Active toast message.
        /// </summary>
        public UiNotification ActiveToast { get; }

        /// <summary>
        /// Active modal message.
        /// </summary>
        public UiNotification ActiveModal { get; }

        /// <summary>
        /// Active fullscreen message.
        /// </summary>
        public UiNotification ActiveFullscreen { get; }

        /// <summary>
        /// Queued toast messages.
        /// </summary>
        public IReadOnlyList<UiNotification> QueuedToasts { get; }
    }
}
