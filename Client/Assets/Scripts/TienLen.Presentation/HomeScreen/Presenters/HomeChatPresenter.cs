using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application.Chat;

namespace TienLen.Presentation.HomeScreen.Presenters
{
    /// <summary>
    /// Presenter for the Home Screen Chat.
    /// Mediates between HomeChatView and GlobalChatHandler.
    /// </summary>
    public sealed class HomeChatPresenter : IDisposable
    {
        private readonly GlobalChatHandler _chatHandler;
        private readonly ILogger<HomeChatPresenter> _logger;

        public event Action<ChatMessageDto> MessageReceived;

        public IEnumerable<ChatMessageDto> CachedMessages => _chatHandler?.CachedMessages ?? Array.Empty<ChatMessageDto>();

        public HomeChatPresenter(GlobalChatHandler chatHandler, ILogger<HomeChatPresenter> logger)
        {
            _chatHandler = chatHandler;
            _logger = logger ?? NullLogger<HomeChatPresenter>.Instance;

            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived += OnMessageReceived;
            }
        }

        public async void SendMessage(string text)
        {
            if (_chatHandler == null) return;
            try
            {
                await _chatHandler.SendMessageAsync(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message.");
            }
        }

        public void Dispose()
        {
            if (_chatHandler != null)
            {
                _chatHandler.MessageReceived -= OnMessageReceived;
            }
        }

        private void OnMessageReceived(ChatMessageDto message)
        {
            MessageReceived?.Invoke(message);
        }
    }
}
