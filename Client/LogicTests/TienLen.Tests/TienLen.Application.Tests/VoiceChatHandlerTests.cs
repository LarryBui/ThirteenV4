using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TienLen.Application.Voice;

namespace TienLen.Application.Tests
{
    public sealed class VoiceChatHandlerTests
    {
        [Test]
        public async UniTask EnsureGameRoomJoinedAsync_IsIdempotent()
        {
            var voiceService = new FakeVoiceChatService();
            using var handler = new VoiceChatHandler(voiceService, NullLogger<VoiceChatHandler>.Instance);

            await handler.EnsureGameRoomJoinedAsync("match-1");
            await handler.EnsureGameRoomJoinedAsync("match-1");

            Assert.That(voiceService.JoinCalls, Is.EqualTo(1));
            Assert.That(voiceService.LeaveCalls, Is.EqualTo(0));
            Assert.That(voiceService.LastMatchId, Is.EqualTo("match-1"));
        }

        [Test]
        public async UniTask EnsureGameRoomJoinedAsync_SwitchesChannel()
        {
            var voiceService = new FakeVoiceChatService();
            using var handler = new VoiceChatHandler(voiceService, NullLogger<VoiceChatHandler>.Instance);

            await handler.EnsureGameRoomJoinedAsync("match-1");
            await handler.EnsureGameRoomJoinedAsync("match-2");

            Assert.That(voiceService.JoinCalls, Is.EqualTo(2));
            Assert.That(voiceService.LeaveCalls, Is.EqualTo(1));
            Assert.That(voiceService.LastMatchId, Is.EqualTo("match-2"));
        }

        [Test]
        public async UniTask LeaveGameRoomAsync_IsIdempotent()
        {
            var voiceService = new FakeVoiceChatService();
            using var handler = new VoiceChatHandler(voiceService, NullLogger<VoiceChatHandler>.Instance);

            await handler.EnsureGameRoomJoinedAsync("match-1");
            await handler.LeaveGameRoomAsync();
            await handler.LeaveGameRoomAsync();

            Assert.That(voiceService.LeaveCalls, Is.EqualTo(1));
        }

        [Test]
        public void EnsureGameRoomJoinedAsync_RejectsEmptyMatchId()
        {
            var voiceService = new FakeVoiceChatService();
            using var handler = new VoiceChatHandler(voiceService, NullLogger<VoiceChatHandler>.Instance);

            Assert.ThrowsAsync<ArgumentException>(async () => await handler.EnsureGameRoomJoinedAsync(" "));
        }

        private sealed class FakeVoiceChatService : IVoiceChatService
        {
            public int JoinCalls { get; private set; }
            public int LeaveCalls { get; private set; }
            public string? LastMatchId { get; private set; }

            public event Action<string, string, bool> OnSpeechMessageReceived;
            public event Action<string, bool> OnParticipantSpeaking;

            public UniTask InitializeAsync() => UniTask.CompletedTask;

            public UniTask JoinChannelAsync(string matchId)
            {
                JoinCalls++;
                LastMatchId = matchId;
                return UniTask.CompletedTask;
            }

            public UniTask LeaveChannelAsync()
            {
                LeaveCalls++;
                return UniTask.CompletedTask;
            }

            public UniTask<string> RequestAuthTokenAsync(string matchId)
            {
                return UniTask.FromResult(string.Empty);
            }

            public UniTask EnableSpeechToTextAsync(bool active)
            {
                return UniTask.CompletedTask;
            }

            public UniTask MuteInputAsync(bool isMuted)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
