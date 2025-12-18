using System;
using TienLen.Application.Session;
using TienLen.Domain.Aggregates;
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

        [Header("Player Profiles")]
        [SerializeField] private PlayerProfileUI localPlayerProfile;
        [SerializeField] private PlayerProfileUI opponentProfile_1;
        [SerializeField] private PlayerProfileUI opponentProfile_2;
        [SerializeField] private PlayerProfileUI opponentProfile_3;

        private TienLenMatchHandler _matchHandler;
        private IGameSessionContext _gameSessionContext;

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
        }

        private void OnDestroy()
        {
            if (_matchHandler != null)
            {
                _matchHandler.GameRoomStateUpdated -= HandleGameRoomStateUpdated;
                _matchHandler.GameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
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

        public void OnStartGameClicked()
        {
            if (_matchHandler != null)
            {
                _matchHandler.StartGameAsync().Forget();
            }
            else
            {
                Debug.LogError("GameRoomController: Cannot start game, Match Handler is null.");
            }
        }
    }
}
