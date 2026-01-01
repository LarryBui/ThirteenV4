using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application.Chat;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

namespace TienLen.Presentation
{
    /// <summary>
    /// Controller for the Home screen chat system.
    /// Handles UI references and events for global chat.
    /// </summary>
    public sealed class HomeChatController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform messageContainer;
        [SerializeField] private GameObject messagePrefab;

        private GlobalChatHandler _chatHandler;
        private ILogger<HomeChatController> _logger;
        private readonly List<GameObject> _messageElements = new List<GameObject>();

        [Inject]
        public void Construct(GlobalChatHandler chatHandler, ILogger<HomeChatController> logger)
        {
            _chatHandler = chatHandler;
            _logger = logger ?? NullLogger<HomeChatController>.Instance;
        }

        private void Start()
        {
            if (sendButton) sendButton.onClick.AddListener(HandleSendClicked);
            if (messageInput) messageInput.onSubmit.AddListener(_ => HandleSendClicked());

            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived += AddMessage;
                // Load existing history
                foreach (var msg in _chatHandler.CachedMessages)
                {
                    AddMessage(msg);
                }
            }
        }

        private void OnDestroy()
        {
            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived -= AddMessage;
            }
        }

        private async void HandleSendClicked()
        {
            if (string.IsNullOrWhiteSpace(messageInput.text)) return;

            string text = messageInput.text;
            messageInput.text = "";

            try
            {
                await _chatHandler.SendMessageAsync(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message.");
            }
        }

        private void AddMessage(ChatMessageDto message)
        {
            if (messagePrefab == null || messageContainer == null) return;

            var go = Instantiate(messagePrefab, messageContainer);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                text.text = $"<b>{message.SenderUsername}</b>: {message.Content}";
            }
            _messageElements.Add(go);

            // Simple auto-scroll
            Canvas.ForceUpdateCanvases();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 0;
        }
    }
}
