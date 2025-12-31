using System;
using NUnit.Framework;
using TienLen.Application.Chat;

namespace TienLen.Application.Tests
{
    public sealed class ChatMessageBufferTests
    {
        [Test]
        public void AddMessage_MaintainsCapacity()
        {
            var buffer = new ChatMessageBuffer(3);
            var now = DateTimeOffset.UtcNow;
            buffer.Add(new ChatMessageDto("1", "c1", "u1", "user1", "first", now));
            buffer.Add(new ChatMessageDto("2", "c1", "u1", "user1", "second", now));
            buffer.Add(new ChatMessageDto("3", "c1", "u1", "user1", "third", now));
            buffer.Add(new ChatMessageDto("4", "c1", "u1", "user1", "fourth", now));

            var messages = buffer.Messages;
            Assert.That(messages.Count, Is.EqualTo(3));
            Assert.That(messages[0].Content, Is.EqualTo("second"));
            Assert.That(messages[1].Content, Is.EqualTo("third"));
            Assert.That(messages[2].Content, Is.EqualTo("fourth"));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var buffer = new ChatMessageBuffer(3);
            buffer.Add(new ChatMessageDto("1", "c1", "u1", "user1", "first", DateTimeOffset.UtcNow));
            
            buffer.Clear();
            
            Assert.That(buffer.Messages.Count, Is.EqualTo(0));
        }
    }
}
