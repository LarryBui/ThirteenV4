using System;
using System.Collections.Generic;
using TienLen.Application.Session;
using TienLen.Domain.Aggregates;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using VContainer;
using TienLen.Application;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

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

        [Header("Player Profiles")]
        [SerializeField] private PlayerProfileUI localPlayerProfile;
        [SerializeField] private PlayerProfileUI opponentProfile_1;
        [SerializeField] private PlayerProfileUI opponentProfile_2;
        [SerializeField] private PlayerProfileUI opponentProfile_3;

        private TienLenMatchHandler _matchHandler;
        private IGameSessionContext _gameSessionContext;
        private LocalHandView _localHandView;

        [Inject]
        public void Construct(TienLenMatchHandler matchHandler, IGameSessionContext gameSessionContext)
        {
            _matchHandler = matchHandler;
            _gameSessionContext = gameSessionContext;
        }

        private void Start()
        {
            ClearAllPlayerProfiles(); // Clear profiles on start to ensure clean state

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
        }

        private void HandleGameStarted()
        {
            PrepareLocalHandReveal();

            Debug.Log("GameRoomController: AnimateDeal 52 cards over 2.0 seconds.");
            // 52 cards, 2.0 seconds duration
            _cardDealer.AnimateDeal(52, 2.0f).Forget();
        }

        private void HandleGameRoomStateUpdated()
        {
            RefreshGameRoomUI();
        }

        private void RefreshGameRoomUI()
        {
            var match = _matchHandler?.CurrentMatch;
            Debug.Log($"RefreshGameRoomUI: {JsonConvert.SerializeObject(match)}");

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
            var seatIndex = _gameSessionContext?.CurrentMatch?.SeatIndex ?? -1;
            if (seatIndex >= 0)
            {
                return seatIndex;
            }

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
            if (_matchHandler != null)
            {
                var matchId = _gameSessionContext?.CurrentMatch?.MatchId ?? _matchHandler.CurrentMatch?.Id ?? "<unknown>";
                var seatIndex = _gameSessionContext?.CurrentMatch?.SeatIndex ?? -1;
                var localUserId = _gameSessionContext?.Identity?.UserId;
                var localUserIdShort = string.IsNullOrEmpty(localUserId)
                    ? "<unknown>"
                    : (localUserId.Length <= 8 ? localUserId : localUserId.Substring(0, 8));

                Debug.Log($"GameRoomController: StartGame clicked (matchId={matchId}, seatIndex={seatIndex}, userId={localUserIdShort})");
                _matchHandler.StartGameAsync().Forget();
            }
            else
            {
                Debug.LogError("GameRoomController: Cannot start game, Match Handler is null.");
            }
        }

        /// <summary>
        /// UI callback for the "Play" button.
        /// For now this logs the current selection; later steps will send selected cards to the server.
        /// </summary>
        public void OnPlayClicked()
        {
            var selectedCount = _localHandView?.SelectedCards?.Count ?? 0;
            Debug.Log($"GameRoomController: Play clicked (selectedCount={selectedCount})");
        }

        private void HandleCardArrivedAtPlayerAnchor(int playerIndex, Vector3 anchorWorldPosition)
        {
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

            _localHandView ??= GetComponent<LocalHandView>() ?? gameObject.AddComponent<LocalHandView>();

            _localHandView.Configure(
                cardPrefab: _localHandCardPrefab,
                handAnchor: southAnchor,
                uiParent: southAnchor.transform.parent != null ? southAnchor.transform.parent : southAnchor.transform);

            _localHandView.BeginReveal(localHandCards);
        }

        private bool TryGetLocalHand(Match match, out IReadOnlyList<Card> cards)
        {
            cards = Array.Empty<Card>();

            var localUserId = _gameSessionContext?.Identity?.UserId;
            if (string.IsNullOrWhiteSpace(localUserId))
            {
                var seatIndex = _gameSessionContext?.CurrentMatch?.SeatIndex ?? -1;
                if (seatIndex >= 0 && match.Seats != null && seatIndex < match.Seats.Length)
                {
                    localUserId = match.Seats[seatIndex];
                }
            }

            if (string.IsNullOrWhiteSpace(localUserId)) return false;
            if (match.Players == null) return false;
            if (!match.Players.TryGetValue(localUserId, out var player)) return false;
            if (player?.Hand == null) return false;

            cards = player.Hand.Cards;
            return cards != null && cards.Count > 0;
        }
    }
}
