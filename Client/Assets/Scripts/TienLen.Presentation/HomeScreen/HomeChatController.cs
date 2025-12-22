using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using TienLen.Application.Chat;
using TienLen.Application.Speech;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.HomeScreen
{
    /// <summary>
    /// Builds and manages the Home screen global chat panel UI.
    /// </summary>
    public sealed class HomeChatController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform inputRoot;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button micButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Layout")]
        [SerializeField] private float panelWidthPercent = 0.33f;
        [SerializeField] private float panelHeightPercent = 0.5f;
        [SerializeField] private float inputWidthPercent = 0.6667f;
        [SerializeField] private float minInputWidth = 600f;
        [SerializeField] private Vector2 panelPadding = new(16f, 16f);
        [SerializeField] private float inputRowHeight = 72f;
        [SerializeField] private int maxVisibleMessages = 60;
        [SerializeField] private bool buildUiOnStart = true;

        private const float InnerGap = 8f;
        private const float StatusHeight = 24f;
        private const float SendButtonWidth = 96f;
        private const float MicButtonWidth = 72f;

        private IAuthenticationService _authService;
        private GlobalChatHandler _chatHandler;
        private ISpeechToTextService _speechService;
        private ILogger<HomeChatController> _logger;

        private readonly List<string> _messageLines = new();
        private readonly StringBuilder _stringBuilder = new();
        private CancellationTokenSource _speechTokenSource;
        private Vector2Int _lastScreenSize;

        /// <summary>
        /// Injects dependencies for the chat controller.
        /// </summary>
        /// <param name="authService">Authentication service for session state.</param>
        /// <param name="chatHandler">Application-level chat handler.</param>
        /// <param name="speechService">Speech-to-text service.</param>
        /// <param name="logger">Logger for chat UI diagnostics.</param>
        [Inject]
        public void Construct(
            IAuthenticationService authService,
            GlobalChatHandler chatHandler,
            ISpeechToTextService speechService,
            ILogger<HomeChatController> logger)
        {
            _authService = authService;
            _chatHandler = chatHandler;
            _speechService = speechService;
            _logger = logger ?? NullLogger<HomeChatController>.Instance;
        }

        private void Awake()
        {
            if (buildUiOnStart)
            {
                EnsureView();
            }
        }

        private void Start()
        {
            BindUiEvents();
            BindServiceEvents();
            RefreshMicState();
            RenderInitialMessages();

            if (_authService != null && _authService.IsAuthenticated)
            {
                ConnectChatAsync().Forget();
            }
        }

        private void Update()
        {
            if (panelRoot == null) return;

            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != _lastScreenSize)
            {
                _lastScreenSize = size;
                ApplyPanelSizing();
            }
        }

        private void OnDestroy()
        {
            UnbindUiEvents();
            UnbindServiceEvents();
            CancelSpeechCapture();
        }

        private void BindUiEvents()
        {
            if (sendButton != null) sendButton.onClick.AddListener(HandleSendClicked);
            if (micButton != null) micButton.onClick.AddListener(HandleMicClicked);
            if (inputField != null) inputField.onEndEdit.AddListener(HandleSubmit);
        }

        private void UnbindUiEvents()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(HandleSendClicked);
            if (micButton != null) micButton.onClick.RemoveListener(HandleMicClicked);
            if (inputField != null) inputField.onEndEdit.RemoveListener(HandleSubmit);
        }

        private void BindServiceEvents()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated += HandleAuthenticated;
                _authService.OnAuthenticationFailed += HandleAuthFailed;
            }

            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived += HandleMessageReceived;
            }
        }

        private void UnbindServiceEvents()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated -= HandleAuthenticated;
                _authService.OnAuthenticationFailed -= HandleAuthFailed;
            }

            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived -= HandleMessageReceived;
            }
        }

        private void HandleAuthenticated()
        {
            ConnectChatAsync().Forget();
        }

        private void HandleAuthFailed(string error)
        {
            SetStatus("Chat unavailable (auth failed).");
        }

        private async UniTaskVoid ConnectChatAsync()
        {
            if (_chatHandler == null) return;

            try
            {
                SetStatus("Connecting chat...");
                await _chatHandler.EnsureConnectedAsync();
                SetStatus(string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HomeChat: failed to connect.");
                SetStatus("Chat unavailable.");
            }
        }

        private void HandleMessageReceived(ChatMessageDto message)
        {
            var line = FormatMessage(message);
            if (string.IsNullOrWhiteSpace(line)) return;

            _messageLines.Add(line);
            var maxCount = Mathf.Max(1, maxVisibleMessages);
            if (_messageLines.Count > maxCount)
            {
                _messageLines.RemoveAt(0);
            }

            UpdateLogText();
        }

        private void RenderInitialMessages()
        {
            if (_chatHandler == null) return;
            var cached = _chatHandler.CachedMessages;
            if (cached == null || cached.Count == 0) return;

            _messageLines.Clear();
            foreach (var message in cached)
            {
                var line = FormatMessage(message);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _messageLines.Add(line);
                }
            }

            TrimVisibleMessages();
            UpdateLogText();
        }

        private void TrimVisibleMessages()
        {
            var maxCount = Mathf.Max(1, maxVisibleMessages);
            while (_messageLines.Count > maxCount)
            {
                _messageLines.RemoveAt(0);
            }
        }

        private string FormatMessage(ChatMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message.Content)) return string.Empty;

            var name = !string.IsNullOrWhiteSpace(message.SenderUsername)
                ? message.SenderUsername
                : CreateFallbackName(message.SenderId);
            var timestamp = message.CreatedAtUtc.ToLocalTime().ToString("HH:mm");
            return $"[{timestamp}] {name}: {message.Content}";
        }

        private static string CreateFallbackName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return "Player";
            return userId.Length <= 4 ? userId : userId.Substring(0, 4);
        }

        private static bool IsSubmitKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return true;
            }

            return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return true;
#endif
        }

        private void UpdateLogText()
        {
            if (logText == null) return;

            _stringBuilder.Clear();
            for (int i = 0; i < _messageLines.Count; i++)
            {
                if (i > 0) _stringBuilder.Append('\n');
                _stringBuilder.Append(_messageLines[i]);
            }

            logText.text = _stringBuilder.ToString();
        }

        private void HandleSendClicked()
        {
            SubmitMessage(inputField != null ? inputField.text : string.Empty).Forget();
        }

        private void HandleSubmit(string value)
        {
            if (!IsSubmitKeyPressed()) return;
            SubmitMessage(value).Forget();
        }

        private async UniTaskVoid SubmitMessage(string value)
        {
            if (_chatHandler == null) return;

            var message = value?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            try
            {
                await _chatHandler.SendMessageAsync(message);
                if (inputField != null)
                {
                    inputField.text = string.Empty;
                    inputField.ActivateInputField();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HomeChat: failed to send message.");
                SetStatus("Send failed.");
            }
        }

        private void HandleMicClicked()
        {
            CaptureSpeechAsync().Forget();
        }

        private async UniTaskVoid CaptureSpeechAsync()
        {
            if (_speechService == null || !_speechService.IsSupported)
            {
                SetStatus("Speech not supported.");
                return;
            }

            if (_chatHandler == null)
            {
                SetStatus("Chat unavailable.");
                return;
            }

            if (_speechService.IsListening)
            {
                return;
            }

            CancelSpeechCapture();
            _speechTokenSource = new CancellationTokenSource();

            try
            {
                SetMicInteractable(false);
                SetStatus("Listening...");
                var text = await _speechService.CaptureOnceAsync(_speechTokenSource.Token);
                SetStatus(string.Empty);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _chatHandler.SendMessageAsync(text.Trim());
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    return;
                }

                _logger.LogWarning(ex, "HomeChat: speech capture failed.");
                SetStatus(_speechService != null && !_speechService.IsSupported
                    ? "Enable Speech in Windows Settings > Privacy > Speech."
                    : "Speech failed.");
            }
            finally
            {
                DisposeSpeechToken();
                SetMicInteractable(true);
                if (inputField != null)
                {
                    inputField.ActivateInputField();
                }
            }
        }

        private void CancelSpeechCapture()
        {
            if (_speechTokenSource == null) return;

            _speechTokenSource.Cancel();
            DisposeSpeechToken();
        }

        private void DisposeSpeechToken()
        {
            if (_speechTokenSource == null) return;
            _speechTokenSource.Dispose();
            _speechTokenSource = null;
        }

        private void RefreshMicState()
        {
            if (micButton == null) return;
            micButton.interactable = _speechService != null && _speechService.IsSupported;
        }

        private void SetMicInteractable(bool interactable)
        {
            if (micButton == null) return;
            micButton.interactable = interactable && _speechService != null && _speechService.IsSupported;
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
        }

        private void EnsureView()
        {
            if (panelRoot != null) return;

            // Prefer the Home scene canvas to avoid attaching to additive scenes.
            Canvas canvas = null;
            var scene = gameObject.scene;
            var rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                canvas = rootObjects[i].GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    break;
                }
            }
            if (canvas == null)
            {
                _logger.LogError("HomeChat: Canvas not found; chat UI cannot be built.");
                return;
            }

            var panelObject = new GameObject("ChatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.layer = canvas.gameObject.layer;
            panelRoot = panelObject.GetComponent<RectTransform>();
            panelRoot.SetParent(canvas.transform, false);
            panelRoot.anchorMin = new Vector2(0f, 1f);
            panelRoot.anchorMax = new Vector2(0f, 1f);
            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchoredPosition = new Vector2(panelPadding.x, -panelPadding.y);

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.6f);

            inputRoot = CreateRect("InputRow", canvas.transform as RectTransform);
            inputRoot.anchorMin = new Vector2(0.5f, 0f);
            inputRoot.anchorMax = new Vector2(0.5f, 0f);
            inputRoot.pivot = new Vector2(0.5f, 0f);
            inputRoot.anchoredPosition = new Vector2(0f, panelPadding.y);
            inputRoot.sizeDelta = new Vector2(0f, inputRowHeight);

            micButton = CreateButton("Btn_Mic", inputRoot, "Mic", MicButtonWidth);
            var micRect = micButton.GetComponent<RectTransform>();
            micRect.anchorMin = new Vector2(1f, 0f);
            micRect.anchorMax = new Vector2(1f, 1f);
            micRect.pivot = new Vector2(1f, 0.5f);
            micRect.sizeDelta = new Vector2(MicButtonWidth, 0f);
            micRect.anchoredPosition = new Vector2(-InnerGap, 0f);

            sendButton = CreateButton("Btn_Send", inputRoot, "Send", SendButtonWidth);
            var sendRect = sendButton.GetComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(1f, 0f);
            sendRect.anchorMax = new Vector2(1f, 1f);
            sendRect.pivot = new Vector2(1f, 0.5f);
            sendRect.sizeDelta = new Vector2(SendButtonWidth, 0f);
            sendRect.anchoredPosition = new Vector2(-(MicButtonWidth + InnerGap * 2f), 0f);

            inputField = CreateInputField("InputField", inputRoot, SendButtonWidth + MicButtonWidth + InnerGap * 3f);

            statusText = CreateText("StatusText", panelRoot, 20, new Color(1f, 1f, 1f, 0.85f));
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.offsetMin = new Vector2(panelPadding.x, panelPadding.y + InnerGap);
            statusRect.offsetMax = new Vector2(-panelPadding.x, panelPadding.y + InnerGap + StatusHeight);
            statusText.alignment = TextAlignmentOptions.Left;

            logText = CreateText("ChatLog", panelRoot, 22, Color.white);
            var logRect = logText.GetComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0f, 0f);
            logRect.anchorMax = new Vector2(1f, 1f);
            var logMinY = panelPadding.y + StatusHeight + InnerGap * 2f;
            logRect.offsetMin = new Vector2(panelPadding.x, logMinY);
            logRect.offsetMax = new Vector2(-panelPadding.x, -panelPadding.y);
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.enableWordWrapping = true;

            ApplyPanelSizing();
        }

        private void ApplyPanelSizing()
        {
            if (panelRoot == null) return;
            if (panelRoot.parent is not RectTransform parentRect) return;

            var width = parentRect.rect.width * Mathf.Clamp01(panelWidthPercent);
            var height = parentRect.rect.height * Mathf.Clamp01(panelHeightPercent);
            panelRoot.sizeDelta = new Vector2(width, height);
            panelRoot.anchoredPosition = new Vector2(panelPadding.x, -panelPadding.y);

            if (inputRoot == null) return;
            if (inputRoot.parent is not RectTransform inputParent) return;

            var maxWidth = Mathf.Max(0f, inputParent.rect.width - panelPadding.x * 2f);
            var targetWidth = inputParent.rect.width * Mathf.Clamp01(inputWidthPercent);
            var inputWidth = Mathf.Min(maxWidth, Mathf.Max(minInputWidth, targetWidth));

            inputRoot.sizeDelta = new Vector2(inputWidth, inputRowHeight);
            inputRoot.anchoredPosition = new Vector2(0f, panelPadding.y);
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private TMP_Text CreateText(string name, RectTransform parent, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            text.fontSize = fontSize;
            text.color = color;
            text.text = string.Empty;
            return text;
        }

        private Button CreateButton(string name, RectTransform parent, string label, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(width, 0f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.9f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelText = CreateText($"{name}_Label", rect, 20, new Color(0.1f, 0.1f, 0.1f, 1f));
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.text = label;

            return button;
        }

        private TMP_InputField CreateInputField(string name, RectTransform parent, float rightOffset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            go.layer = parent.gameObject.layer;

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(-rightOffset, 0f);

            var background = go.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.15f);

            var textArea = CreateRect("TextArea", rect);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(8f, 8f);
            textArea.offsetMax = new Vector2(-8f, -8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholder = CreateText("Placeholder", textArea, 20, new Color(1f, 1f, 1f, 0.4f));
            placeholder.text = "Type or speak...";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var text = CreateText("Text", textArea, 20, Color.white);
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 256;

            return input;
        }
    }
}
