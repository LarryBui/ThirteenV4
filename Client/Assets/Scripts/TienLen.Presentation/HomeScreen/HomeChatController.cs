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
using TienLen.Application.Session;
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
        [SerializeField] private TMP_Text logText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button micButton;
        [Header("Settings")]
        [SerializeField] private int maxVisibleMessages = 60;

        private IAuthenticationService _authService;
        private GlobalChatHandler _chatHandler;
        private ISpeechToTextService _speechService;
        private IGameSessionContext _gameSessionContext;
        private ILogger<HomeChatController> _logger;

        private readonly List<string> _messageLines = new();
        private readonly StringBuilder _stringBuilder = new();
        private CancellationTokenSource _speechTokenSource;

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
            IGameSessionContext gameSessionContext,
            ILogger<HomeChatController> logger)
        {
            _authService = authService;
            _chatHandler = chatHandler;
            _speechService = speechService;
            _gameSessionContext = gameSessionContext;
            _logger = logger ?? NullLogger<HomeChatController>.Instance;
        }

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            BindUiEvents();
            BindServiceEvents();
            RefreshMicState();
            RenderInitialMessages();

            if (_authService != null && _authService.IsAuthenticated)
            {
                ConnectChatAsync().Forget();
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
            // Chat unavailable (auth failed).
        }

        private async UniTaskVoid ConnectChatAsync()
        {
            if (_chatHandler == null) return;

            try
            {
                await _chatHandler.EnsureConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HomeChat: failed to connect.");
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
            }
        }

        private void HandleMicClicked()
        {
            CaptureSpeechAsync().Forget();
        }

        private async UniTaskVoid CaptureSpeechAsync()
        {
            if (!IsSpeechAllowed())
            {
                return;
            }

            if (_chatHandler == null)
            {
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
                var text = await _speechService.CaptureOnceAsync(_speechTokenSource.Token);

                if (!string.IsNullOrWhiteSpace(text) && inputField != null)
                {
                    var speechText = text.Trim();
                    if (string.IsNullOrWhiteSpace(inputField.text))
                    {
                        inputField.text = speechText;
                    }
                    else
                    {
                        // Append to existing text with a space
                        inputField.text = $"{inputField.text.TrimEnd()} {speechText}";
                    }
                    
                    // Move caret to end for continued typing
                    inputField.caretPosition = inputField.text.Length;
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    return;
                }

                _logger.LogWarning(ex, "HomeChat: speech capture failed.");
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
            micButton.interactable = IsSpeechAllowed();
        }

        private void SetMicInteractable(bool interactable)
        {
            if (micButton == null) return;
            micButton.interactable = interactable && IsSpeechAllowed();
        }

        private bool IsSpeechAllowed()
        {
            if (_speechService == null || !_speechService.IsSupported)
            {
                return false;
            }

            return _gameSessionContext != null && _gameSessionContext.CurrentMatch.IsInMatch;
        }

        /// <summary>
        /// Validates that required UI references are assigned in the editor.
        /// </summary>
        /// <returns>True when all required references are present.</returns>
        private bool ValidateReferences()
        {
            var hasAllReferences = logText != null
                && inputField != null
                && sendButton != null
                && micButton != null;

            if (hasAllReferences)
            {
                return true;
            }

            if (_logger != null)
            {
                _logger.LogError("HomeChat: missing UI references. Assign them in the inspector.");
            }
            else
            {
                Debug.LogError("HomeChat: missing UI references. Assign them in the inspector.", this);
            }

            return false;
        }
    }
}
