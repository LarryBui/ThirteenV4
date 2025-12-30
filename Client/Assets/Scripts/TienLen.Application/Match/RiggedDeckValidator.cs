using System.Collections.Generic;

namespace TienLen.Application
{
    /// <summary>
    /// Validates rigged deck requests before sending them to the server.
    /// </summary>
    public static class RiggedDeckValidator
    {
        private const int MaxSeats = 4;
        private const int MaxCardsPerSeat = 13;
        private const int MinRank = 0;
        private const int MaxRank = 12;
        private const int MinSuit = 0;
        private const int MaxSuit = 3;

        /// <summary>
        /// Validates a rigged deck request for seat/card integrity.
        /// </summary>
        /// <param name="request">Rigged deck request.</param>
        /// <param name="error">Validation error message when invalid.</param>
        /// <returns>True when the request is valid.</returns>
        public static bool TryValidate(RiggedDeckRequestDto request, out string error)
        {
            if (request == null)
            {
                error = "Rigged deck request is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.MatchId))
            {
                error = "Match id is required for rigged decks.";
                return false;
            }

            var hands = request.Hands ?? new List<RiggedHandDto>();
            var handTexts = request.HandTexts ?? new List<RiggedHandTextDto>();
            if (hands.Count == 0 && handTexts.Count == 0)
            {
                error = "Rigged deck requires at least one seat entry.";
                return false;
            }

            var seenSeats = new HashSet<int>();
            var seenCards = new HashSet<int>();

            foreach (var hand in hands)
            {
                if (hand == null)
                {
                    error = "Rigged hand entry is missing.";
                    return false;
                }

                if (hand.Seat < 0 || hand.Seat >= MaxSeats)
                {
                    error = $"Seat {hand.Seat} is out of range.";
                    return false;
                }

                if (!seenSeats.Add(hand.Seat))
                {
                    error = $"Seat {hand.Seat} is listed more than once.";
                    return false;
                }

                var cards = hand.Cards ?? new List<RiggedCardDto>();
                if (cards.Count > MaxCardsPerSeat)
                {
                    error = $"Seat {hand.Seat} has more than {MaxCardsPerSeat} cards.";
                    return false;
                }

                foreach (var card in cards)
                {
                    if (card.Rank < MinRank || card.Rank > MaxRank)
                    {
                        error = $"Card rank {card.Rank} is out of range.";
                        return false;
                    }

                    if (card.Suit < MinSuit || card.Suit > MaxSuit)
                    {
                        error = $"Card suit {card.Suit} is out of range.";
                        return false;
                    }

                    var key = (card.Rank * 4) + card.Suit;
                    if (!seenCards.Add(key))
                    {
                        error = "Duplicate card detected across rigged hands.";
                        return false;
                    }
                }
            }

            foreach (var handText in handTexts)
            {
                if (handText == null)
                {
                    error = "Rigged hand text entry is missing.";
                    return false;
                }

                if (handText.Seat < 0 || handText.Seat >= MaxSeats)
                {
                    error = $"Seat {handText.Seat} is out of range.";
                    return false;
                }

                if (!seenSeats.Add(handText.Seat))
                {
                    error = $"Seat {handText.Seat} is listed more than once.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(handText.Cards))
                {
                    error = $"Seat {handText.Seat} has no card input.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
