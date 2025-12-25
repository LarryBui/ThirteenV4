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

        private GameRoomPresenter _presenter;
        private TienLen.Infrastructure.Config.AvatarRegistry _avatarRegistry;
        private ILogger<GameRoomView> _logger;
        private bool _isLeaving;
        private bool _isDealing; // Tracks deal animation state
        private bool _isAnimationBlocking; // Blocks input during critical sequences
        private readonly List<Vector3> _pendingLocalPlayOrigins = new List<Vector3>();

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
            _presenter.OnPresenceChanged += HandlePresenceChanged;
            _presenter.OnGameEnded += HandleGameEnded;

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
                _presenter.OnPresenceChanged -= HandlePresenceChanged;
                _presenter.OnGameEnded -= HandleGameEnded;
            }
        }

        // --- Interaction Handlers ---

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
            
            // Capture positions for animation
            _pendingLocalPlayOrigins.Clear();
            if (_localHandView != null && _localHandView.TryGetSelectedCardSnapshots(out var snapshots))
            {
                foreach (var snap in snapshots)
                {
                    _pendingLocalPlayOrigins.Add(snap.WorldPosition);
                }
            }

            _presenter.PlayCards(new List<Card>(selected));
            _localHandView?.HideSelectedCards();
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
            if (match != null)
            {
                 if (match.Phase == "Playing")
                 {
                     _boardView.SetBoard(match.CurrentBoard);
                 }
                 else
                 {
                     _boardView.Clear();
                 }
            }

            // Local Hand
            // Only update if NOT currently animating the deal/sort sequence
            if (!_isDealing)
            {
                if (match != null && match.Phase == "Playing" && _presenter.TryGetLocalHand(out var hand))
                {
                    _localHandView?.SetHand(hand);
                }
                else
                {
                    _localHandView?.Clear();
                }
            }

            // Buttons
            RefreshActionButtonsState();
        }

        private void RefreshActionButtonsState()
        {
            if (_isAnimationBlocking || _isDealing)
            {
                _actionButtons.SetActionButtonsVisible(false);
                _actionButtons.SetStartButtonVisible(false);
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

                // Play button is interactable if selection is valid OR if nothing is selected yet.
                // It only disables if a selection is made that is actually invalid.
                bool isPlayInteractable = validation.IsValid || validation.Reason == TienLen.Domain.Services.PlayValidationReason.NoSelection;
                _actionButtons.SetPlayButtonInteractable(isPlayInteractable);
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

        private void HandleGameStarted()
        {
            if (_isLeaving) return;
            
            _logView?.AddEntry("Game Started");
            _isDealing = true;
            RefreshActionButtonsState();

            // Note: CardDealer and LocalHandView handle their own animations 
            // by listening to OnGameStarted and OnCardArrived.
            
            // We'll reset _isDealing via a delay or wait for state refresh
            ResetDealingState().Forget();
        }

        private async UniTaskVoid ResetDealingState()
        {
            // Approximate duration of deal + sort
            await UniTask.Delay(TimeSpan.FromSeconds(5f));
            _isDealing = false;
            RefreshAll();
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
            bool isLocal = seatIndex == (_presenter.CurrentMatch?.LocalSeatIndex ?? -1);
            
            if (isLocal && _pendingLocalPlayOrigins.Count == cards.Count)
            {
                 // Use buffered positions from the hand
                 _boardView.AnimatePlay(cards, _pendingLocalPlayOrigins);
                 _pendingLocalPlayOrigins.Clear();
            }
            else
            {
                // Fallback / Opponent: Use Seat Anchor
                var seatView = _seatsManager.GetViewBySeatIndex(seatIndex);
                Vector3 spawnPos = seatView != null ? seatView.CardSourceAnchor.position : Vector3.zero;
                _boardView.AnimatePlay(cards, spawnPos);
            }
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
            _actionButtons.SetStartButtonVisible(false); // Ensure Start is hidden
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
            _localHandView?.Clear(); 
            _opponentRevealer?.Clear();

            _isAnimationBlocking = false;
            RefreshAll();
        }
    }
}
