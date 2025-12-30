using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application.Session;
using TienLen.Domain.Aggregates;
using TienLen.Domain.ValueObjects;

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
        private readonly ILogger<TienLenMatchHandler> _logger;

        public Match CurrentMatch { get; private set; }

        /// <summary>
        /// Raised after a server GameRoom snapshot has been applied to <see cref="CurrentMatch"/>.
        /// Use this to refresh GameRoom UI (seats/owner/player display info).
        /// </summary>
        public event Action GameRoomStateUpdated;

        /// <summary>
        /// Raised when a player joins the match.
        /// </summary>
        public event Action<int, string> PlayerJoined; // seat, userId

        /// <summary>
        /// Raised when a player leaves the match.
        /// </summary>
        public event Action<int, string> PlayerLeft; // seat, userId

        /// <summary>
        /// Raised when match presence changes (join/leave) with username info when available.
        /// </summary>
        public event Action<IReadOnlyList<PresenceChange>> MatchPresenceChanged;

        /// <summary>
        /// Raised when the game starts (gameplay begins).
        /// </summary>
        public event Action GameStarted;

        /// <summary>
        /// Raised when the board state updates (cards played or round reset).
        /// </summary>
        public event Action<int, bool> GameBoardUpdated;

        /// <summary>
        /// Raised when cards are played (used for UI logging).
        /// </summary>
        public event Action<int, IReadOnlyList<Card>> CardsPlayed; // seat, cards

        /// <summary>
        /// Raised when a turn is passed (used for UI logging).
        /// </summary>
        public event Action<int> TurnPassed; // seat

        /// <summary>
        /// Raised when the active turn countdown value changes.
        /// </summary>
        public event Action<int, long> TurnSecondsRemainingUpdated; // activeSeat, turnSecondsRemaining

        /// <summary>
        /// Raised when the server reports a gameplay error (e.g., invalid play).
        /// </summary>
        public event Action<int, string> GameErrorReceived;

        /// <summary>
        /// Raised when the game ends, providing the full result details.
        /// </summary>
        public event Action<GameEndedResultDto> GameEnded;

        /// <summary>
        /// Raised when player balances change (e.g. game settlement).
        /// </summary>
        public event Action<Dictionary<string, long>> MatchBalanceChanged;

        /// <summary>
        /// Raised when a player finishes their hand.
        /// </summary>
        public event Action<int, int> PlayerFinished; // seat, rank

        /// <summary>
        /// Raised when an in-game chat message is received.
        /// </summary>
        public event Action<int, string> InGameChatReceived; // seatIndex, message

        public TienLenMatchHandler(
            IMatchNetworkClient networkClient,
            IAuthenticationService authService,
            IGameSessionContext gameSessionContext,
            ILogger<TienLenMatchHandler> logger)
        {
            _networkClient = networkClient;
            _authService = authService;
            _gameSessionContext = gameSessionContext ?? throw new ArgumentNullException(nameof(gameSessionContext));
            _logger = logger ?? NullLogger<TienLenMatchHandler>.Instance;
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
                _logger.LogWarning(ex, "MatchHandler: LeaveMatchAsync failed.");
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
            await _networkClient.SendStartGameAsync();
            // Domain update happens on OnGameStarted event
        }

        /// <summary>
        /// Starts a rigged game using a predefined deck payload.
        /// </summary>
        /// <param name="request">Rigged deck request payload.</param>
        public async UniTask StartRiggedGameAsync(RiggedDeckRequestDto request)
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.Equals(CurrentMatch.Id, request.MatchId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Rigged deck match id does not match the current match.");
            }

            if (!RiggedDeckValidator.TryValidate(request, out var error))
            {
                throw new ArgumentException(error ?? "Rigged deck request is invalid.", nameof(request));
            }

            await _networkClient.SendStartGameTestAsync(request);
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

        public async UniTask SendInGameChatAsync(string message)
        {
            if (CurrentMatch == null) return;
            await _networkClient.SendInGameChatAsync(message);
        }

        // --- Event Handling (Network -> Domain) ---

        private void SubscribeToNetworkEvents()
        {
            _networkClient.OnCardsPlayed += HandleCardsPlayed;
            _networkClient.OnGameStarted += HandleGameStarted;
            _networkClient.OnPlayerJoinedOP += HandlePlayerJoinedOP;
            _networkClient.OnPlayerLeft += HandlePlayerLeft;
            _networkClient.OnTurnPassed += HandleTurnPassed;
            _networkClient.OnGameEnded += HandleGameEnded;
            _networkClient.OnGameError += HandleGameError;
            _networkClient.OnMatchPresenceChanged += HandleMatchPresenceChanged;
            _networkClient.OnInGameChatReceived += HandleInGameChatReceived;
            _networkClient.OnPlayerFinished += HandlePlayerFinished;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkClient.OnCardsPlayed -= HandleCardsPlayed;
            _networkClient.OnGameStarted -= HandleGameStarted;
            _networkClient.OnPlayerJoinedOP -= HandlePlayerJoinedOP;
            _networkClient.OnPlayerLeft -= HandlePlayerLeft;
            _networkClient.OnTurnPassed -= HandleTurnPassed;
            _networkClient.OnGameEnded -= HandleGameEnded;
            _networkClient.OnGameError -= HandleGameError;
            _networkClient.OnMatchPresenceChanged -= HandleMatchPresenceChanged;
            _networkClient.OnInGameChatReceived -= HandleInGameChatReceived;
            _networkClient.OnPlayerFinished -= HandlePlayerFinished;
        }

        private void HandlePlayerFinished(int seat, int rank)
        {
            PlayerFinished?.Invoke(seat, rank);
        }

        private void HandleInGameChatReceived(int seatIndex, string message)
        {
            InGameChatReceived?.Invoke(seatIndex, message);
        }

        private void HandleTurnPassed(int seat, int nextTurnSeat, bool newRound, long turnSecondsRemaining)
        {
            if (CurrentMatch == null) return;
            try
            {
                CurrentMatch.HandleTurnPassed(seat, nextTurnSeat, newRound);
                CurrentMatch.TurnSecondsRemaining = turnSecondsRemaining;
                GameRoomStateUpdated?.Invoke();
                GameBoardUpdated?.Invoke(seat, newRound);
                TurnSecondsRemainingUpdated?.Invoke(nextTurnSeat, turnSecondsRemaining);
                TurnPassed?.Invoke(seat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MatchHandler: Error applying TurnPassed.");
            }
        }

        private void HandleGameEnded(GameEndedResultDto result)
        {
            if (CurrentMatch == null) return;
            CurrentMatch.Phase = "Lobby";

            // FORK: 1. Update Session Balance
            var localUserId = _gameSessionContext.Identity.UserId;
            if (result.BalanceChanges != null && !string.IsNullOrEmpty(localUserId) && result.BalanceChanges.TryGetValue(localUserId, out var change))
            {
                var newBalance = _gameSessionContext.Identity.Balance + change;
                _gameSessionContext.SetIdentity(
                    _gameSessionContext.Identity.UserId,
                    _gameSessionContext.Identity.DisplayName,
                    _gameSessionContext.Identity.AvatarIndex,
                    newBalance
                );
            }

            // FORK: 2. Invoke Public Event (DTO payload)
            GameEnded?.Invoke(result);
            
            // FORK: 3. Invoke Economy Event (if needed)
            if (result.BalanceChanges != null && result.BalanceChanges.Count > 0)
            {
                MatchBalanceChanged?.Invoke(new Dictionary<string, long>((Dictionary<string, long>)result.BalanceChanges));
            }

            GameRoomStateUpdated?.Invoke();
        }

        private void HandleGameError(int code, string message)
        {
            _logger.LogWarning("MatchHandler: Game error received. code={code}, message={message}", code, message);
            GameErrorReceived?.Invoke(code, message);
        }

        private void HandleMatchPresenceChanged(IReadOnlyList<PresenceChange> changes)
        {
            if (CurrentMatch == null) return;
            if (changes == null || changes.Count == 0) return;
            MatchPresenceChanged?.Invoke(changes);
        }

        private void HandleCardsPlayed(int seat, List<Card> cards, int nextTurnSeat, bool newRound, long turnSecondsRemaining)
        {
            if (CurrentMatch == null) return;
            
            try
            {
                CurrentMatch.PlayTurn(seat, cards, nextTurnSeat, newRound);
                CurrentMatch.TurnSecondsRemaining = turnSecondsRemaining;
                GameRoomStateUpdated?.Invoke();
                GameBoardUpdated?.Invoke(seat, newRound);
                TurnSecondsRemainingUpdated?.Invoke(nextTurnSeat, turnSecondsRemaining);
                CardsPlayed?.Invoke(seat, cards ?? new List<Card>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MatchHandler: Error applying PlayTurn.");
            }
        }

        private void HandleGameStarted(List<Card> hand, int firstTurnSeat, long turnSecondsRemaining)
        {
            if (CurrentMatch == null) return;
            CurrentMatch.StartGame(firstTurnSeat);
            CurrentMatch.TurnSecondsRemaining = turnSecondsRemaining;
            
            // Deal cards to self
            var localUserId = _authService.CurrentUserId;
            if (CurrentMatch.Players.TryGetValue(localUserId, out var player))
            {
                player.Hand.Clear(); // Ensure empty before adding
                player.Hand.AddCards(hand);
            }

            GameRoomStateUpdated?.Invoke();
            GameStarted?.Invoke();
            GameBoardUpdated?.Invoke(firstTurnSeat, true);
            TurnSecondsRemainingUpdated?.Invoke(firstTurnSeat, turnSecondsRemaining);
        }

        private void HandlePlayerJoinedOP(MatchStateSnapshotDto snapshot)
        {
            if (CurrentMatch == null) return;

            var previousUsers = new HashSet<string>(CurrentMatch.Players.Keys);
            Array.Clear(CurrentMatch.Seats, 0, CurrentMatch.Seats.Length);
            var seatsToCopy = Math.Min(snapshot.Seats.Length, CurrentMatch.Seats.Length);
            Array.Copy(snapshot.Seats, CurrentMatch.Seats, seatsToCopy);
            CurrentMatch.OwnerSeat = snapshot.OwnerSeat;
            CurrentMatch.TurnSecondsRemaining = snapshot.TurnSecondsRemaining;

            ApplyGameRoomSnapshotToPlayers(snapshot);
            UpdateLocalSeatIndex();
            GameRoomStateUpdated?.Invoke();

            if (snapshot.Players != null)
            {
                foreach (var playerState in snapshot.Players)
                {
                    if (string.IsNullOrWhiteSpace(playerState.UserId)) continue;
                    if (!previousUsers.Contains(playerState.UserId))
                    {
                        PlayerJoined?.Invoke(playerState.Seat, playerState.UserId);
                    }
                }
            }
        }

        private void HandlePlayerLeft(int seat, string userId)
        {
            if (CurrentMatch == null) return;

            PlayerLeft?.Invoke(seat, userId);

            if (seat >= 0 && seat < CurrentMatch.Seats.Length)
            {
                if (CurrentMatch.Seats[seat] == userId || string.IsNullOrWhiteSpace(CurrentMatch.Seats[seat]))
                {
                    CurrentMatch.Seats[seat] = string.Empty;
                }
                else
                {
                    var resolvedSeat = FindSeatIndex(CurrentMatch.Seats, userId);
                    if (resolvedSeat >= 0)
                    {
                        CurrentMatch.Seats[resolvedSeat] = string.Empty;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                CurrentMatch.Players.Remove(userId);
            }

            GameRoomStateUpdated?.Invoke();
        }

        private void ApplyGameRoomSnapshotToPlayers(MatchStateSnapshotDto snapshot)
        {
            if (snapshot.Seats.Length > MaxPlayers)
            {
                _logger.LogWarning(
                    "MatchHandler: Server snapshot has {seatCount} seats, but GameRoom supports a maximum of {maxPlayers}. Extra seats will be ignored.",
                    snapshot.Seats.Length,
                    MaxPlayers);
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
                    player.Balance = pState.Balance;

                    // Sync local session balance if this is the local player
                    var localUserId = _gameSessionContext.Identity.UserId;
                    if (pState.UserId == localUserId)
                    {
                         _gameSessionContext.SetIdentity(
                            _gameSessionContext.Identity.UserId,
                            _gameSessionContext.Identity.DisplayName,
                            _gameSessionContext.Identity.AvatarIndex,
                            pState.Balance // Use authoritative balance from server
                        );
                    }

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
            // Cache the local seat on the match so the GameRoom can rely on CurrentMatch only.
            CurrentMatch.LocalSeatIndex = seatIndex;
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
