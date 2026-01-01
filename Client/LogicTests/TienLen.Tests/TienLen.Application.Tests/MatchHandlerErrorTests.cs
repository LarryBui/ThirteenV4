using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TienLen.Application;
using TienLen.Application.Errors;
using TienLen.Application.Session;
using TienLen.Application.Voice;
using TienLen.Domain.ValueObjects;

namespace TienLen.Application.Tests
{
    public sealed class MatchHandlerErrorTests
    {
        [Test]
        public void FindAndJoinMatchAsync_PublishesCriticalError_WhenAccessDenied()
        {
            var exception = new MatchAccessDeniedException(
                appCode: 1001,
                category: 2,
                statusCode: 3,
                message: "VIP status required to create or join VIP matches",
                retryable: false);
            var network = new FakeMatchNetworkClient(exception);
            var auth = new FakeAuthService("user-1");
            var session = new GameSessionContext();
            var voice = new FakeVoiceChatService();
            var errorBus = new FakeErrorBus();

            using var handler = new TienLenMatchHandler(
                network,
                auth,
                session,
                voice,
                errorBus,
                NullLogger<TienLenMatchHandler>.Instance);

            var thrown = Assert.ThrowsAsync<MatchAccessDeniedException>(
                async () => await handler.FindAndJoinMatchAsync(2));

            Assert.That(thrown, Is.Not.Null);
            Assert.That(errorBus.PublishCount, Is.EqualTo(1));
            Assert.That(errorBus.LastError, Is.Not.Null);
            Assert.That(errorBus.LastError.AppCode, Is.EqualTo(1001));
            Assert.That(errorBus.LastError.Category, Is.EqualTo(2));
            Assert.That(errorBus.LastError.Message, Is.EqualTo(exception.Message));
            Assert.That(errorBus.LastError.Context, Is.EqualTo("find_match"));
        }

        private sealed class FakeMatchNetworkClient : IMatchNetworkClient
        {
            private readonly Exception _exception;

            public FakeMatchNetworkClient(Exception exception)
            {
                _exception = exception;
            }

            public event Action<int, List<Card>, int, bool, long> OnCardsPlayed;
            public event Action<int, int, bool, long> OnTurnPassed;
            public event Action<List<Card>, int, long> OnGameStarted;
            public event Action<MatchStateSnapshotDto> OnPlayerJoinedOP;
            public event Action<int, string> OnPlayerLeft;
            public event Action<IReadOnlyList<PresenceChange>> OnMatchPresenceChanged;
            public event Action<GameEndedResultDto> OnGameEnded;
            public event Action<int, int> OnPlayerFinished;
            public event Action<int, string> OnGameError;
            public event Action<int, string> OnInGameChatReceived;

            public UniTask SendJoinMatchAsync(string matchId) => UniTask.CompletedTask;
            public UniTask SendLeaveMatchAsync() => UniTask.CompletedTask;
            public UniTask SendStartGameAsync() => UniTask.CompletedTask;
            public UniTask SendStartGameTestAsync(RiggedDeckRequestDto request) => UniTask.CompletedTask;
            public UniTask SendPlayCardsAsync(List<Card> cards) => UniTask.CompletedTask;
            public UniTask SendPassTurnAsync() => UniTask.CompletedTask;
            public UniTask SendRequestNewGameAsync() => UniTask.CompletedTask;
            public UniTask SendInGameChatAsync(string message) => UniTask.CompletedTask;

            public UniTask<string> FindMatchAsync(int matchType = 0)
            {
                return UniTask.FromException<string>(_exception);
            }
        }

        private sealed class FakeAuthService : IAuthenticationService
        {
            public FakeAuthService(string userId)
            {
                CurrentUserId = userId;
                CurrentUserDisplayName = "Player";
                CurrentUserAvatarIndex = 0;
            }

            public bool IsAuthenticated => true;
            public string CurrentUserId { get; }
            public string CurrentUserDisplayName { get; }
            public int CurrentUserAvatarIndex { get; }
            public event Action OnAuthenticated;
            public event Action<string> OnAuthenticationFailed;
            public UniTask LoginAsync() => UniTask.CompletedTask;
        }

        private sealed class FakeVoiceChatService : IVoiceChatService
        {
            public UniTask InitializeAsync() => UniTask.CompletedTask;
            public UniTask JoinChannelAsync(string matchId) => UniTask.CompletedTask;
            public UniTask LeaveChannelAsync() => UniTask.CompletedTask;
            public UniTask<string> RequestAuthTokenAsync(string matchId) => UniTask.FromResult(string.Empty);
        }

        private sealed class FakeErrorBus : IAppErrorBus
        {
            public event Action<CriticalError> CriticalErrorPublished;
            public int PublishCount { get; private set; }
            public CriticalError LastError { get; private set; }

            public void Publish(CriticalError error)
            {
                PublishCount++;
                LastError = error;
                CriticalErrorPublished?.Invoke(error);
            }
        }
    }
}
