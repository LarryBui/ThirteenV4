using System.Collections.Generic;

namespace TienLen.Application
{
    /// <summary>
    /// Request payload describing a rigged deck to start a match with.
    /// </summary>
    public sealed class RiggedDeckRequestDto
    {
        /// <summary>Match identifier to apply the rigged deck to.</summary>
        public string MatchId { get; }
        /// <summary>Seat-to-cards assignments for the rigged start.</summary>
        public IReadOnlyList<RiggedHandDto> Hands { get; }
        /// <summary>Seat-to-text assignments for the rigged start (parsed server-side).</summary>
        public IReadOnlyList<RiggedHandTextDto> HandTexts { get; }

        /// <summary>
        /// Creates a new rigged deck request.
        /// </summary>
        /// <param name="matchId">Match identifier.</param>
        /// <param name="hands">Rigged hands by seat.</param>
        public RiggedDeckRequestDto(string matchId, IReadOnlyList<RiggedHandDto> hands)
            : this(matchId, hands, null)
        {
        }

        /// <summary>
        /// Creates a new rigged deck request with optional raw text inputs.
        /// </summary>
        /// <param name="matchId">Match identifier.</param>
        /// <param name="hands">Rigged hands by seat.</param>
        /// <param name="handTexts">Rigged hand card lists in raw string form.</param>
        public RiggedDeckRequestDto(string matchId, IReadOnlyList<RiggedHandDto> hands, IReadOnlyList<RiggedHandTextDto> handTexts)
        {
            MatchId = matchId;
            Hands = hands ?? new List<RiggedHandDto>();
            HandTexts = handTexts ?? new List<RiggedHandTextDto>();
        }
    }

    /// <summary>
    /// Seat-specific hand definition for a rigged deck start.
    /// </summary>
    public sealed class RiggedHandDto
    {
        /// <summary>Seat index (0-based).</summary>
        public int Seat { get; }
        /// <summary>Cards to assign to the seat.</summary>
        public IReadOnlyList<RiggedCardDto> Cards { get; }

        /// <summary>
        /// Creates a new rigged hand.
        /// </summary>
        /// <param name="seat">Seat index.</param>
        /// <param name="cards">Cards assigned to the seat.</param>
        public RiggedHandDto(int seat, IReadOnlyList<RiggedCardDto> cards)
        {
            Seat = seat;
            Cards = cards ?? new List<RiggedCardDto>();
        }
    }

    /// <summary>
    /// Serializable card payload used for rigged deck requests.
    /// </summary>
    public readonly struct RiggedCardDto
    {
        /// <summary>Rank value (0=3 ... 12=2).</summary>
        public int Rank { get; }
        /// <summary>Suit value (0=Spades, 1=Clubs, 2=Diamonds, 3=Hearts).</summary>
        public int Suit { get; }

        /// <summary>
        /// Creates a new rigged card payload.
        /// </summary>
        /// <param name="rank">Rank value.</param>
        /// <param name="suit">Suit value.</param>
        public RiggedCardDto(int rank, int suit)
        {
            Rank = rank;
            Suit = suit;
        }
    }

    /// <summary>
    /// Seat-specific rigged hand definition expressed as a raw card list string.
    /// </summary>
    public sealed class RiggedHandTextDto
    {
        /// <summary>Seat index (0-based).</summary>
        public int Seat { get; }
        /// <summary>Raw card list text for the seat.</summary>
        public string Cards { get; }

        /// <summary>
        /// Creates a new rigged hand text payload.
        /// </summary>
        /// <param name="seat">Seat index.</param>
        /// <param name="cards">Raw card list text.</param>
        public RiggedHandTextDto(int seat, string cards)
        {
            Seat = seat;
            Cards = cards ?? string.Empty;
        }
    }
}
