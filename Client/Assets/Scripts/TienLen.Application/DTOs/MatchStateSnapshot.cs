using System;
using System.Collections.Generic;

namespace TienLen.Application
{
    /// <summary>
    /// Immutable snapshot of server match state for lobby synchronization.
    /// </summary>
    public class MatchStateSnapshotDto
    {
        public string[] Seats { get; set; }
        public int OwnerSeat { get; set; }
        public long Tick { get; set; }
        public PlayerStateDto[] Players { get; set; }
        public long TurnSecondsRemaining { get; set; }
        public int Type { get; set; }
    }

    public class PlayerStateDto
    {
        public string UserId { get; set; }
        public int Seat { get; set; }
        public bool IsOwner { get; set; }
        public int CardsRemaining { get; set; }
        public string DisplayName { get; set; }
        public int AvatarIndex { get; set; }
        public long Balance { get; set; }
        public bool IsVip { get; set; }
    }
}
