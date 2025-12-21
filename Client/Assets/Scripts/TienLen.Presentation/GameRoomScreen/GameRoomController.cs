using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Domain.Aggregates;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TienLen.Application;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace TienLen.Presentation.GameRoomScreen
{
    public class GameRoomController : MonoBehaviour
    {
        private const int SeatCount = 4;

        [Header("Scene References")]
        [SerializeField] private CardDealer _cardDealer;

        [Header("Hand View")]
        [Tooltip("Prefab used to render the local player's hand (front face). Should be a UI prefab with a RectTransform.")]
        [SerializeField] private GameObject _localHandCardPrefab;

        [Header("Actions")]
        [Tooltip("Start Game button visible only to the match owner.")]
        [SerializeField] private Button _startGameButton;
        [Tooltip("Play button that submits the currently selected cards.")]
        [SerializeField] private Button _playButton;
        [Tooltip("Pass button that skips the turn.")]
        [SerializeField] private Button _passButton;
        [Tooltip("Leave button that exits the match and returns to the Home screen.")]
        [SerializeField] private Button _leaveButton;

        [Header("Player Profiles")]
        [SerializeField] private PlayerProfileUI localPlayerProfile;
        [SerializeField] private PlayerProfileUI opponentProfile_1;
        [SerializeField] private PlayerProfileUI opponentProfile_2;
        [SerializeField] private PlayerProfileUI opponentProfile_3;

        [Header("Board")]
        [SerializeField] private BoardCardsView _boardCardsView;
        [Tooltip("Animates local played cards flying to the board.")]
        [SerializeField] private PlayedCardsAnimator _playedCardsAnimator;
        [Tooltip("Displays transient game messages such as errors.")]
        [SerializeField] private GameMessagePresenter _gameMessagePresenter;
        [Tooltip("Displays recent room actions in the top-left corner.")]
        [SerializeField] private GameRoomLogView _gameRoomLogView;

        private TienLenMatchHandler _matchHandler;
        private ILogger<GameRoomController> _logger;
        private ILoggerFactory _loggerFactory;
        private LocalHandView _localHandView;
        private bool _isLeaving;
        // Monotonic counter for tracking optimistic play animations.
        private int _pendingPlayToken;

        /// <summary>
        /// Injects required services for the GameRoom.
        /// </summary>
        /// <param name="matchHandler">Match handler for game state coordination.</param>
        /// <param name="logger">Logger for GameRoom diagnostics.</param>
        /// <param name="loggerFactory">Factory used to create child component loggers.</param>
        [Inject]
        public void Construct(
            TienLenMatchHandler matchHandler,
            ILogger<GameRoomController> logger,
            ILoggerFactory loggerFactory)
        {
            _matchHandler = matchHandler;
            _logger = logger ?? NullLogger<GameRoomController>.Instance;
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        private void Start()
        {
            ConfigureChildLoggers();
            ClearAllPlayerProfiles(); // Clear profiles on start to ensure clean state
            InitializeStartGameButton();
            UpdateStartGameButtonState();
            UpdatePlayButtonState();

            if (_matchHandler == null)
            {
                _logger.LogWarning("GameRoomController: TienLenMatchHandler not injected.");
                return;
            }

            _matchHandler.GameRoomStateUpdated += HandleGameRoomStateUpdated;
            _matchHandler.GameStarted += HandleGameStarted;
            _matchHandler.GameBoardUpdated += HandleGameBoardUpdated;
            _matchHandler.GameErrorReceived += HandleGameError;
            _matchHandler.CardsPlayed += HandleCardsPlayed;
            _matchHandler.TurnPassed += HandleTurnPassed;
            _matchHandler.MatchPresenceChanged += HandleMatchPresenceChanged;

            // Render current state once in case the initial snapshot arrived before this scene loaded.
            RefreshGameRoomUI();
            UpdateBoardView(_matchHandler?.CurrentMatch);

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor += HandleCardArrivedAtPlayerAnchor;
            }

            BindLocalHandView(GetComponent<LocalHandView>());
            BindRoomLogView(_gameRoomLogView ?? GetComponentInChildren<GameRoomLogView>(includeInactive: true));
        }

        private void OnDestroy()
        {
            if (_matchHandler != null)
            {
                _matchHandler.GameRoomStateUpdated -= HandleGameRoomStateUpdated;
                _matchHandler.GameStarted -= HandleGameStarted;
                _matchHandler.GameBoardUpdated -= HandleGameBoardUpdated;
                _matchHandler.GameErrorReceived -= HandleGameError;
                _matchHandler.CardsPlayed -= HandleCardsPlayed;
                _matchHandler.TurnPassed -= HandleTurnPassed;
                _matchHandler.MatchPresenceChanged -= HandleMatchPresenceChanged;
            }

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor -= HandleCardArrivedAtPlayerAnchor;
            }

            BindLocalHandView(null);
            BindRoomLogView(null);
        }

        private void HandleGameStarted()
        {
            if (_isLeaving) return;
            var match = _matchHandler?.CurrentMatch;
            if (match != null)
            {
                _logger.LogInformation(
                    "GameRoomController: Game started. matchId={matchId}, localSeat={localSeat}, firstTurnSeat={firstTurnSeat}",
                    match.Id,
                    match.LocalSeatIndex,
                    match.CurrentTurnSeat);
            }
            PrepareLocalHandReveal();

            // 52 cards, per-card delay configured on the CardDealer.
            _cardDealer.AnimateDeal(52).Forget();

            UpdateBoardView(match);
            UpdateStartGameButtonState();
            UpdatePlayButtonState();
        }

        private void HandleGameBoardUpdated(int seat, bool newRound)
        {
            if (_isLeaving) return;
            var match = _matchHandler?.CurrentMatch;
            if (match == null) return;

            UpdateBoardView(match);
            UpdateBoardLabel(match, seat, newRound);
            if (newRound)
            {
                _boardCardsView?.PlayNewRoundFade();
            }

            if (match.LocalSeatIndex >= 0 && match.LastPlaySeat == match.LocalSeatIndex)
            {
                _gameMessagePresenter?.RequestClear();
            }
        }

        private void HandleGameError(int code, string message)
        {
            if (_isLeaving) return;
            _playedCardsAnimator?.CancelActiveAnimations();
            _localHandView?.ShowHiddenSelectedCards();
            _gameMessagePresenter?.ShowError(message);
            _logger.LogWarning("GameRoomController: Game error received. code={code}, message={message}", code, message);
        }

        private void HandleCardsPlayed(int seat, IReadOnlyList<Card> cards)
        {
            TryAnimateOpponentPlay(seat, cards);
            var match = _matchHandler?.CurrentMatch;
            var displayName = ResolveDisplayName(match, seat, userId: null);
            var cardText = FormatCards(cards);
            _gameRoomLogView?.AddEntry($"{displayName} played {cardText}.");
        }

        private void HandleTurnPassed(int seat)
        {
            var match = _matchHandler?.CurrentMatch;
            var displayName = ResolveDisplayName(match, seat, userId: null);
            _gameRoomLogView?.AddEntry($"{displayName} passed.");
        }

        private void HandleMatchPresenceChanged(IReadOnlyList<PresenceChange> changes)
        {
            var match = _matchHandler?.CurrentMatch;
            if (match == null || changes == null) return;

            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.UserId)) continue;

                var seat = FindSeatByUserId(match, change.UserId);
                var displayName = ResolveDisplayName(match, seat, change.UserId);
                var suffix = change.Joined ? "joined" : "left";
                _gameRoomLogView?.AddEntry($"{displayName} {suffix}.");
            }
        }

        private void HandleGameRoomStateUpdated()
        {
            _logger.LogInformation(
                "GameRoomController: Game room state updated. seatId={seatId}",
                _matchHandler?.CurrentMatch?.LocalSeatIndex);
            if (_isLeaving) return;
            RefreshGameRoomUI();

            // Sync the local hand view with the domain state
            if (_localHandView != null && TryGetLocalHand(_matchHandler?.CurrentMatch, out var localHandCards))
            {
                // Note: If an animation is playing (e.g. Deal), this might conflict. 
                // However, PrepareLocalHandReveal calls BeginReveal which clears the hand anyway,
                // so the conflict is minimal (flash). 
                // Ideally, we check game phase or animation state.
                _localHandView.SetHand(localHandCards);
            }

            UpdateBoardView(_matchHandler?.CurrentMatch);
            UpdateStartGameButtonState();
            UpdatePlayButtonState();
        }

        private void RefreshGameRoomUI()
        {
            var match = _matchHandler?.CurrentMatch;

            if (match == null || match.Seats == null || match.Seats.Length < SeatCount)
            {
                ClearAllPlayerProfiles();
                return;
            }

            var localSeatIndex = match.LocalSeatIndex;
            if (localSeatIndex < 0 || localSeatIndex >= SeatCount)
            {
                // Fallback to "seat 0 is local" until we can resolve the actual local seat index.
                localSeatIndex = 0;
            }

            RenderSeat(localPlayerProfile, match, localSeatIndex);
            RenderSeat(opponentProfile_1, match, (localSeatIndex + 1) % SeatCount);
            RenderSeat(opponentProfile_2, match, (localSeatIndex + 2) % SeatCount);
            RenderSeat(opponentProfile_3, match, (localSeatIndex + 3) % SeatCount);
        }

        private void ClearAllPlayerProfiles()
        {
            ClearProfileSlot(localPlayerProfile);
            ClearProfileSlot(opponentProfile_1);
            ClearProfileSlot(opponentProfile_2);
            ClearProfileSlot(opponentProfile_3);
        }

        private void UpdateBoardView(Match match)
        {
            if (_boardCardsView == null) return;
            if (match == null)
            {
                _boardCardsView.Clear();
                return;
            }

            _boardCardsView.SetBoard(match.CurrentBoard);
        }

        private void UpdateBoardLabel(Match match, int seat, bool newRound)
        {
            if (_boardCardsView == null) return;
            if (newRound)
            {
                _boardCardsView.SetLabel("New round");
                return;
            }

            var displayName = TryGetSeatDisplayName(match, seat);
            _boardCardsView.SetLabel(displayName);
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

        private static string ResolveDisplayName(Match match, int seat, string userId)
        {
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

        private PlayerProfileUI FindProfileBySeat(int seat)
        {
            if (seat < 0) return null;

            if (localPlayerProfile != null && localPlayerProfile.SeatIndex == seat) return localPlayerProfile;
            if (opponentProfile_1 != null && opponentProfile_1.SeatIndex == seat) return opponentProfile_1;
            if (opponentProfile_2 != null && opponentProfile_2.SeatIndex == seat) return opponentProfile_2;
            if (opponentProfile_3 != null && opponentProfile_3.SeatIndex == seat) return opponentProfile_3;

            return null;
        }

        private static string FormatCards(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return "cards";

            var parts = new string[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                parts[i] = CardTextFormatter.FormatShort(cards[i]);
            }

            return string.Join(" ", parts);
        }

        private static int FindSeatByUserId(Match match, string userId)
        {
            if (match == null || match.Seats == null) return -1;
            if (string.IsNullOrWhiteSpace(userId)) return -1;

            for (int i = 0; i < match.Seats.Length; i++)
            {
                if (match.Seats[i] == userId) return i;
            }

            return -1;
        }

        private static void ClearProfileSlot(PlayerProfileUI slot)
        {
            if (slot == null) return;
            slot.ClearProfile();
            slot.SetActive(false);
        }

        private static void RenderSeat(PlayerProfileUI slot, Match match, int seatIndex)
        {
            if (slot == null) return;

            if (match == null || match.Seats == null || seatIndex < 0 || seatIndex >= match.Seats.Length)
            {
                slot.ClearProfile();
                slot.SetActive(false);
                return;
            }

            var userId = match.Seats[seatIndex];
            if (string.IsNullOrEmpty(userId))
            {
                slot.ClearProfile();
                slot.SetActive(false);
                return;
            }

            if (match.Players != null && match.Players.TryGetValue(userId, out var player))
            {
                slot.SetProfile(player.DisplayName, player.AvatarIndex, seatIndex);
                slot.SetActive(true);
                return;
            }

            var suffix = userId.Length <= 4 ? userId : userId.Substring(0, 4);
            slot.SetProfile($"Player {suffix}", avatarIndex: 0, seatIndex: seatIndex);
            slot.SetActive(true);
        }

        /// <summary>
        /// UI callback for the "Start Game" button.
        /// Sends a start-game request and hides the button immediately to prevent duplicate clicks.
        /// </summary>
        public void OnStartGameClicked()
        {
            if (_isLeaving) return;
            if (_startGameButton != null)
            {
                _startGameButton.interactable = false;
                _startGameButton.gameObject.SetActive(false);
            }
            if (_matchHandler != null && _matchHandler.CurrentMatch != null)
            {

                _matchHandler.StartGameAsync().Forget();
            }
            else
            {
                _logger.LogError("GameRoomController: Cannot start game, Match Handler or Match is null.");
                UpdateStartGameButtonState();
            }
        }

        /// <summary>
        /// UI callback for the "Play" button.
        /// Sends selected cards to the server.
        /// </summary>
        public void OnPlayClicked()
        {
            if (_isLeaving) return;
            var selectedCards = _localHandView?.SelectedCards;
            var selectedCount = selectedCards?.Count ?? 0;

            if (selectedCount > 0 && _matchHandler != null)
            {
                TryAnimateSelectedCards();

                // Create a copy list for the async call
                var cardsToSend = new List<Card>(selectedCards);
                _matchHandler.PlayCardsAsync(cardsToSend).Forget();
                
                // Optimistically clear selection or wait for server event?
                // Server event will update the hand, which should trigger view refresh.
                // But clearing selection immediately feels responsive.
                _localHandView.ClearSelection();
            }
        }

        /// <summary>
        /// UI callback for the "Pass" button.
        /// Sends a pass turn request to the server.
        /// </summary>
        public void OnPassClicked()
        {
            if (_isLeaving) return;
            if (_matchHandler != null)
            {
                _matchHandler.PassTurnAsync().Forget();
                _localHandView?.ClearSelection();
            }
        }

        /// <summary>
        /// UI callback for the "Leave" button.
        /// Leaves the current match (best-effort) and unloads the GameRoom scene to return to Home.
        /// </summary>
        public void OnLeaveClicked()
        {
            LeaveToHomeAsync().Forget();
        }

        private async UniTaskVoid LeaveToHomeAsync()
        {
            if (_isLeaving) return;
            _isLeaving = true;

            if (_leaveButton != null) _leaveButton.interactable = false;

            try
            {
                if (_matchHandler != null)
                {
                    await _matchHandler.LeaveMatchAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GameRoomController: Leave failed.");
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                var scene = gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                {
                    await SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        private void BindLocalHandView(LocalHandView view)
        {
            if (_localHandView == view) return;

            if (_localHandView != null)
            {
                _localHandView.SelectionChanged -= HandleLocalHandSelectionChanged;
            }

            _localHandView = view;

            if (_localHandView != null)
            {
                _localHandView.SelectionChanged += HandleLocalHandSelectionChanged;
            }
        }

        private void BindRoomLogView(GameRoomLogView view)
        {
            _gameRoomLogView = view;
        }

        private void HandleLocalHandSelectionChanged(IReadOnlyList<Card> selectedCards)
        {
            UpdatePlayButtonState();
        }

        private void UpdatePlayButtonState()
        {
            var match = _matchHandler?.CurrentMatch;
            var isPlaying = match != null && string.Equals(match.Phase, "Playing", StringComparison.OrdinalIgnoreCase);
            var isMyTurn = isPlaying && IsLocalPlayersTurn(match);
            var showActions = isPlaying && isMyTurn;

            // Keep action buttons hidden until the game is live and it's the local player's turn.
            if (_playButton != null) _playButton.gameObject.SetActive(showActions);
            if (_passButton != null) _passButton.gameObject.SetActive(showActions);
        }

        private void InitializeStartGameButton()
        {
            if (_startGameButton == null) return;
            _startGameButton.interactable = false;
            _startGameButton.gameObject.SetActive(false);
        }

        private void UpdateStartGameButtonState()
        {
            if (_startGameButton == null) return;

            var match = _matchHandler?.CurrentMatch;
            var localSeatIndex = match?.LocalSeatIndex ?? -1;
            var ownerSeat = match?.OwnerSeat ?? -1;
            var isOwner = localSeatIndex >= 0 && ownerSeat == localSeatIndex;
            var isLobby = match != null && string.Equals(match.Phase, "Lobby", StringComparison.OrdinalIgnoreCase);
            var canStart = isOwner && isLobby;

            _startGameButton.gameObject.SetActive(canStart);
            _startGameButton.interactable = canStart;
        }

        private bool IsLocalPlayersTurn(Match match)
        {
            if (match == null) return false;

            var localSeatIndex = match.LocalSeatIndex;
            if (localSeatIndex < 0) return false;

            // CurrentTurnSeat is 0-based from server.
            return match.CurrentTurnSeat == localSeatIndex;
        }

        private void HandleCardArrivedAtPlayerAnchor(int playerIndex, Vector3 anchorWorldPosition)
        {
            if (_isLeaving) return;
            // 0=South (local player). Reveal local hand cards when the deal animation reaches South.
            if (playerIndex != 0) return;
            _localHandView?.RevealNextCard(anchorWorldPosition);
        }

        private void PrepareLocalHandReveal()
        {
            var match = _matchHandler?.CurrentMatch;
            if (match == null) return;

            if (_cardDealer == null) return;
            if (_localHandCardPrefab == null)
            {
                _logger.LogError(
                    "GameRoomController: Local hand card prefab is not assigned. Assign a FrontCardView prefab to render the local hand.");
                return;
            }

            if (!TryGetLocalHand(match, out var localHandCards))
            {
                return;
            }

            var southAnchor = _cardDealer.GetPlayerAnchor(0);
            if (southAnchor == null) return;

            var localHandView = GetComponent<LocalHandView>() ?? gameObject.AddComponent<LocalHandView>();
            BindLocalHandView(localHandView);

            localHandView.Configure(
                cardPrefab: _localHandCardPrefab,
                handAnchor: southAnchor,
                uiParent: southAnchor.transform.parent != null ? southAnchor.transform.parent : southAnchor.transform);

            localHandView.BeginReveal(localHandCards);
            UpdateStartGameButtonState();
            UpdatePlayButtonState();
        }

        private void TryAnimateSelectedCards()
        {
            var animator = _playedCardsAnimator ?? GetComponentInChildren<PlayedCardsAnimator>(includeInactive: true);
            if (animator == null) return;
            if (_loggerFactory != null)
            {
                animator.SetLogger(_loggerFactory.CreateLogger<PlayedCardsAnimator>());
            }
            if (_localHandView == null) return;

            if (!_localHandView.TryGetSelectedCardSnapshots(out var snapshots)) return;

            var cards = new List<Card>(snapshots.Count);
            var rects = new List<RectTransform>(snapshots.Count);
            foreach (var snapshot in snapshots)
            {
                if (snapshot == null || snapshot.Rect == null) continue;
                cards.Add(snapshot.Card);
                rects.Add(snapshot.Rect);
            }

            if (cards.Count == 0 || rects.Count == 0) return;
            if (cards.Count != rects.Count) return;

            _pendingPlayToken++;
            _localHandView.HideSelectedCards();
            animator.AnimatePlay(cards, rects).Forget();
        }

        private void TryAnimateOpponentPlay(int seat, IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return;

            var match = _matchHandler?.CurrentMatch;
            if (match == null) return;

            var localSeatIndex = match.LocalSeatIndex;
            if (localSeatIndex >= 0 && seat == localSeatIndex) return;

            var profile = FindProfileBySeat(seat);
            if (profile == null) return;

            var sourceAnchor = profile.GetAvatarAnchor();
            if (sourceAnchor == null) return;

            var animator = _playedCardsAnimator ?? GetComponentInChildren<PlayedCardsAnimator>(includeInactive: true);
            if (animator == null) return;

            if (_loggerFactory != null)
            {
                animator.SetLogger(_loggerFactory.CreateLogger<PlayedCardsAnimator>());
            }

            var sourceRects = new List<RectTransform>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                sourceRects.Add(sourceAnchor);
            }

            animator.AnimatePlay(cards, sourceRects).Forget();
        }

        private void ConfigureChildLoggers()
        {
            if (_loggerFactory == null) return;

            var profileLogger = _loggerFactory.CreateLogger<PlayerProfileUI>();
            localPlayerProfile?.SetLogger(profileLogger);
            opponentProfile_1?.SetLogger(profileLogger);
            opponentProfile_2?.SetLogger(profileLogger);
            opponentProfile_3?.SetLogger(profileLogger);

            var animatorLogger = _loggerFactory.CreateLogger<PlayedCardsAnimator>();
            _playedCardsAnimator?.SetLogger(animatorLogger);
        }

        private bool TryGetLocalHand(Match match, out IReadOnlyList<Card> cards)
        {
            cards = Array.Empty<Card>();

            if (match == null || match.Seats == null) return false;
            var localSeatIndex = match.LocalSeatIndex;
            if (localSeatIndex < 0 || localSeatIndex >= match.Seats.Length) return false;

            var localUserId = match.Seats[localSeatIndex];
            if (string.IsNullOrWhiteSpace(localUserId)) return false;

            if (match.Players == null) return false;
            if (!match.Players.TryGetValue(localUserId, out var player)) return false;
            if (player?.Hand == null) return false;

            cards = player.Hand.Cards;
            return cards != null && cards.Count > 0;
        }
    }
}
