using System;
using System.Collections.Generic;

namespace TienLen.Application
{
    /// <summary>
    /// Immutable snapshot of server match state for lobby synchronization.
    /// </summary>
    public readonly struct MatchStateSnapshotDto
    {
        /// <summary>Seat assignments in order (empty strings for open seats).</summary>
        public string[] Seats { get; }
        /// <summary>Seat index of the current match owner.</summary>
        public int OwnerSeat { get; }
        /// <summary>Server tick when the snapshot was generated.</summary>
        public long Tick { get; }
        /// <summary>Full list of player details in the match.</summary>
        public IReadOnlyList<PlayerStateDTO> Players { get; }

        /// <summary>
        /// Creates a new snapshot, copying seat values to avoid external mutation.
        /// </summary>
        public MatchStateSnapshotDto(string[] seats, int ownerSeat, long tick, IReadOnlyList<PlayerStateDTO> players)
        {
            Seats = seats == null ? Array.Empty<string>() : (string[])seats.Clone();
            OwnerSeat = ownerSeat;
            Tick = tick;
            Players = players ?? Array.Empty<PlayerStateDTO>();
        }
    }

    /// <summary>
    /// Detailed state of a player in a match snapshot.
    /// </summary>
    public readonly struct PlayerStateDTO
    {
        public string UserId { get; }
        public int Seat { get; }
        public bool IsOwner { get; }
        public int CardsRemaining { get; }
        public string DisplayName { get; }
        public int AvatarIndex { get; }

        public PlayerStateDTO(string userId, int seat, bool isOwner, int cardsRemaining, string displayName, int avatarIndex)
        {
            UserId = userId;
            Seat = seat;
            IsOwner = isOwner;
            CardsRemaining = cardsRemaining;
            DisplayName = displayName;
            AvatarIndex = avatarIndex;
        }
    }
}