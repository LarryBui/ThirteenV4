using System.Collections.Generic;
using TienLen.Domain.Aggregates;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Coordinates all player seats at the table.
    /// Handles the mapping of seat indices to relative screen positions (South, East, North, West).
    /// </summary>
    public sealed class PlayerSeatsManagerView : MonoBehaviour
    {
        private const int SeatCount = 4;

        [Header("Seat Visuals")]
        [SerializeField] private PlayerSeatView _southSeat; // Always Local Player
        [SerializeField] private PlayerSeatView _eastSeat;  // Counter-clockwise from local
        [SerializeField] private PlayerSeatView _northSeat;
        [SerializeField] private PlayerSeatView _westSeat;

        private TienLen.Infrastructure.Config.AvatarRegistry _avatarRegistry;
        private readonly Dictionary<int, PlayerSeatView> _seatIndexToViewMap = new Dictionary<int, PlayerSeatView>();

        public void Configure(TienLen.Infrastructure.Config.AvatarRegistry avatarRegistry)
        {
            _avatarRegistry = avatarRegistry;
        }

        /// <summary>
        /// Synchronizes the visual seats with the current match state.
        /// </summary>
        /// <param name="match">Current match data.</param>
        /// <param name="localSeatIndex">The seat index of the local user.</param>
        public void RefreshSeats(Match match, int localSeatIndex)
        {
            _seatIndexToViewMap.Clear();
            ClearAllSeats();

            if (match == null || match.Seats == null) return;

            // Mapping logic:
            // Relative 0 (Local) -> South
            // Relative 1 -> East
            // Relative 2 -> North
            // Relative 3 -> West
            
            int baseSeat = localSeatIndex >= 0 ? localSeatIndex : 0;

            for (int i = 0; i < SeatCount; i++)
            {
                int currentSeatIndex = (baseSeat + i) % SeatCount;
                string userId = match.Seats[currentSeatIndex];
                
                var view = GetViewByRelativeIndex(i);
                if (view == null) continue;

                if (string.IsNullOrEmpty(userId))
                {
                    view.ClearProfile();
                    view.SetActive(false);
                    continue;
                }

                _seatIndexToViewMap[currentSeatIndex] = view;

                if (match.Players != null && match.Players.TryGetValue(userId, out var player))
                {
                    Sprite avatar = _avatarRegistry != null ? _avatarRegistry.GetAvatar(player.AvatarIndex) : null;
                    view.SetProfile(player.DisplayName, avatar, currentSeatIndex, currentSeatIndex == match.OwnerSeat);
                    view.SetBalance(player.Balance);
                }
                else
                {
                    // Fallback for identified but missing player data
                    Sprite fallbackAvatar = _avatarRegistry != null ? _avatarRegistry.GetAvatar(0) : null;
                    view.SetProfile($"Player {userId.Substring(0, Mathf.Min(userId.Length, 4))}", fallbackAvatar, currentSeatIndex, false);
                    view.SetBalance(0);
                }
            }
        }

        public PlayerSeatView GetViewBySeatIndex(int seatIndex)
        {
            _seatIndexToViewMap.TryGetValue(seatIndex, out var view);
            return view;
        }

        public void SetTurnActive(int seatIndex, bool active)
        {
            var view = GetViewBySeatIndex(seatIndex);
            view?.SetTurnActive(active);
        }

        public void ClearAllSeats()
        {
            _southSeat?.ClearProfile();
            _eastSeat?.ClearProfile();
            _northSeat?.ClearProfile();
            _westSeat?.ClearProfile();
        }

        /// <summary>
        /// Returns the anchor for a player based on their relative position.
        /// 0=South (Local), 1=East, 2=North, 3=West.
        /// </summary>
        public RectTransform GetAnchorByRelativeIndex(int relativeIndex)
        {
            var view = GetViewByRelativeIndex(relativeIndex);
            return view != null ? view.CardSourceAnchor : null;
        }

        private PlayerSeatView GetViewByRelativeIndex(int index)
        {
            return index switch
            {
                0 => _southSeat,
                1 => _eastSeat,
                2 => _northSeat,
                3 => _westSeat,
                _ => null
            };
        }
    }
}
