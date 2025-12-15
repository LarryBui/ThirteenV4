using System;
using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    /// <summary>
    /// Client-side match state modeled to mirror the server's MatchState.
    /// </summary>
    public class Match
    {
        /// <summary>Match identifier.</summary>
        public Guid Id { get; }
        /// <summary>Lifecycle phase (e.g., lobby, playing, ended).</summary>
        public string Phase { get; set; }
        /// <summary>UserId -> Player lookup.</summary>
        public Dictionary<string, Player> Players { get; }
        /// <summary>Seats indexed 0..N-1 containing userIds or empty strings.</summary>
        public string[] Seats { get; }
        /// <summary>UserId of the current owner.</summary>
        public string OwnerUserID { get; set; }
        /// <summary>Seat whose turn it is (1-based).</summary>
        public int CurrentTurnSeat { get; set; }
        /// <summary>Seat that led the current round (1-based).</summary>
        public int RoundLeaderSeat { get; set; }
        /// <summary>Seat that last played cards (1-based).</summary>
        public int LastPlaySeat { get; set; }
        /// <summary>Finish order (userIds) as players empty their hands.</summary>
        public List<string> FinishOrder { get; }
        /// <summary>Cards currently on the board.</summary>
        public List<Card> CurrentBoard { get; private set; }

        /// <summary>
        /// Initialize a new match with the given seat count (defaults to 4, matching server).
        /// </summary>
        /// <param name="id">Match identifier.</param>
        /// <param name="seatCount">Total seats available in the match.</param>
        public Match(Guid id, int seatCount = 4)
        {
            throw new NotImplementedException("Match domain model not implemented yet.");
        }

        /// <summary>
        /// Adds a player to the match, ensuring the seat array mirrors server-side state.
        /// </summary>
        /// <param name="player">Player to register.</param>
        public void RegisterPlayer(Player player)
        {
            throw new NotImplementedException("Match.RegisterPlayer not implemented yet.");
        }

        /// <summary>
        /// Deals a shuffled deck evenly across occupied seats in seating order.
        /// </summary>
        public void DealCards()
        {
            throw new NotImplementedException("Match.DealCards not implemented yet.");
        }

        /// <summary>
        /// Plays a turn for the specified player.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="cards">Cards to play.</param>
        public void PlayTurn(string userId, List<Card> cards)
        {
            throw new NotImplementedException("Match.PlayTurn not implemented yet.");
        }

        /// <summary>
        /// Skips the turn for the specified player.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        public void SkipTurn(string userId)
        {
            throw new NotImplementedException("Match.SkipTurn not implemented yet.");
        }
    }
}
