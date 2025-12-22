using System;
using NUnit.Framework;
using TienLen.Application.Chat;

namespace TienLen.Application.Tests
{
    /// <summary>
    /// Tests for the chat message buffer behavior.
    /// </summary>
    public sealed class ChatMessageBufferTests
    {
        [Test]
        public void Add_WhenOverCapacity_RemovesOldestMessage()
        {
            var buffer = new ChatMessageBuffer(3);

            buffer.Add(CreateMessage("first"));
            buffer.Add(CreateMessage("second"));
            buffer.Add(CreateMessage("third"));
            buffer.Add(CreateMessage("fourth"));

            var messages = buffer.Messages;

            Assert.AreEqual(3, messages.Count);
            Assert.AreEqual("second", messages[0].Content);
            Assert.AreEqual("third", messages[1].Content);
            Assert.AreEqual("fourth", messages[2].Content);
        }

        [Test]
        public void Clear_RemovesAllMessages()
        {
            var buffer = new ChatMessageBuffer(2);

            buffer.Add(CreateMessage("one"));
            buffer.Add(CreateMessage("two"));
            buffer.Clear();

            Assert.AreEqual(0, buffer.Messages.Count);
        }

        private static ChatMessageDto CreateMessage(string content)
        {
            return new ChatMessageDto(
                Guid.NewGuid().ToString("N"),
                "global",
                "user",
                "User",
                content,
                DateTimeOffset.UtcNow);
        }
    }
}
