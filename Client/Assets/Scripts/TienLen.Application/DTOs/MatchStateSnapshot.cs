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
        /// <summary>Seconds remaining before the current turn expires.</summary>
        public long TurnSecondsRemaining { get; }
        /// <summary>Full list of player details in the match.</summary>
        public IReadOnlyList<PlayerStateDTO> Players { get; }

        /// <summary>
        /// Creates a new snapshot, copying seat values to avoid external mutation.
        /// </summary>
        public MatchStateSnapshotDto(string[] seats, int ownerSeat, long tick, long turnSecondsRemaining, IReadOnlyList<PlayerStateDTO> players)
        {
            Seats = seats == null ? Array.Empty<string>() : (string[])seats.Clone();
            OwnerSeat = ownerSeat;
            Tick = tick;
            TurnSecondsRemaining = turnSecondsRemaining;
            Players = players ?? Array.Empty<PlayerStateDTO>();
        }
    }

    /// <summary>
    /// Detailed state of a player in a match snapshot.
    /// </summary>
    public readonly struct PlayerStateDTO
    {
        /// <summary>Unique user id for the player.</summary>
        public string UserId { get; }
        /// <summary>Seat index for the player.</summary>
        public int Seat { get; }
        /// <summary>Whether the player owns the match.</summary>
        public bool IsOwner { get; }
        /// <summary>Number of cards remaining in the player's hand.</summary>
        public int CardsRemaining { get; }
        /// <summary>Display name shown in the UI.</summary>
        public string DisplayName { get; }
        /// <summary>Avatar index selected for the player.</summary>
        public int AvatarIndex { get; }
        /// <summary>Public balance reported by the server.</summary>
        public long Balance { get; }

        public PlayerStateDTO(string userId, int seat, bool isOwner, int cardsRemaining, string displayName, int avatarIndex, long balance)
        {
            UserId = userId;
            Seat = seat;
            IsOwner = isOwner;
            CardsRemaining = cardsRemaining;
            DisplayName = displayName;
            AvatarIndex = avatarIndex;
            Balance = balance;
        }
    }
}
