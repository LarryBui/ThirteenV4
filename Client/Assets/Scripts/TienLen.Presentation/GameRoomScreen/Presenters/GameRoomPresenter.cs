using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using TienLen.Domain.Aggregates;
using TienLen.Domain.Services;
using TienLen.Domain.ValueObjects;
using Cysharp.Threading.Tasks;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Presenter for the GameRoom.
    /// Handles interaction logic, state mapping, and event bridging from the Application layer (MatchHandler) to the View (Controller).
    /// </summary>
    public class GameRoomPresenter : IDisposable
    {
        private readonly TienLenMatchHandler _matchHandler;
        private readonly ILogger<GameRoomPresenter> _logger;

        public event Action OnStateUpdated;
        public event Action<int, IReadOnlyList<Card>> OnCardsPlayed;
        public event Action<int> OnTurnPassed;
        public event Action<int, bool> OnBoardUpdated;
        public event Action<int, long> OnTurnCountdownUpdated;
        public event Action<string> OnError;
        public event Action<IReadOnlyList<PresenceChange>> OnPresenceChanged;
        public event Action OnGameStarted;
        public event Action<List<int>, Dictionary<int, List<Card>>> OnGameEnded;

        // Expose current match for read-only binding in View
        public Match CurrentMatch => _matchHandler?.CurrentMatch;

        public GameRoomPresenter(
            TienLenMatchHandler matchHandler,
            ILogger<GameRoomPresenter> logger)
        {
            _matchHandler = matchHandler;
            _logger = logger ?? NullLogger<GameRoomPresenter>.Instance;

            Subscribe();
        }

        private void Subscribe()
        {
            if (_matchHandler == null) return;
            _matchHandler.GameRoomStateUpdated += HandleStateUpdated;
            _matchHandler.GameStarted += HandleGameStarted;
            _matchHandler.GameBoardUpdated += HandleBoardUpdated;
            _matchHandler.GameErrorReceived += HandleError;
            _matchHandler.CardsPlayed += HandleCardsPlayed;
            _matchHandler.TurnPassed += HandleTurnPassed;
            _matchHandler.TurnSecondsRemainingUpdated += HandleCountdown;
            _matchHandler.MatchPresenceChanged += HandlePresenceChanged;
            _matchHandler.GameEnded += HandleGameEnded;
        }

        public void Dispose()
        {
            if (_matchHandler == null) return;
            _matchHandler.GameRoomStateUpdated -= HandleStateUpdated;
            _matchHandler.GameStarted -= HandleGameStarted;
            _matchHandler.GameBoardUpdated -= HandleBoardUpdated;
            _matchHandler.GameErrorReceived -= HandleError;
            _matchHandler.CardsPlayed -= HandleCardsPlayed;
            _matchHandler.TurnPassed -= HandleTurnPassed;
            _matchHandler.TurnSecondsRemainingUpdated -= HandleCountdown;
            _matchHandler.MatchPresenceChanged -= HandlePresenceChanged;
            _matchHandler.GameEnded -= HandleGameEnded;
        }

        // --- Event Forwarding ---
        private void HandleStateUpdated() => OnStateUpdated?.Invoke();
        private void HandleGameStarted() => OnGameStarted?.Invoke();
        private void HandleBoardUpdated(int seat, bool newRound) => OnBoardUpdated?.Invoke(seat, newRound);
        private void HandleError(int code, string msg) => OnError?.Invoke(msg);
        private void HandleCardsPlayed(int seat, IReadOnlyList<Card> cards) => OnCardsPlayed?.Invoke(seat, cards);
        private void HandleTurnPassed(int seat) => OnTurnPassed?.Invoke(seat);
        private void HandleCountdown(int seat, long seconds) => OnTurnCountdownUpdated?.Invoke(seat, seconds);
        private void HandlePresenceChanged(IReadOnlyList<PresenceChange> changes) => OnPresenceChanged?.Invoke(changes);
        private void HandleGameEnded(List<int> finishOrder, Dictionary<int, List<Card>> remainingHands) => OnGameEnded?.Invoke(finishOrder, remainingHands);


        // --- Actions ---

        public void StartGame()
        {
             _matchHandler?.StartGameAsync().Forget();
        }

        public void PlayCards(List<Card> cards)
        {
             _matchHandler?.PlayCardsAsync(cards).Forget();
        }

        public void PassTurn()
        {
             _matchHandler?.PassTurnAsync().Forget();
        }

        public async UniTask LeaveMatchAsync()
        {
             if (_matchHandler != null) await _matchHandler.LeaveMatchAsync();
        }

        // --- View Logic Helpers ---

        public bool IsMyTurn()
        {
            var match = CurrentMatch;
            if (match == null || match.LocalSeatIndex < 0) return false;
            // CurrentTurnSeat is 0-based
            return match.CurrentTurnSeat == match.LocalSeatIndex;
        }

        public bool CanStartGame()
        {
             var match = CurrentMatch;
             if (match == null) return false;
             // Check if Lobby and Owner
             return string.Equals(match.Phase, "Lobby", StringComparison.OrdinalIgnoreCase) 
                    && match.OwnerSeat == match.LocalSeatIndex;
        }

        public bool CanPass()
        {
            return PlayValidator.CanPass(CurrentMatch?.CurrentBoard);
        }

        public bool HasPlayableMove(IReadOnlyList<Card> hand)
        {
             return PlayValidator.HasPlayableMove(hand, CurrentMatch?.CurrentBoard);
        }
        
        public PlayValidationResult ValidatePlay(IReadOnlyList<Card> selectedCards)
        {
             var match = CurrentMatch;
             if (match == null) return PlayValidationResult.Invalid(PlayValidationReason.NoSelection);
             
             // Get local hand
             if (!TryGetLocalHand(out var hand)) return PlayValidationResult.Invalid(PlayValidationReason.CardsNotInHand);
             
             return PlayValidator.ValidatePlay(hand, selectedCards, match.CurrentBoard);
        }

        public bool TryGetLocalHand(out IReadOnlyList<Card> hand)
        {
            hand = Array.Empty<Card>();
            var match = CurrentMatch;
            if (match == null || match.LocalSeatIndex < 0) return false;
            
            if (match.LocalSeatIndex >= match.Seats.Length) return false;

            var userId = match.Seats[match.LocalSeatIndex];
            if (string.IsNullOrEmpty(userId)) return false;
            
            if (match.Players.TryGetValue(userId, out var player))
            {
                if (player.Hand != null)
                {
                    hand = player.Hand.Cards;
                    return true;
                }
            }
            return false;
        }
        
        public string ResolveDisplayName(int seat, string userId = null)
        {
            var match = CurrentMatch;
            
            if (match != null && seat >= 0 && seat < match.Seats.Length)
            {
                var nameFromSeat = TryGetSeatDisplayName(match, seat);
                if (!string.IsNullOrWhiteSpace(nameFromSeat) && !string.Equals(nameFromSeat, "Player", StringComparison.OrdinalIgnoreCase))
                {
                    return nameFromSeat;
                }
            }

            if (match != null && !string.IsNullOrWhiteSpace(userId))
            {
                if (match.Players != null && match.Players.TryGetValue(userId, out var player))
                {
                    if (!string.IsNullOrWhiteSpace(player.DisplayName))
                    {
                        return player.DisplayName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var suffix = userId.Length <= 4 ? userId : userId.Substring(0, 4);
                return $"Player {suffix}";
            }

            return seat >= 0 ? $"Player {seat + 1}" : "Player";
        }

        private static string TryGetSeatDisplayName(Match match, int seat)
        {
            if (match == null || match.Seats == null) return "Player";
            if (seat < 0 || seat >= match.Seats.Length) return "Player";

            var userId = match.Seats[seat];
            if (string.IsNullOrWhiteSpace(userId)) return "Player";

            if (match.Players != null && match.Players.TryGetValue(userId, out var player))
            {
                if (!string.IsNullOrWhiteSpace(player.DisplayName))
                {
                    return player.DisplayName;
                }
            }

            var suffix = userId.Length <= 4 ? userId : userId.Substring(0, 4);
            return $"Player {suffix}";
        }

        public int FindSeatByUserId(string userId)
        {
             var match = CurrentMatch;
             if (match == null || match.Seats == null) return -1;
             if (string.IsNullOrWhiteSpace(userId)) return -1;

             for (int i = 0; i < match.Seats.Length; i++)
             {
                 if (match.Seats[i] == userId) return i;
             }
             return -1;
        }
    }
}