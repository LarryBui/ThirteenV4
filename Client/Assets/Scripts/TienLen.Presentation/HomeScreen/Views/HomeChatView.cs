using System;
using System.Collections.Generic;
using TienLen.Application.Chat;
using TienLen.Presentation.HomeScreen.Presenters;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

namespace TienLen.Presentation.HomeScreen.Views
{
    /// <summary>
    /// View for the Home screen chat system.
    /// Handles UI references and events for global chat.
    /// </summary>
    public sealed class HomeChatView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform messageContainer;
        [SerializeField] private GameObject messagePrefab;

        private HomeChatPresenter _presenter;
        private readonly List<GameObject> _messageElements = new List<GameObject>();

        [Inject]
        public void Construct(HomeChatPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (sendButton) sendButton.onClick.AddListener(HandleSendClicked);
            if (messageInput) messageInput.onSubmit.AddListener(_ => HandleSendClicked());

            if (_presenter != null)
            {
                _presenter.MessageReceived += AddMessage;
                // Load existing history
                foreach (var msg in _presenter.CachedMessages)
                {
                    AddMessage(msg);
                }
            }
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.MessageReceived -= AddMessage;
                _presenter.Dispose();
            }
        }

        private void HandleSendClicked()
        {
            if (string.IsNullOrWhiteSpace(messageInput.text)) return;

            string text = messageInput.text;
            messageInput.text = "";

            _presenter?.SendMessage(text);
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
