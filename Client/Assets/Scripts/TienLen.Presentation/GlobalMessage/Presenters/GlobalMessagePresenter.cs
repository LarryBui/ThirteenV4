using System;
using TienLen.Presentation.Shared;

namespace TienLen.Presentation.GlobalMessage.Presenters
{
    /// <summary>
    /// Presenter for the global message overlay.
    /// </summary>
    public sealed class GlobalMessagePresenter : IDisposable
    {
        private readonly GlobalMessageHandler _handler;

        /// <summary>
        /// Raised when the global message snapshot changes.
        /// </summary>
        public event Action<GlobalMessageSnapshot> OnSnapshotChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalMessagePresenter"/> class.
        /// </summary>
        /// <param name="handler">Global message handler.</param>
        public GlobalMessagePresenter(GlobalMessageHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _handler.OnChanged += HandleChanged;
        }

        /// <summary>
        /// Unsubscribes from handler events.
        /// </summary>
        public void Dispose()
        {
            _handler.OnChanged -= HandleChanged;
        }

        /// <summary>
        /// Gets the latest global message snapshot.
        /// </summary>
        public GlobalMessageSnapshot GetSnapshot()
        {
            return _handler.GetSnapshot();
        }

        /// <summary>
        /// Informs the handler that the active toast was dismissed.
        /// </summary>
        public void DismissToast()
        {
            _handler.DismissActiveToast();
        }

        /// <summary>
        /// Forwards user actions to the handler.
        /// </summary>
        /// <param name="action">Action selected by the user.</param>
        public void RequestAction(UiActionKind action)
        {
            switch (action)
            {
                case UiActionKind.Retry:
                    _handler.RequestRetry();
                    break;
                case UiActionKind.Back:
                    _handler.RequestBack();
                    break;
                case UiActionKind.Close:
                    _handler.RequestClose();
                    break;
                case UiActionKind.Yes:
                    _handler.RequestYes();
                    break;
                case UiActionKind.No:
                    _handler.RequestNo();
                    break;
            }
        }

        private void HandleChanged(GlobalMessageSnapshot snapshot)
        {
            OnSnapshotChanged?.Invoke(snapshot);
        }
    }
}
