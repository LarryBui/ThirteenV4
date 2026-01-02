using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using TienLen.Application;
using TienLen.Application.Session;
using TienLen.Application.Voice;
using TienLen.Domain.ValueObjects;

namespace TienLen.Application.Tests
{
    public sealed class MatchHandlerVivoxAuthTests
    {
        [Test]
        public void PlayerJoinedOp_RequestsVivoxTokenEachSnapshot()
        {
            var network = new FakeMatchNetworkClient();
            var auth = new FakeAuthService("user-1");
            var session = new GameSessionContext();
            var voice = new FakeVoiceChatService();

            using var handler = new TienLenMatchHandler(network, auth, session, voice, NullLogger<TienLenMatchHandler>.Instance);

            handler.JoinMatchAsync("match-1").Forget();

            network.RaisePlayerJoined(new MatchStateSnapshotDto
            {
                Seats = new[] { "user-1", "user-2", "", "" },
                OwnerSeat = 0,
                Players = new[]
                {
                    new PlayerStateDto { UserId = "user-1", Seat = 0, DisplayName = "P1", AvatarIndex = 0 },
                    new PlayerStateDto { UserId = "user-2", Seat = 1, DisplayName = "P2", AvatarIndex = 1 }
                }
            });

            Assert.That(voice.RequestCalls, Is.EqualTo(1));
            Assert.That(voice.LastMatchId, Is.EqualTo("match-1"));
            Assert.That(handler.VivoxAuthToken, Is.EqualTo("token-1"));

            network.RaisePlayerJoined(new MatchStateSnapshotDto
            {
                Seats = new[] { "user-1", "user-2", "", "" },
                OwnerSeat = 0,
                Players = new[]
                {
                    new PlayerStateDto { UserId = "user-1", Seat = 0, DisplayName = "P1", AvatarIndex = 0 },
                    new PlayerStateDto { UserId = "user-2", Seat = 1, DisplayName = "P2", AvatarIndex = 1 }
                }
            });

            Assert.That(voice.RequestCalls, Is.EqualTo(2));
        }

        private sealed class FakeMatchNetworkClient : IMatchNetworkClient
        {
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
            public UniTask<string> FindMatchAsync(int matchType = 0) => UniTask.FromResult("match-1");

            public void RaisePlayerJoined(MatchStateSnapshotDto snapshot)
            {
                OnPlayerJoinedOP?.Invoke(snapshot);
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
            public int RequestCalls { get; private set; }
            public string LastMatchId { get; private set; }

            public UniTask InitializeAsync() => UniTask.CompletedTask;
            public UniTask JoinChannelAsync(string matchId) => UniTask.CompletedTask;
            public UniTask LeaveChannelAsync() => UniTask.CompletedTask;

            public UniTask<string> RequestAuthTokenAsync(string matchId)
            {
                RequestCalls++;
                LastMatchId = matchId;
                return UniTask.FromResult($"token-{RequestCalls}");
            }
        }

    }
}
