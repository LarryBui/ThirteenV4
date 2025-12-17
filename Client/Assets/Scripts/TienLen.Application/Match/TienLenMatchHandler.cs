using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Application.Session;
using TienLen.Domain.Aggregates;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using Newtonsoft.Json;

namespace TienLen.Application
{
    /// <summary>
    /// Application service (Use Case Controller) that orchestrates the match flow.
    /// It bridges the UI (Presentation), the Game Logic (Domain), and the Network (Infrastructure).
    /// </summary>
    public class TienLenMatchHandler : IDisposable
    {
        private const int MaxPlayers = 4;

        private readonly IMatchNetworkClient _networkClient;
        private readonly IAuthenticationService _authService;
        private readonly IGameSessionContext _gameSessionContext; // Injected
        private readonly Dictionary<string, PlayerAvatar> _playerAvatarsByUserId = new();

        public Match CurrentMatch { get; private set; }

        /// <summary>
        /// Raised after a server GameRoom snapshot has been applied to <see cref="CurrentMatch"/>.
        /// Use this to refresh GameRoom UI (seats/owner/player display info).
        /// </summary>
        public event Action GameRoomStateUpdated;

        public TienLenMatchHandler(IMatchNetworkClient networkClient, IAuthenticationService authService, IGameSessionContext gameSessionContext)
        {
            _networkClient = networkClient;
            _authService = authService;
            _gameSessionContext = gameSessionContext ?? throw new ArgumentNullException(nameof(gameSessionContext));
            SubscribeToNetworkEvents();
        }

        public void Dispose()
        {
            UnsubscribeFromNetworkEvents();
        }

        // --- Use Cases ---

        public async UniTask FindAndJoinMatchAsync()
        {
            string matchId = await _networkClient.FindMatchAsync();
            await JoinMatchAsync(matchId);
        }

        public async UniTask JoinMatchAsync(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("Match id is required.", nameof(matchId));
            
            var previousMatch = CurrentMatch;
            var previousMatchId = _gameSessionContext.CurrentMatch.MatchId;
            var previousSeatIndex = _gameSessionContext.CurrentMatch.SeatIndex;
            var wasInMatch = _gameSessionContext.CurrentMatch.IsInMatch;

            try
            {
                // Initialize local state before awaiting the network join so we don't drop early snapshots.
                CurrentMatch = new Match(matchId, seatCount: MaxPlayers);
                _playerAvatarsByUserId.Clear();

                // Seat is unknown (-1) until confirmed by server snapshot.
                _gameSessionContext.SetMatch(matchId, -1);

                // Add self to match with a placeholder seat; server snapshot will correct this.
                var selfPlayer = new Player
                {
                    UserID = _authService.CurrentUserId,
                    DisplayName = _authService.CurrentUserDisplayName,
                    AvatarIndex = _authService.CurrentUserAvatarIndex,
                    Seat = 1
                };
                CurrentMatch.RegisterPlayer(selfPlayer);

                // Send request to network.
                await _networkClient.SendJoinMatchAsync(matchId);
            }
            catch
            {
                CurrentMatch = previousMatch;
                if (wasInMatch)
                {
                    _gameSessionContext.SetMatch(previousMatchId, previousSeatIndex);
                }
                else
                {
                    _gameSessionContext.ClearMatch();
                }

                throw;
            }
        }

        public async UniTask StartGameAsync()
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            await _networkClient.SendStartGameAsync();
            // Domain update happens on OnMatchStarted event
        }

        public async UniTask PlayCardsAsync(List<Card> cards)
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            
            // 1. Optimistic local validation (optional but good for UX)
            // CurrentMatch.PlayTurn(_authService.CurrentUserId, cards); 
            
            // 2. Send to network
            await _networkClient.SendPlayCardsAsync(cards);
        }

        // --- Event Handling (Network -> Domain) ---

        private void SubscribeToNetworkEvents()
        {
            _networkClient.OnPlayerJoined += HandlePlayerJoined;
            _networkClient.OnCardsPlayed += HandleCardsPlayed;
            _networkClient.OnGameStarted += HandleMatchStarted;
            _networkClient.OnPlayerJoinedOP += HandlePlayerJoinedOP;
            // ... other events
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkClient.OnPlayerJoined -= HandlePlayerJoined;
            _networkClient.OnCardsPlayed -= HandleCardsPlayed;
            _networkClient.OnGameStarted -= HandleMatchStarted;
            _networkClient.OnPlayerJoinedOP -= HandlePlayerJoinedOP;
        }

        private void HandlePlayerJoined(PlayerAvatar playerAvatar)
        {
            if (string.IsNullOrWhiteSpace(playerAvatar.UserId)) return;

            _playerAvatarsByUserId[playerAvatar.UserId] = playerAvatar;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Handler: PlayerJoined event payload: " + JsonConvert.SerializeObject(playerAvatar));
#endif

            if (CurrentMatch == null)
            {
                return;
            }

            if (CurrentMatch.Players.TryGetValue(playerAvatar.UserId, out var player))
            {
                if (!string.IsNullOrWhiteSpace(playerAvatar.DisplayName))
                {
                    player.DisplayName = playerAvatar.DisplayName;
                }

                player.AvatarIndex = playerAvatar.AvatarIndex;
            }
        }

        private void HandleCardsPlayed(string userId, List<Card> cards)
        {
            if (CurrentMatch == null) return;
            
            try
            {
                CurrentMatch.PlayTurn(userId, cards);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Handler: Error applying PlayTurn: {ex.Message}");
                // Sync error! Request full state?
            }
        }

        private void HandleMatchStarted()
        {
            if (CurrentMatch == null) return;
            CurrentMatch.DealCards();
        }

        private void HandlePlayerJoinedOP(MatchStateSnapshot snapshot)
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Handler op50: MatchStateSnapshot received: " + JsonConvert.SerializeObject(snapshot));
#endif
            if (CurrentMatch == null) return;

            Array.Clear(CurrentMatch.Seats, 0, CurrentMatch.Seats.Length);
            var seatsToCopy = Math.Min(snapshot.Seats.Length, CurrentMatch.Seats.Length);
            Array.Copy(snapshot.Seats, CurrentMatch.Seats, seatsToCopy);
            CurrentMatch.OwnerUserID = snapshot.OwnerId;

            ApplyGameRoomSnapshotToPlayers(snapshot);
            UpdateLocalSeatIndex();
            GameRoomStateUpdated?.Invoke();
        }

        private void ApplyGameRoomSnapshotToPlayers(MatchStateSnapshot snapshot)
        {
            if (snapshot.Seats.Length > MaxPlayers)
            {
                Debug.LogWarning($"Handler op50: Server snapshot has {snapshot.Seats.Length} seats, but GameRoom supports a maximum of {MaxPlayers} players. Extra seats will be ignored.");
            }

            var activeUserIds = new HashSet<string>();

            for (int seatIndex = 0; seatIndex < CurrentMatch.Seats.Length; seatIndex++)
            {
                var userId = CurrentMatch.Seats[seatIndex];
                if (string.IsNullOrEmpty(userId)) continue;

                activeUserIds.Add(userId);

                if (!CurrentMatch.Players.TryGetValue(userId, out var player))
                {
                    player = new Player { UserID = userId };
                    CurrentMatch.Players.Add(userId, player);
                }

                player.Seat = seatIndex + 1;
                player.IsOwner = userId == snapshot.OwnerId;

                if (_playerAvatarsByUserId.TryGetValue(userId, out var avatar))
                {
                    if (!string.IsNullOrWhiteSpace(avatar.DisplayName))
                    {
                        player.DisplayName = avatar.DisplayName;
                    }

                    player.AvatarIndex = avatar.AvatarIndex;
                }
                else if (userId == _authService.CurrentUserId)
                {
                    player.DisplayName = _authService.CurrentUserDisplayName;
                    player.AvatarIndex = _authService.CurrentUserAvatarIndex;
                }

                if (string.IsNullOrWhiteSpace(player.DisplayName))
                {
                    player.DisplayName = CreateFallbackDisplayName(userId);
                }

                if (!_playerAvatarsByUserId.ContainsKey(userId) && userId != _authService.CurrentUserId)
                {
                    player.AvatarIndex = GetFallbackAvatarIndex(userId, avatarCount: MaxPlayers);
                }
            }

            RemoveInactivePlayers(activeUserIds);
        }

        private void UpdateLocalSeatIndex()
        {
            var localUserId = _gameSessionContext.Identity.UserId;
            if (string.IsNullOrWhiteSpace(localUserId))
            {
                localUserId = _authService.CurrentUserId;
            }

            var seatIndex = FindSeatIndex(CurrentMatch.Seats, localUserId);
            _gameSessionContext.SetMatch(CurrentMatch.Id, seatIndex);
        }

        private void RemoveInactivePlayers(HashSet<string> activeUserIds)
        {
            if (CurrentMatch.Players.Count == 0) return;

            var toRemove = new List<string>();
            foreach (var entry in CurrentMatch.Players)
            {
                if (!activeUserIds.Contains(entry.Key))
                {
                    toRemove.Add(entry.Key);
                }
            }

            foreach (var userId in toRemove)
            {
                CurrentMatch.Players.Remove(userId);
            }
        }

        private static int FindSeatIndex(string[] seats, string userId)
        {
            if (seats == null || string.IsNullOrEmpty(userId)) return -1;

            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == userId) return i;
            }

            return -1;
        }

        private static string CreateFallbackDisplayName(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return "Player";
            var suffix = userId.Length <= 4 ? userId : userId.Substring(0, 4);
            return $"Player {suffix}";
        }

        private static int GetFallbackAvatarIndex(string userId, int avatarCount)
        {
            if (string.IsNullOrEmpty(userId) || avatarCount <= 0) return 0;

            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < userId.Length; i++)
                {
                    hash ^= userId[i];
                    hash *= 16777619;
                }

                return (int)(hash % (uint)avatarCount);
            }
        }
    }
}
