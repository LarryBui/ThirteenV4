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

        public Match CurrentMatch { get; private set; }

        /// <summary>
        /// Raised after a server GameRoom snapshot has been applied to <see cref="CurrentMatch"/>.
        /// Use this to refresh GameRoom UI (seats/owner/player display info).
        /// </summary>
        public event Action GameRoomStateUpdated;
        
        /// <summary>
        /// Raised when the game starts (gameplay begins).
        /// </summary>
        public event Action GameStarted;

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

        /// <summary>
        /// Leaves the current match (best-effort), clears local match state, and allows the UI to return to Home.
        /// </summary>
        public async UniTask LeaveMatchAsync()
        {
            try
            {
                await _networkClient.SendLeaveMatchAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MatchHandler: LeaveMatchAsync failed: {ex.Message}");
            }
            finally
            {
                CurrentMatch = null;
                _gameSessionContext.ClearMatch();
            }
        }

        public async UniTask StartGameAsync()
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var userId = _authService?.CurrentUserId;
            var userIdShort = string.IsNullOrEmpty(userId) ? "<unknown>" : (userId.Length <= 8 ? userId : userId.Substring(0, 8));
            Debug.Log($"MatchHandler: Sending StartGame request (matchId={CurrentMatch.Id}, userId={userIdShort})");
#endif
            await _networkClient.SendStartGameAsync();
            // Domain update happens on OnGameStarted event
        }

        public async UniTask PassTurnAsync()
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            await _networkClient.SendPassTurnAsync();
        }

        public async UniTask PlayCardsAsync(List<Card> cards)
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            
            // 2. Send to network
            await _networkClient.SendPlayCardsAsync(cards);
        }

        // --- Event Handling (Network -> Domain) ---

        private void SubscribeToNetworkEvents()
        {
            _networkClient.OnCardsPlayed += HandleCardsPlayed;
            _networkClient.OnGameStarted += HandleGameStarted;
            _networkClient.OnPlayerJoinedOP += HandlePlayerJoinedOP;
            _networkClient.OnTurnPassed += HandleTurnPassed;
            _networkClient.OnGameEnded += HandleGameEnded;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkClient.OnCardsPlayed -= HandleCardsPlayed;
            _networkClient.OnGameStarted -= HandleGameStarted;
            _networkClient.OnPlayerJoinedOP -= HandlePlayerJoinedOP;
            _networkClient.OnTurnPassed -= HandleTurnPassed;
            _networkClient.OnGameEnded -= HandleGameEnded;
        }

        private void HandleTurnPassed(int seat, int nextTurnSeat, bool newRound)
        {
            if (CurrentMatch == null) return;
            try
            {
                // TODO: Update Domain Match to use seat index for SkipTurn logic
                // For now, we need to map seat to user ID if domain still uses UserID,
                // or update Domain to use Seat.
                // Assuming we update Domain Match to use SeatIndex as well.
                CurrentMatch.HandleTurnPassed(seat, nextTurnSeat, newRound);
                GameRoomStateUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Handler: Error applying TurnPassed: {ex.Message}");
            }
        }

        private void HandleGameEnded(List<int> finishOrderSeats)
        {
            if (CurrentMatch == null) return;
            CurrentMatch.Phase = "Finished";
            // TODO: Process finish order seats
            GameRoomStateUpdated?.Invoke();
        }

        private void HandleCardsPlayed(int seat, List<Card> cards, int nextTurnSeat, bool newRound)
        {
            if (CurrentMatch == null) return;
            
            try
            {
                CurrentMatch.PlayTurn(seat, cards, nextTurnSeat, newRound);
                GameRoomStateUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Handler: Error applying PlayTurn: {ex.Message}");
            }
        }

        private void HandleGameStarted(List<Card> hand, int firstTurnSeat)
        {
            if (CurrentMatch == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"MatchHandler: OnGameStarted received (matchId={CurrentMatch.Id}, handCount={(hand?.Count ?? 0)}, firstTurnSeat={firstTurnSeat})");
#endif
            CurrentMatch.StartGame(firstTurnSeat);
            
            // Deal cards to self
            var localUserId = _authService.CurrentUserId;
            if (CurrentMatch.Players.TryGetValue(localUserId, out var player))
            {
                player.Hand.Clear(); // Ensure empty before adding
                player.Hand.AddCards(hand);
            }

            GameRoomStateUpdated?.Invoke();
            GameStarted?.Invoke();
        }

        private void HandlePlayerJoinedOP(MatchStateSnapshotDto snapshot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Handler op50: MatchStateSnapshot received: " + JsonConvert.SerializeObject(snapshot));
#endif
            if (CurrentMatch == null) return;

            Array.Clear(CurrentMatch.Seats, 0, CurrentMatch.Seats.Length);
            var seatsToCopy = Math.Min(snapshot.Seats.Length, CurrentMatch.Seats.Length);
            Array.Copy(snapshot.Seats, CurrentMatch.Seats, seatsToCopy);
            CurrentMatch.OwnerSeat = snapshot.OwnerSeat;

            ApplyGameRoomSnapshotToPlayers(snapshot);
            UpdateLocalSeatIndex();
            GameRoomStateUpdated?.Invoke();
        }

        private void ApplyGameRoomSnapshotToPlayers(MatchStateSnapshotDto snapshot)
        {
            if (snapshot.Seats.Length > MaxPlayers)
            {
                Debug.LogWarning($"Handler op50: Server snapshot has {snapshot.Seats.Length} seats, but GameRoom supports a maximum of {MaxPlayers} players. Extra seats will be ignored.");
            }

            var activeUserIds = new HashSet<string>();

            // The snapshot now contains authoritative PlayerState objects with names/avatars
            if (snapshot.Players != null)
            {
                foreach (var pState in snapshot.Players)
                {
                    if (string.IsNullOrEmpty(pState.UserId)) continue;

                    activeUserIds.Add(pState.UserId);

                    if (!CurrentMatch.Players.TryGetValue(pState.UserId, out var player))
                    {
                        player = new Player { UserID = pState.UserId };
                        CurrentMatch.Players.Add(pState.UserId, player);
                    }

                    player.Seat = (int)pState.Seat;
                    player.IsOwner = pState.IsOwner;
                    player.DisplayName = pState.DisplayName;
                    player.AvatarIndex = (int)pState.AvatarIndex;

                    if (string.IsNullOrWhiteSpace(player.DisplayName))
                    {
                        player.DisplayName = CreateFallbackDisplayName(pState.UserId);
                    }
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
    }
}
