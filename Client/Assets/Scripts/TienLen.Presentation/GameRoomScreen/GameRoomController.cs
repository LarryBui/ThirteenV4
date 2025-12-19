using System;
using System.Collections.Generic;
using TienLen.Application.Session;
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

        private TienLenMatchHandler _matchHandler;
        private IGameSessionContext _gameSessionContext;
        private LocalHandView _localHandView;
        private bool _isLeaving;

        [Inject]
        public void Construct(TienLenMatchHandler matchHandler, IGameSessionContext gameSessionContext)
        {
            _matchHandler = matchHandler;
            _gameSessionContext = gameSessionContext;
        }

        private void Start()
        {
            ClearAllPlayerProfiles(); // Clear profiles on start to ensure clean state
            UpdatePlayButtonState(selectedCount: 0);

            if (_matchHandler == null)
            {
                Debug.LogWarning("GameRoomController: TienLenMatchHandler not injected.");
                return;
            }

            _matchHandler.GameRoomStateUpdated += HandleGameRoomStateUpdated;
            _matchHandler.GameStarted += HandleGameStarted;

            // Render current state once in case the initial snapshot arrived before this scene loaded.
            RefreshGameRoomUI();

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor += HandleCardArrivedAtPlayerAnchor;
            }

            BindLocalHandView(GetComponent<LocalHandView>());
        }

        private void OnDestroy()
        {
            if (_matchHandler != null)
            {
                _matchHandler.GameRoomStateUpdated -= HandleGameRoomStateUpdated;
                _matchHandler.GameStarted -= HandleGameStarted;
            }

            if (_cardDealer != null)
            {
                _cardDealer.CardArrivedAtPlayerAnchor -= HandleCardArrivedAtPlayerAnchor;
            }

            BindLocalHandView(null);
        }

        private void HandleGameStarted()
        {
            if (_isLeaving) return;
            PrepareLocalHandReveal();

            // 52 cards, 2.0 seconds duration
            _cardDealer.AnimateDeal(52, 2.0f).Forget();

            UpdatePlayButtonState(selectedCount: _localHandView?.SelectedCards?.Count ?? 0);
        }

        private void HandleGameRoomStateUpdated()
        {
            if (_isLeaving) return;
            RefreshGameRoomUI();
            UpdatePlayButtonState(_localHandView?.SelectedCards?.Count ?? 0);
        }

        private void RefreshGameRoomUI()
        {
            var match = _matchHandler?.CurrentMatch;

            if (match == null || match.Seats == null || match.Seats.Length < SeatCount)
            {
                ClearAllPlayerProfiles();
                return;
            }

            var localSeatIndex = ResolveLocalSeatIndex(match.Seats);
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

        private static void ClearProfileSlot(PlayerProfileUI slot)
        {
            if (slot == null) return;
            slot.ClearProfile();
            slot.SetActive(false);
        }

        private int ResolveLocalSeatIndex(string[] seats)
        {
            var localUserId = _gameSessionContext?.Identity?.UserId;
            if (string.IsNullOrEmpty(localUserId) || seats == null)
            {
                return -1;
            }

            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == localUserId)
                {
                    return i;
                }
            }

            return -1;
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
                slot.SetProfile(player.DisplayName, player.AvatarIndex);
                slot.SetActive(true);
                return;
            }

            var suffix = userId.Length <= 4 ? userId : userId.Substring(0, 4);
            slot.SetProfile($"Player {suffix}", avatarIndex: 0);
            slot.SetActive(true);
        }

        /// <summary>
        /// UI callback for the "Start Game" button.
        /// Sends a start-game request and logs contextual information for debugging.
        /// </summary>
        public void OnStartGameClicked()
        {
            if (_isLeaving) return;
            if (_matchHandler != null && _matchHandler.CurrentMatch != null)
            {
                _matchHandler.StartGameAsync().Forget();
            }
            else
            {
                Debug.LogError("GameRoomController: Cannot start game, Match Handler or Match is null.");
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
                Debug.LogWarning($"GameRoomController: Leave failed: {ex.Message}");
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

        private void HandleLocalHandSelectionChanged(IReadOnlyList<Card> selectedCards)
        {
            UpdatePlayButtonState(selectedCards?.Count ?? 0);
        }

        private void UpdatePlayButtonState(int selectedCount)
        {
            var match = _matchHandler?.CurrentMatch;
            var isPlaying = match != null && string.Equals(match.Phase, "Playing", StringComparison.OrdinalIgnoreCase);
            var isMyTurn = isPlaying && IsLocalPlayersTurn(match);
            var showActions = isPlaying && isMyTurn;

            // Keep action buttons hidden until the game is live and it's the local player's turn.
            if (_playButton != null) _playButton.gameObject.SetActive(showActions);
            if (_passButton != null) _passButton.gameObject.SetActive(showActions);

            if (!showActions) return;
        }

        private bool IsLocalPlayersTurn(Match match)
        {
            if (match == null) return false;

            var localSeatIndex = ResolveLocalSeatIndex(match.Seats);
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
                Debug.LogError("GameRoomController: Local hand card prefab is not assigned. Assign a FrontCardView prefab to render the local hand.");
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
            UpdatePlayButtonState(selectedCount: localHandView.SelectedCards?.Count ?? 0);
        }

        private bool TryGetLocalHand(Match match, out IReadOnlyList<Card> cards)
        {
            cards = Array.Empty<Card>();

            var localUserId = _gameSessionContext?.Identity?.UserId;
            if (string.IsNullOrWhiteSpace(localUserId)) return false;
            
            if (match.Players == null) return false;
            if (!match.Players.TryGetValue(localUserId, out var player)) return false;
            if (player?.Hand == null) return false;

            cards = player.Hand.Cards;
            return cards != null && cards.Count > 0;
        }
    }
}
