using System;
using Cysharp.Threading.Tasks;

namespace TienLen.Application.Chat
{
    /// <summary>
    /// Defines chat network operations used by the application layer.
    /// </summary>
    public interface IChatNetworkClient
    {
        /// <summary>
        /// Fired when a chat message is received from the backend.
        /// </summary>
        event Action<ChatMessageDto> MessageReceived;

        /// <summary>
        /// Joins the global chat channel for the current session.
        /// </summary>
        UniTask JoinGlobalChannelAsync();

        /// <summary>
        /// Sends a message to the global chat channel.
        /// </summary>
        /// <param name="message">Message content to send.</param>
        UniTask SendGlobalMessageAsync(string message);
    }
}
