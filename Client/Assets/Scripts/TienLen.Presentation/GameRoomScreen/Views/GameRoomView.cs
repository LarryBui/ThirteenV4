using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application; // For MatchPresenceChange
using TienLen.Domain.ValueObjects;
using UnityEngine;
using TienLen.Presentation.GameRoomScreen.Services;
using TienLen.Presentation.GameRoomScreen.Components;
using VContainer;
using Cysharp.Threading.Tasks;


namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// The Root View for the Game Room scene.
    /// Acts as the coordinator, wiring the Presenter's logic to the various sub-views.
    /// Replaces the monolithic GameRoomController.
    /// </summary>
    public sealed class GameRoomView : MonoBehaviour
    {
        [Header("Sub-Views")]
        [SerializeField] private ActionButtonsView _actionButtons;
        [SerializeField] private PlayerSeatsManagerView _seatsManager;
        [SerializeField] private GameBoardView _boardView;
        [SerializeField] private LocalHandView _localHandView;
        [SerializeField] private GameRoomMessageView _messageView;
        [SerializeField] private GameRoomLogView _logView;
        [SerializeField] private OpponentHandRevealer _opponentRevealer;
        [SerializeField] private CardDealer _cardDealer;

        private GameRoomPresenter _presenter;
        private TienLen.Infrastructure.Config.AvatarRegistry _avatarRegistry;
        private ILogger<GameRoomView> _logger;
        private bool _isLeaving;
        private bool _isDealing; // Tracks deal animation state
        private bool _isAnimationBlocking; // Blocks input during critical sequences

        [Inject]
        public void Construct(
            GameRoomPresenter presenter, 
            TienLen.Infrastructure.Config.AvatarRegistry avatarRegistry,
            ILogger<GameRoomView> logger)
        {
            _presenter = presenter;
            _avatarRegistry = avatarRegistry;
            _logger = logger ?? NullLogger<GameRoomView>.Instance;
        }

        private void Start()
        {
            if (_presenter == null)
            {
                _logger.LogError("GameRoomView: Presenter not injected.");
                return;
            }

            // 0. Configure Sub-Views
            if (_seatsManager != null && _avatarRegistry != null)
            {
                _seatsManager.Configure(_avatarRegistry);
            }

            // 1. Wire Up Input (View -> Presenter)
            _actionButtons.StartGameClicked += () => 
            {
                Debug.Log("[GameRoomView] StartGame requested via ActionButtons.");
                _presenter.StartGame();
            };
            _actionButtons.PassClicked += () => 
            {
                _presenter.PassTurn();
                _localHandView?.ClearSelection();
            };
            _actionButtons.LeaveClicked += HandleLeaveClicked;
            _actionButtons.PlayClicked += HandlePlayClicked;

            if (_localHandView != null)
            {
                _localHandView.SelectionChanged += OnLocalHandSelectionChanged;
            }

            // 2. Wire Up Events (Presenter -> View)
            _presenter.OnStateUpdated += RefreshAll;
            _presenter.OnGameStarted += HandleGameStarted;
            _presenter.OnCardsPlayed += HandleCardsPlayed;
            _presenter.OnTurnPassed += HandleTurnPassed;
            _presenter.OnBoardUpdated += HandleBoardUpdated;
            _presenter.OnError += HandleError;
            _presenter.OnTurnCountdownUpdated += HandleTurnCountdown;
            _presenter.OnPresenceChanged += HandlePresenceChanged;
            _presenter.OnGameEnded += HandleGameEnded;

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor += HandleCardArrived;
            }

            // 3. Initial Refresh
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnStateUpdated -= RefreshAll;
                _presenter.OnGameStarted -= HandleGameStarted;
                _presenter.OnCardsPlayed -= HandleCardsPlayed;
                _presenter.OnTurnPassed -= HandleTurnPassed;
                _presenter.OnBoardUpdated -= HandleBoardUpdated;
                _presenter.OnError -= HandleError;
                _presenter.OnTurnCountdownUpdated -= HandleTurnCountdown;
                _presenter.OnPresenceChanged -= HandlePresenceChanged;
                _presenter.OnGameEnded -= HandleGameEnded;
            }

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor -= HandleCardArrived;
            }
        }

        // --- Interaction Handlers ---

        private void HandleCardArrived(int relativeIndex, Vector3 position)
        {
            if (_isLeaving) return;
            // 0=South (local player). Reveal local hand cards when the deal animation reaches South.
            if (relativeIndex == 0)
            {
                _localHandView?.RevealNextCard(position);
            }
        }

        private void HandleError(string message)
        {
            _messageView?.ShowError(message);
        }

        private void HandlePlayClicked()
        {
            if (_isAnimationBlocking) return;

            var selected = _localHandView?.SelectedCards;
            var validation = _presenter.ValidatePlay(selected);

            if (!validation.IsValid)
            {
                // TODO: Map validation reason to user friendly string
                _messageView?.ShowError("Invalid Play");
                return;
            }

            // Optimistic Update: Hide cards immediately or wait?
            // Let's animate them flying to board?
            // The BoardView.AnimatePlay handles the "Flying" part, but it expects cards to fly from Source -> Center.
            // If Local Player plays, source is Hand.
            // For now, standard flow: Send Request -> Server OK -> Event CardPlayed -> Animation.
            
            _presenter.PlayCards(new List<Card>(selected));
            _localHandView?.ClearSelection();
        }

        private void HandleLeaveClicked()
        {
            LeaveAsync().Forget();
        }

        private async UniTaskVoid LeaveAsync()
        {
            if (_isLeaving) return;
            _isLeaving = true;

            _actionButtons.SetLeaveButtonInteractable(false);

            try
            {
                await _presenter.LeaveMatchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GameRoomView: Leave failed.");
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                var scene = gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                {
                    await UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        private void OnLocalHandSelectionChanged(IReadOnlyList<Card> selection)
        {
            RefreshActionButtonsState();
        }

        // --- State Refresh ---

        private void RefreshAll()
        {
            if (_isLeaving || _isAnimationBlocking) return;

            var match = _presenter.CurrentMatch;
            int localSeat = match?.LocalSeatIndex ?? 0;

            // Seats
            _seatsManager.RefreshSeats(match, localSeat);

            // Board
            if (match != null) _boardView.SetBoard(match.CurrentBoard);

            // Local Hand
            // Only update if NOT currently animating the deal/sort sequence
            if (!_isDealing && _presenter.TryGetLocalHand(out var hand))
            {
                _localHandView?.SetHand(hand);
            }

            // Buttons
            RefreshActionButtonsState();
        }

        private void RefreshActionButtonsState()
        {
            if (_isAnimationBlocking)
            {
                _actionButtons.SetActionButtonsVisible(false);
                return;
            }

            bool isMyTurn = _presenter.IsMyTurn();
            bool isPlaying = _presenter.CurrentMatch?.Phase == "Playing"; // Magic string, should be Enum

            if (!isPlaying)
            {
                _actionButtons.SetStartButtonVisible(_presenter.CanStartGame());
                _actionButtons.SetActionButtonsVisible(false);
                return;
            }

            _actionButtons.SetStartButtonVisible(false);
            _actionButtons.SetActionButtonsVisible(isMyTurn);

            if (isMyTurn)
            {
                bool canPass = _presenter.CanPass();
                _actionButtons.SetPassButtonInteractable(canPass);
                
                var selection = _localHandView?.SelectedCards;
                bool hasSelection = selection != null && selection.Count > 0;
                var validation = _presenter.ValidatePlay(selection);
                _actionButtons.SetPlayButtonInteractable(validation.IsValid);
                _actionButtons.SetPlayButtonValidationVisual(validation.IsValid || !hasSelection);

                bool hasMove = false;
                if (_presenter.TryGetLocalHand(out var hand))
                {
                    hasMove = _presenter.HasPlayableMove(hand);
                }
                _actionButtons.SetPassButtonHighlight(!hasMove && canPass);
            }
            else
            {
                 _actionButtons.SetPassButtonHighlight(false);
            }
        }

        // --- Event Handlers ---

        private async void HandleGameStarted()
        {
            if (_isLeaving) return;
            
            _logView?.AddEntry("Game Started");
            _isDealing = true;

            IReadOnlyList<Card> sortedHand = null;

            // 1. Get the sorted hand (which we will shuffle locally for visual effect)
            if (_presenter.TryGetLocalHand(out var hand))
            {
                sortedHand = hand;
                var shuffledHand = new List<Card>(hand);
                ShuffleList(shuffledHand);
                _localHandView?.BeginReveal(shuffledHand);
            }

            // 2. Animate Dealing
            if (_cardDealer != null)
            {
                await _cardDealer.AnimateDeal(52);
            }

            // 3. Animate Sort
            if (_localHandView != null && sortedHand != null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                await _localHandView.SortHandAnimation(sortedHand);
            }

            _isDealing = false;
            RefreshAll();
        }

        private void ShuffleList<T>(IList<T> list)
        {
            var rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private void HandleTurnPassed(int seatIndex)
        {
            string name = _presenter.ResolveDisplayName(seatIndex);
            _logView?.AddEntry($"{name} passed.");
            
            // Visual feedback?
            // _boardView.ShowFloatingText(seatIndex, "Pass");
        }

        private void HandleCardsPlayed(int seatIndex, IReadOnlyList<Card> cards)
        {
            // 1. Log
            string name = _presenter.ResolveDisplayName(seatIndex);
            _logView?.AddEntry($"{name} played {cards.Count} cards.");

            // 2. Animate
            // We need the world position of the player's avatar to spawn the flying cards
            var seatView = _seatsManager.GetViewBySeatIndex(seatIndex);
            Vector3 spawnPos = seatView != null ? seatView.CardSourceAnchor.position : Vector3.zero;

            // Decrement counter for non-local players
            if (seatIndex != (_presenter.CurrentMatch?.LocalSeatIndex ?? -1))
            {
                _seatsManager.DecrementSeatCardCount(seatIndex, cards.Count);
            }

            // If local player, maybe we spawn from hand? 
            // _localHandView has the specific card positions.
            // For simplicity, let's use the Seat Anchor for everyone for now.
            _boardView.AnimatePlay(cards, spawnPos);
        }

        private void HandleBoardUpdated(int seatIndex, bool newRound)
        {
            if (newRound)
            {
                _boardView.SetStatusLabel("New Round");
                _boardView.Clear(); // Or fade out old cards
            }
            else
            {
                string name = _presenter.ResolveDisplayName(seatIndex);
                _boardView.SetStatusLabel($"{name}'s Turn"); // Or "Last played by X"
            }
        }

        private void HandleTurnCountdown(int seatIndex, long seconds)
        {
            _seatsManager.StartCountdown(seatIndex, (int)seconds);
        }

        private void HandlePresenceChanged(IReadOnlyList<PresenceChange> changes)
        {
            foreach (var c in changes)
            {
                _logView?.AddEntry($"{c.Username} {(c.Joined ? "joined" : "left")}.");
            }
            RefreshAll();
        }

        private void HandleGameEnded(List<int> finishOrder, Dictionary<int, List<Card>> remainingHands)
        {
            RunGameEndSequence(remainingHands).Forget();
        }

        private async UniTaskVoid RunGameEndSequence(Dictionary<int, List<Card>> remainingHands)
        {
            _isAnimationBlocking = true;
            _actionButtons.SetActionButtonsVisible(false); // Hide inputs
            _messageView.ShowInfo("Game Ended");

            // 1. Reveal Local
            _localHandView?.ShowHiddenSelectedCards();

            // 2. Reveal Opponents
            if (_opponentRevealer != null && remainingHands != null)
            {
                int localSeat = _presenter.CurrentMatch?.LocalSeatIndex ?? 0;
                foreach (var kvp in remainingHands)
                {
                    if (kvp.Key != localSeat)
                        _opponentRevealer.RevealHand(kvp.Key, kvp.Value);
                }
            }

            // 3. Wait
            await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy());

            // 4. Cleanup
            _boardView.Clear();
            _localHandView?.Clear(); // Or ClearHand() if method name matches
            _opponentRevealer?.Clear();
            _seatsManager.StopAllCountdowns();

            _isAnimationBlocking = false;
            RefreshAll();
        }
    }
}
