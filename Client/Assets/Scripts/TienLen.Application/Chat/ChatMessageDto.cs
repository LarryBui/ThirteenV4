using System;

namespace TienLen.Application.Chat
{
    /// <summary>
    /// Immutable chat message data transferred between the network and presentation layers.
    /// </summary>
    public readonly struct ChatMessageDto
    {
        /// <summary>Unique message identifier assigned by the chat backend.</summary>
        public string MessageId { get; }

        /// <summary>Chat channel identifier where the message was posted.</summary>
        public string ChannelId { get; }

        /// <summary>Sender user id for the message.</summary>
        public string SenderId { get; }

        /// <summary>Sender display name or username when available.</summary>
        public string SenderUsername { get; }

        /// <summary>Normalized message content.</summary>
        public string Content { get; }

        /// <summary>Timestamp (UTC) when the message was created.</summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// Creates a new chat message snapshot.
        /// </summary>
        public ChatMessageDto(
            string messageId,
            string channelId,
            string senderId,
            string senderUsername,
            string content,
            DateTimeOffset createdAtUtc)
        {
            MessageId = messageId ?? string.Empty;
            ChannelId = channelId ?? string.Empty;
            SenderId = senderId ?? string.Empty;
            SenderUsername = senderUsername ?? string.Empty;
            Content = content ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
        }
    }
}
