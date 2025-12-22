using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nakama;
using Newtonsoft.Json;
using TienLen.Application.Chat;
using TienLen.Infrastructure.Services;

namespace TienLen.Infrastructure.Chat
{
    /// <summary>
    /// Nakama-backed implementation of <see cref="IChatNetworkClient"/> for global chat.
    /// </summary>
    public sealed class NakamaChatClient : IChatNetworkClient
    {
        private const string GlobalRoomName = "global";

        private readonly NakamaAuthenticationService _authService;
        private readonly ILogger<NakamaChatClient> _logger;

        private ISocket _subscribedSocket;
        private IChannel _channel;

        /// <inheritdoc />
        public event Action<ChatMessageDto> MessageReceived;

        /// <summary>
        /// Creates a new chat client bound to the Nakama auth service.
        /// </summary>
        /// <param name="authService">Authentication service used for socket access.</param>
        /// <param name="logger">Logger for chat diagnostics.</param>
        public NakamaChatClient(NakamaAuthenticationService authService, ILogger<NakamaChatClient> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? NullLogger<NakamaChatClient>.Instance;
        }

        private ISocket Socket => _authService.Socket;

        /// <inheritdoc />
        public async UniTask JoinGlobalChannelAsync()
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            EnsureSocketEventSubscriptions(Socket);

            if (_channel != null) return;

            var channel = await Socket.JoinChatAsync(GlobalRoomName, ChannelType.Room, persistence: false, hidden: false);
            if (channel == null)
            {
                throw new InvalidOperationException("Failed to join global chat channel.");
            }

            _channel = channel;
        }

        /// <inheritdoc />
        public async UniTask SendGlobalMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");
            if (_channel == null) throw new InvalidOperationException("Global chat channel is not joined.");

            var payload = JsonConvert.SerializeObject(new ChatPayload { Text = message.Trim() });
            await Socket.WriteChatMessageAsync(_channel, payload);
        }

        private void EnsureSocketEventSubscriptions(ISocket socket)
        {
            if (ReferenceEquals(_subscribedSocket, socket)) return;

            if (_subscribedSocket != null)
            {
                _subscribedSocket.ReceivedChannelMessage -= HandleChannelMessage;
            }

            _channel = null;
            socket.ReceivedChannelMessage += HandleChannelMessage;
            _subscribedSocket = socket;
        }

        private void HandleChannelMessage(IApiChannelMessage message)
        {
            HandleChannelMessageMainThread(message).Forget();
        }

        private async UniTaskVoid HandleChannelMessageMainThread(IApiChannelMessage message)
        {
            await UniTask.SwitchToMainThread();

            if (message == null) return;
            if (_channel != null && !string.IsNullOrWhiteSpace(message.ChannelId) && message.ChannelId != _channel.Id)
            {
                return;
            }

            var text = ExtractMessageText(message.Content);
            var createdAt = ParseTimestamp(message.CreateTime);

            var dto = new ChatMessageDto(
                message.MessageId,
                message.ChannelId,
                message.SenderId,
                message.Username,
                text,
                createdAt);

            MessageReceived?.Invoke(dto);
        }

        private static string ExtractMessageText(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            try
            {
                var payload = JsonConvert.DeserializeObject<ChatPayload>(content);
                if (!string.IsNullOrWhiteSpace(payload?.Text)) return payload.Text.Trim();
            }
            catch (JsonException)
            {
                // Fall back to raw content when payload is not JSON.
            }

            return content.Trim();
        }

        private DateTimeOffset ParseTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
            {
                return DateTimeOffset.UtcNow;
            }

            if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }

            _logger.LogWarning("Chat message timestamp could not be parsed. value={value}", timestamp);
            return DateTimeOffset.UtcNow;
        }

        private sealed class ChatPayload
        {
            [JsonProperty("text")]
            public string Text { get; set; }
        }
    }
}
