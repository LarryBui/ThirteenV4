using TienLen.Domain.Enums;
using TienLen.Domain.ValueObjects;

namespace TienLen.Presentation.GameRoomScreen.Utils
{
    /// <summary>
    /// Formats card values for UI display using rank shorthand and Unicode suit symbols.
    /// </summary>
    public static class CardTextFormatter
    {
        /// <summary>
        /// Formats a card as a short string (e.g., "A♠").
        /// </summary>
        /// <param name="card">Card to format.</param>
        public static string FormatShort(Card card)
        {
            return $"{ToRankString(card.Rank)}{ToSuitSymbol(card.Suit)}";
        }

        /// <summary>
        /// Converts a rank to its short text representation (e.g., "K").
        /// </summary>
        /// <param name="rank">Rank to convert.</param>
        public static string ToRankString(Rank rank)
        {
            return rank switch
            {
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                Rank.Two => "2",
                _ => rank.ToString()
            };
        }

        /// <summary>
        /// Converts a suit to its Unicode symbol (e.g., "♠").
        /// </summary>
        /// <param name="suit">Suit to convert.</param>
        public static string ToSuitSymbol(Suit suit)
        {
            return suit switch
            {
                Suit.Spades => "\u2660",
                Suit.Clubs => "\u2663",
                Suit.Diamonds => "\u2666",
                Suit.Hearts => "\u2665",
                _ => suit.ToString()
            };
        }
    }
}
