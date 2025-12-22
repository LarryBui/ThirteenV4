using System;
using System.Collections.Generic;

namespace TienLen.Application.Chat
{
    /// <summary>
    /// Maintains a capped, ordered list of recent chat messages.
    /// </summary>
    public sealed class ChatMessageBuffer
    {
        private readonly int _capacity;
        private readonly List<ChatMessageDto> _messages;
        private readonly object _sync = new();

        /// <summary>
        /// Maximum number of messages held in memory.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Snapshot of buffered messages in chronological order.
        /// </summary>
        public IReadOnlyList<ChatMessageDto> Messages
        {
            get
            {
                lock (_sync)
                {
                    return _messages.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a buffer with the provided capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of messages to keep.</param>
        public ChatMessageBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }

            _capacity = capacity;
            _messages = new List<ChatMessageDto>(capacity);
        }

        /// <summary>
        /// Adds a message, trimming the oldest entries when over capacity.
        /// </summary>
        /// <param name="message">Message to store.</param>
        public void Add(ChatMessageDto message)
        {
            lock (_sync)
            {
                if (_messages.Count >= _capacity)
                {
                    _messages.RemoveAt(0);
                }

                _messages.Add(message);
            }
        }

        /// <summary>
        /// Clears all buffered messages.
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                _messages.Clear();
            }
        }
    }
}
