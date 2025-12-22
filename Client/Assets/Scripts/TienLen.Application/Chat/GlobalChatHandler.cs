using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;

namespace TienLen.Application.Chat
{
    /// <summary>
    /// Application service that manages the global chat channel lifecycle.
    /// </summary>
    public sealed class GlobalChatHandler : IDisposable
    {
        private const int DefaultBufferCapacity = 200;

        private readonly IChatNetworkClient _chatClient;
        private readonly IAuthenticationService _authService;
        private readonly ChatMessageBuffer _messageBuffer;
        private readonly ILogger<GlobalChatHandler> _logger;
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private bool _isConnected;

        /// <summary>
        /// Fired when a new chat message is received.
        /// </summary>
        public event Action<ChatMessageDto> MessageReceived;

        /// <summary>
        /// Snapshot of messages stored in the buffer.
        /// </summary>
        public IReadOnlyList<ChatMessageDto> CachedMessages => _messageBuffer.Messages;

        /// <summary>
        /// True if the handler has joined the global chat channel.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Creates a new global chat handler.
        /// </summary>
        /// <param name="chatClient">Network client used for chat operations.</param>
        /// <param name="authService">Authentication service for session validation.</param>
        /// <param name="logger">Logger for chat diagnostics.</param>
        public GlobalChatHandler(
            IChatNetworkClient chatClient,
            IAuthenticationService authService,
            ILogger<GlobalChatHandler> logger)
        {
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? NullLogger<GlobalChatHandler>.Instance;
            _messageBuffer = new ChatMessageBuffer(DefaultBufferCapacity);

            _chatClient.MessageReceived += HandleMessageReceived;
        }

        /// <summary>
        /// Ensures the global chat channel is joined for the current session.
        /// </summary>
        public async UniTask EnsureConnectedAsync()
        {
            if (_isConnected) return;

            await _connectLock.WaitAsync();
            try
            {
                if (_isConnected) return;
                if (!_authService.IsAuthenticated)
                {
                    throw new InvalidOperationException("Chat requires an authenticated session.");
                }

                await _chatClient.JoinGlobalChannelAsync();
                _isConnected = true;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// Sends a message to the global channel (auto-connects when needed).
        /// </summary>
        /// <param name="message">Message to send.</param>
        public async UniTask SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (!_isConnected)
            {
                await EnsureConnectedAsync();
            }

            await _chatClient.SendGlobalMessageAsync(message);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _chatClient.MessageReceived -= HandleMessageReceived;
            _connectLock.Dispose();
        }

        private void HandleMessageReceived(ChatMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message.Content)) return;
            _messageBuffer.Add(message);
            MessageReceived?.Invoke(message);
        }
    }
}
