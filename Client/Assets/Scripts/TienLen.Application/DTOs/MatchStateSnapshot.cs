using System;

namespace TienLen.Application
{
    /// <summary>
    /// Immutable snapshot of server match state for lobby synchronization.
    /// </summary>
    public readonly struct MatchStateSnapshot
    {
        /// <summary>Seat assignments in order (empty strings for open seats).</summary>
        public string[] Seats { get; }
        /// <summary>User id of the current match owner.</summary>
        public string OwnerId { get; }
        /// <summary>Server tick when the snapshot was generated.</summary>
        public long Tick { get; }

        /// <summary>
        /// Creates a new snapshot, copying seat values to avoid external mutation.
        /// </summary>
        /// <param name="seats">Seat assignments in order.</param>
        /// <param name="ownerId">Current match owner user id.</param>
        /// <param name="tick">Server tick value.</param>
        public MatchStateSnapshot(string[] seats, string ownerId, long tick)
        {
            Seats = seats == null ? Array.Empty<string>() : (string[])seats.Clone();
            OwnerId = ownerId;
            Tick = tick;
        }
    }
}
