using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using TienLen.Presentation.GameRoomScreen.Services;
using TienLen.Presentation.GameRoomScreen.Components;
using VContainer;
using Cysharp.Threading.Tasks;

namespace TienLen.Presentation.GameRoomScreen.Views.Shared
{
    public abstract class BaseGameRoomView : MonoBehaviour
    {
        [Header("Sub-Views")]
        [SerializeField] protected ActionButtonsView _actionButtons;
        [SerializeField] protected PlayerSeatsManagerView _seatsManager;
        [SerializeField] protected GameBoardView _boardView;
        [SerializeField] protected LocalHandView _localHandView;
        [SerializeField] protected GameRoomMessageView _messageView;
        [SerializeField] protected GameRoomLogView _logView;

        protected GameRoomPresenter _presenter;
        protected TienLen.Infrastructure.Config.AvatarRegistry _avatarRegistry;
        protected ILogger<BaseGameRoomView> _logger;
        protected bool _isLeaving;
        protected bool _isDealing;
        protected bool _isAnimationBlocking;
        protected readonly List<Vector3> _pendingLocalPlayOrigins = new List<Vector3>();

        [Inject]
        public virtual void Construct(
            GameRoomPresenter presenter,
            TienLen.Infrastructure.Config.AvatarRegistry avatarRegistry,
            ILogger<BaseGameRoomView> logger)
        {
            _presenter = presenter;
            _avatarRegistry = avatarRegistry;
            _logger = logger;
        }

        protected virtual void Start()
        {
            if (_presenter == null)
            {
                _logger.LogError("{TypeName}: Presenter not injected.", GetType().Name);
                return;
            }

            if (_seatsManager != null && _avatarRegistry != null)
            {
                _seatsManager.Configure(_avatarRegistry);
            }

            _actionButtons.StartGameClicked += () =>
            {
                Debug.Log($"[{GetType().Name}] StartGame requested via ActionButtons.");
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

            _presenter.OnStateUpdated += RefreshAll;
            _presenter.OnGameStarted += HandleGameStarted;
            _presenter.OnCardsPlayed += HandleCardsPlayed;
            _presenter.OnTurnPassed += HandleTurnPassed;
            _presenter.OnBoardUpdated += HandleBoardUpdated;
            _presenter.OnError += HandleError;
            _presenter.OnPresenceChanged += HandlePresenceChanged;
            _presenter.OnGameEnded += HandleGameEnded;

            RefreshAll();
        }

        protected virtual void OnDestroy()
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

        protected virtual void HandleError(string message)
        {
            _messageView?.ShowError(message);
        }

        protected virtual void HandlePlayClicked()
        {
            if (_isAnimationBlocking) return;

            var selected = _localHandView?.SelectedCards;
            var validation = _presenter.ValidatePlay(selected);

            if (!validation.IsValid)
            {
                _messageView?.ShowError("Invalid Play");
                return;
            }

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

        protected virtual void HandleLeaveClicked()
        {
            LeaveAsync().Forget();
        }

        protected virtual async UniTaskVoid LeaveAsync()
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
                _logger.LogWarning(ex, "{TypeName}: Leave failed.", GetType().Name);
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

        protected virtual void OnLocalHandSelectionChanged(IReadOnlyList<Card> selection)
        {
            RefreshActionButtonsState();
        }

        protected virtual void RefreshAll()
        {
            if (_isLeaving || _isAnimationBlocking) return;

            var match = _presenter.CurrentMatch;
            int localSeat = match?.LocalSeatIndex ?? 0;

            _seatsManager.RefreshSeats(match, localSeat);

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

            RefreshActionButtonsState();
        }

        protected virtual void RefreshActionButtonsState()
        {
            if (_isAnimationBlocking || _isDealing)
            {
                _actionButtons.SetActionButtonsVisible(false);
                _actionButtons.SetStartButtonVisible(false);
                return;
            }

            bool isMyTurn = _presenter.IsMyTurn();
            bool isPlaying = _presenter.CurrentMatch?.Phase == "Playing";

            if (!isPlaying)
            {
                bool canStart = _presenter.CanStartGame();
                _actionButtons.SetStartButtonVisible(canStart);
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

        protected virtual void HandleGameStarted()
        {
            if (_isLeaving) return;

            _logView?.AddEntry("Game Started");
            _isDealing = true;
            RefreshActionButtonsState();

            ResetDealingState().Forget();
        }

        protected virtual async UniTaskVoid ResetDealingState()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5f));
            _isDealing = false;
            RefreshAll();
        }

        protected virtual void HandleTurnPassed(int seatIndex)
        {
            string name = _presenter.ResolveDisplayName(seatIndex);
            _logView?.AddEntry($"{name} passed.");
        }

        protected virtual void HandleCardsPlayed(int seatIndex, IReadOnlyList<Card> cards)
        {
            string name = _presenter.ResolveDisplayName(seatIndex);
            _logView?.AddEntry($"{name} played {cards.Count} cards.");

            bool isLocal = seatIndex == (_presenter.CurrentMatch?.LocalSeatIndex ?? -1);

            if (isLocal && _pendingLocalPlayOrigins.Count == cards.Count)
            {
                _boardView.AnimatePlay(cards, _pendingLocalPlayOrigins);
                _pendingLocalPlayOrigins.Clear();
            }
            else
            {
                var seatView = _seatsManager.GetViewBySeatIndex(seatIndex);
                Vector3 spawnPos = seatView != null ? seatView.CardSourceAnchor.position : Vector3.zero;
                _boardView.AnimatePlay(cards, spawnPos);
            }
        }

        protected virtual void HandleBoardUpdated(int seatIndex, bool newRound)
        {
            if (newRound)
            {
                _boardView.SetStatusLabel("New Round");
                _boardView.Clear();
            }
            else
            {
                string name = _presenter.ResolveDisplayName(seatIndex);
                _boardView.SetStatusLabel($"{name}'s Turn");
            }
        }

        protected virtual void HandlePresenceChanged(IReadOnlyList<PresenceChange> changes)
        {
            foreach (var c in changes)
            {
                _logView?.AddEntry($"{c.Username} {(c.Joined ? "joined" : "left")}.");
            }
            RefreshAll();
        }

        protected abstract void HandleGameEnded(GameEndedResultDto result);
    }
}
