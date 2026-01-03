using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using TienLen.Presentation.GlobalMessage.Presenters;
using TienLen.Presentation.Shared;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.GlobalMessage.Views
{
    /// <summary>
    /// View for displaying global messages (toast, modal, fullscreen).
    /// </summary>
    public sealed class GlobalMessageView : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas _canvas;

        [Header("Toast")]
        [SerializeField] private GameObject _toastRoot;
        [SerializeField] private TMP_Text _toastText;

        [Header("Modal")]
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private TMP_Text _modalTitle;
        [SerializeField] private TMP_Text _modalMessage;
        [SerializeField] private Button _modalPrimaryButton;
        [SerializeField] private TMP_Text _modalPrimaryLabel;
        [SerializeField] private Button _modalSecondaryButton;
        [SerializeField] private TMP_Text _modalSecondaryLabel;

        [Header("Fullscreen")]
        [SerializeField] private GameObject _fullscreenRoot;
        [SerializeField] private TMP_Text _fullscreenTitle;
        [SerializeField] private TMP_Text _fullscreenMessage;
        [SerializeField] private Button _fullscreenPrimaryButton;
        [SerializeField] private TMP_Text _fullscreenPrimaryLabel;
        [SerializeField] private Button _fullscreenSecondaryButton;
        [SerializeField] private TMP_Text _fullscreenSecondaryLabel;

        private GlobalMessagePresenter _presenter;
        private UiActionKind? _modalPrimaryAction;
        private UiActionKind? _modalSecondaryAction;
        private UiActionKind? _fullscreenPrimaryAction;
        private UiActionKind? _fullscreenSecondaryAction;

        /// <summary>
        /// Injects the presenter to ensure it is constructed.
        /// </summary>
        /// <param name="presenter">Global message presenter.</param>
        [Inject]
        public void Construct(GlobalMessagePresenter presenter)
        {
            _presenter = presenter;
        }

        private CancellationTokenSource _toastCts;
        private bool _eventsHooked;

        private void Awake()
        {
            EnsureUi();
            HideToast();
            HideModal();
            HideFullscreen();
        }

        private void Start()
        {
            if (_presenter == null)
            {
                Debug.LogError("[GlobalMessageView] Presenter not injected.");
                return;
            }

            _presenter.OnSnapshotChanged += ApplySnapshot;
            ApplySnapshot(_presenter.GetSnapshot());
        }

        private void OnDestroy()
        {
            CancelToastTimer();
            UnhookEvents();
            if (_presenter != null)
            {
                _presenter.OnSnapshotChanged -= ApplySnapshot;
                _presenter.Dispose();
            }
        }

        /// <summary>
        /// Displays a toast notification.
        /// </summary>
        /// <param name="notification">Notification to display.</param>
        public void ShowToast(UiNotification notification)
        {
            if (notification == null)
            {
                HideToast();
                return;
            }

            EnsureUi();
            if (_toastRoot != null) _toastRoot.SetActive(true);
            if (_toastText != null) _toastText.text = notification.Message ?? string.Empty;

            var duration = notification.AutoDismissSeconds ?? 2.5f;
            StartToastTimer(duration);
        }

        /// <summary>
        /// Hides the active toast notification.
        /// </summary>
        public void HideToast()
        {
            CancelToastTimer();
            if (_toastRoot != null) _toastRoot.SetActive(false);
        }

        /// <summary>
        /// Displays a modal notification.
        /// </summary>
        /// <param name="notification">Notification to display.</param>
        public void ShowModal(UiNotification notification)
        {
            if (notification == null)
            {
                HideModal();
                return;
            }

            EnsureUi();
            if (_modalRoot != null) _modalRoot.SetActive(true);
            ApplyPanel(
                notification,
                _modalTitle,
                _modalMessage,
                _modalPrimaryButton,
                _modalPrimaryLabel,
                _modalSecondaryButton,
                _modalSecondaryLabel,
                SetModalActions);
        }

        /// <summary>
        /// Hides the active modal notification.
        /// </summary>
        public void HideModal()
        {
            if (_modalRoot != null) _modalRoot.SetActive(false);
            SetModalActions(null, null);
        }

        /// <summary>
        /// Displays a fullscreen notification.
        /// </summary>
        /// <param name="notification">Notification to display.</param>
        public void ShowFullscreen(UiNotification notification)
        {
            if (notification == null)
            {
                HideFullscreen();
                return;
            }

            EnsureUi();
            if (_fullscreenRoot != null) _fullscreenRoot.SetActive(true);
            ApplyPanel(
                notification,
                _fullscreenTitle,
                _fullscreenMessage,
                _fullscreenPrimaryButton,
                _fullscreenPrimaryLabel,
                _fullscreenSecondaryButton,
                _fullscreenSecondaryLabel,
                SetFullscreenActions);
        }

        /// <summary>
        /// Hides the active fullscreen notification.
        /// </summary>
        public void HideFullscreen()
        {
            if (_fullscreenRoot != null) _fullscreenRoot.SetActive(false);
            SetFullscreenActions(null, null);
        }

        private void ApplyPanel(
            UiNotification notification,
            TMP_Text titleText,
            TMP_Text messageText,
            Button primaryButton,
            TMP_Text primaryLabel,
            Button secondaryButton,
            TMP_Text secondaryLabel,
            Action<UiActionKind?, UiActionKind?> storeActions)
        {
            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(notification.Title) ? "Notice" : notification.Title;
            }

            if (messageText != null)
            {
                messageText.text = notification.Message ?? string.Empty;
            }

            var actions = notification.ResolveActions();
            UiAction primaryAction = null;
            UiAction secondaryAction = null;

            if (actions != null && actions.Count > 0)
            {
                foreach (var action in actions)
                {
                    if (action == null) continue;
                    if (action.IsPrimary)
                    {
                        primaryAction = action;
                        break;
                    }
                }

                if (primaryAction == null)
                {
                    foreach (var action in actions)
                    {
                        if (action == null) continue;
                        primaryAction = action;
                        break;
                    }
                }

                if (primaryAction != null)
                {
                    foreach (var action in actions)
                    {
                        if (action == null || ReferenceEquals(action, primaryAction)) continue;
                        secondaryAction = action;
                        break;
                    }
                }
            }

            storeActions?.Invoke(primaryAction?.Kind, secondaryAction?.Kind);
            ConfigureButton(primaryButton, primaryLabel, primaryAction);
            ConfigureButton(secondaryButton, secondaryLabel, secondaryAction);
        }

        private void StartToastTimer(float seconds)
        {
            CancelToastTimer();

            if (seconds <= 0f)
            {
                return;
            }

            _toastCts = new CancellationTokenSource();
            ToastTimerAsync(seconds, _toastCts.Token).Forget();
        }

        private async UniTaskVoid ToastTimerAsync(float seconds, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested) return;

            HideToast();
            _presenter?.DismissToast();
        }

        private void CancelToastTimer()
        {
            if (_toastCts == null) return;
            _toastCts.Cancel();
            _toastCts.Dispose();
            _toastCts = null;
        }

        private void EnsureUi()
        {
            if (_canvas == null)
            {
                var canvasObject = new GameObject("GlobalMessageCanvas");
                canvasObject.transform.SetParent(transform, false);
                _canvas = canvasObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 5000;

                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (_toastRoot == null)
            {
                _toastRoot = CreateToastRoot(_canvas.transform, out _toastText);
            }

            if (_modalRoot == null)
            {
                _modalRoot = CreateBlockingPanel(
                    "ModalRoot",
                    _canvas.transform,
                    new Color(0f, 0f, 0f, 0.6f),
                    new Vector2(760f, 360f),
                    out _modalTitle,
                    out _modalMessage,
                    out _modalPrimaryButton,
                    out _modalPrimaryLabel,
                    out _modalSecondaryButton,
                    out _modalSecondaryLabel);
            }

            if (_fullscreenRoot == null)
            {
                _fullscreenRoot = CreateBlockingPanel(
                    "FullscreenRoot",
                    _canvas.transform,
                    new Color(0f, 0f, 0f, 0.8f),
                    new Vector2(900f, 420f),
                    out _fullscreenTitle,
                    out _fullscreenMessage,
                    out _fullscreenPrimaryButton,
                    out _fullscreenPrimaryLabel,
                    out _fullscreenSecondaryButton,
                    out _fullscreenSecondaryLabel);
            }

            HookEvents();
        }

        private void HookEvents()
        {
            if (_eventsHooked) return;
            _eventsHooked = true;

            if (_modalPrimaryButton != null) _modalPrimaryButton.onClick.AddListener(HandleModalPrimaryClicked);
            if (_modalSecondaryButton != null) _modalSecondaryButton.onClick.AddListener(HandleModalSecondaryClicked);

            if (_fullscreenPrimaryButton != null) _fullscreenPrimaryButton.onClick.AddListener(HandleFullscreenPrimaryClicked);
            if (_fullscreenSecondaryButton != null) _fullscreenSecondaryButton.onClick.AddListener(HandleFullscreenSecondaryClicked);
        }

        private void ApplySnapshot(GlobalMessageSnapshot snapshot)
        {
            if (snapshot == null)
            {
                HideToast();
                HideModal();
                HideFullscreen();
                return;
            }

            if (snapshot.ActiveFullscreen != null)
            {
                ShowFullscreen(snapshot.ActiveFullscreen);
            }
            else
            {
                HideFullscreen();
            }

            if (snapshot.ActiveModal != null)
            {
                ShowModal(snapshot.ActiveModal);
            }
            else
            {
                HideModal();
            }

            if (snapshot.ActiveToast != null)
            {
                ShowToast(snapshot.ActiveToast);
            }
            else
            {
                HideToast();
            }
        }

        private void UnhookEvents()
        {
            if (!_eventsHooked) return;
            _eventsHooked = false;

            if (_modalPrimaryButton != null) _modalPrimaryButton.onClick.RemoveAllListeners();
            if (_modalSecondaryButton != null) _modalSecondaryButton.onClick.RemoveAllListeners();
            if (_fullscreenPrimaryButton != null) _fullscreenPrimaryButton.onClick.RemoveAllListeners();
            if (_fullscreenSecondaryButton != null) _fullscreenSecondaryButton.onClick.RemoveAllListeners();
        }

        private void HandleModalPrimaryClicked()
        {
            if (!_modalPrimaryAction.HasValue) return;
            _presenter?.RequestAction(_modalPrimaryAction.Value);
        }

        private void HandleModalSecondaryClicked()
        {
            if (!_modalSecondaryAction.HasValue) return;
            _presenter?.RequestAction(_modalSecondaryAction.Value);
        }

        private void HandleFullscreenPrimaryClicked()
        {
            if (!_fullscreenPrimaryAction.HasValue) return;
            _presenter?.RequestAction(_fullscreenPrimaryAction.Value);
        }

        private void HandleFullscreenSecondaryClicked()
        {
            if (!_fullscreenSecondaryAction.HasValue) return;
            _presenter?.RequestAction(_fullscreenSecondaryAction.Value);
        }

        private void SetModalActions(UiActionKind? primary, UiActionKind? secondary)
        {
            _modalPrimaryAction = primary;
            _modalSecondaryAction = secondary;
        }

        private void SetFullscreenActions(UiActionKind? primary, UiActionKind? secondary)
        {
            _fullscreenPrimaryAction = primary;
            _fullscreenSecondaryAction = secondary;
        }

        private static void ConfigureButton(Button button, TMP_Text label, UiAction action)
        {
            if (button == null) return;

            var isActive = action != null;
            button.gameObject.SetActive(isActive);

            if (label != null)
            {
                label.text = action == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(action.Label)
                        ? ResolveDefaultLabel(action.Kind)
                        : action.Label;
            }
        }

        private static string ResolveDefaultLabel(UiActionKind kind)
        {
            return kind switch
            {
                UiActionKind.Retry => "Retry",
                UiActionKind.Back => "Back",
                UiActionKind.Close => "Close",
                UiActionKind.Yes => "Yes",
                UiActionKind.No => "No",
                _ => "OK"
            };
        }

        private static GameObject CreateToastRoot(Transform parent, out TMP_Text messageText)
        {
            var toastRoot = new GameObject("ToastRoot", typeof(RectTransform), typeof(Image));
            toastRoot.transform.SetParent(parent, false);
            var toastRect = toastRoot.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.1f, 0.9f);
            toastRect.anchorMax = new Vector2(0.9f, 0.98f);
            toastRect.offsetMin = Vector2.zero;
            toastRect.offsetMax = Vector2.zero;

            var toastImage = toastRoot.GetComponent<Image>();
            toastImage.color = new Color(0f, 0f, 0f, 0.75f);
            toastImage.raycastTarget = false;

            messageText = CreateText("ToastText", toastRoot.transform, 28, TextAlignmentOptions.Center);
            var textRect = messageText.rectTransform;
            textRect.anchorMin = new Vector2(0.05f, 0.1f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return toastRoot;
        }

        private static GameObject CreateBlockingPanel(
            string name,
            Transform parent,
            Color overlayColor,
            Vector2 panelSize,
            out TMP_Text titleText,
            out TMP_Text messageText,
            out Button primaryButton,
            out TMP_Text primaryLabel,
            out Button secondaryButton,
            out TMP_Text secondaryLabel)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootImage = root.GetComponent<Image>();
            rootImage.color = overlayColor;

            var panel = new GameObject(name + "Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            titleText = CreateText(name + "Title", panel.transform, 32, TextAlignmentOptions.Center);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.05f, 0.7f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            messageText = CreateText(name + "Message", panel.transform, 26, TextAlignmentOptions.Center);
            var messageRect = messageText.rectTransform;
            messageRect.anchorMin = new Vector2(0.05f, 0.35f);
            messageRect.anchorMax = new Vector2(0.95f, 0.7f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;

            primaryButton = CreateButton(name + "PrimaryButton", panel.transform, new Color(0.2f, 0.5f, 0.9f, 0.95f), out primaryLabel);
            var primaryRect = primaryButton.GetComponent<RectTransform>();
            primaryRect.anchorMin = new Vector2(0.55f, 0.1f);
            primaryRect.anchorMax = new Vector2(0.95f, 0.25f);
            primaryRect.offsetMin = Vector2.zero;
            primaryRect.offsetMax = Vector2.zero;

            secondaryButton = CreateButton(name + "SecondaryButton", panel.transform, new Color(0.3f, 0.3f, 0.3f, 0.95f), out secondaryLabel);
            var secondaryRect = secondaryButton.GetComponent<RectTransform>();
            secondaryRect.anchorMin = new Vector2(0.05f, 0.1f);
            secondaryRect.anchorMax = new Vector2(0.45f, 0.25f);
            secondaryRect.offsetMin = Vector2.zero;
            secondaryRect.offsetMax = Vector2.zero;

            return root;
        }

        private static Button CreateButton(string name, Transform parent, Color color, out TMP_Text label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            label = CreateText(name + "Label", buttonObject.transform, 24, TextAlignmentOptions.Center);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }
    }
}
