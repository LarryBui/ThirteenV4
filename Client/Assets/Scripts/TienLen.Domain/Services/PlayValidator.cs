using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Services
{
    /// <summary>
    /// Provides client-side validation for play requests before they are sent to the server.
    /// </summary>
    public static class PlayValidator
    {
        /// <summary>
        /// Validates whether the selected cards can be played given the hand and current board.
        /// </summary>
        /// <param name="handCards">Cards in the player's hand.</param>
        /// <param name="selectedCards">Cards selected for play.</param>
        /// <param name="currentBoard">Current board cards, or empty for a new round.</param>
        /// <returns>Validation result with a reason when invalid.</returns>
        public static PlayValidationResult ValidatePlay(
            IReadOnlyList<Card> handCards,
            IReadOnlyList<Card> selectedCards,
            IReadOnlyList<Card> currentBoard)
        {
            if (selectedCards == null || selectedCards.Count == 0)
            {
                return PlayValidationResult.Invalid(PlayValidationReason.NoSelection);
            }

            if (handCards == null || handCards.Count == 0)
            {
                return PlayValidationResult.Invalid(PlayValidationReason.CardsNotInHand);
            }

            if (!HasCards(handCards, selectedCards))
            {
                return PlayValidationResult.Invalid(PlayValidationReason.CardsNotInHand);
            }

            if (!GameRules.IsValidSet(selectedCards))
            {
                return PlayValidationResult.Invalid(PlayValidationReason.InvalidCombination);
            }

            if (currentBoard != null && currentBoard.Count > 0 && !GameRules.CanBeat(currentBoard, selectedCards))
            {
                return PlayValidationResult.Invalid(PlayValidationReason.CannotBeat);
            }

            return PlayValidationResult.Valid();
        }

        /// <summary>
        /// Determines if a player can pass the current turn based on board state.
        /// </summary>
        /// <param name="currentBoard">Current board cards.</param>
        /// <returns>True when passing is allowed; otherwise false.</returns>
        public static bool CanPass(IReadOnlyList<Card> currentBoard)
        {
            return currentBoard != null && currentBoard.Count > 0;
        }

        private static bool HasCards(IReadOnlyList<Card> handCards, IReadOnlyList<Card> selectedCards)
        {
            var counts = new Dictionary<Card, int>(handCards.Count);
            for (var i = 0; i < handCards.Count; i++)
            {
                var card = handCards[i];
                counts.TryGetValue(card, out var current);
                counts[card] = current + 1;
            }

            for (var i = 0; i < selectedCards.Count; i++)
            {
                var card = selectedCards[i];
                if (!counts.TryGetValue(card, out var current) || current == 0)
                {
                    return false;
                }

                counts[card] = current - 1;
            }

            return true;
        }
    }

    /// <summary>
    /// Enumerates reasons why a play validation can fail.
    /// </summary>
    public enum PlayValidationReason
    {
        None = 0,
        NoSelection = 1,
        CardsNotInHand = 2,
        InvalidCombination = 3,
        CannotBeat = 4
    }

    /// <summary>
    /// Represents the result of a play validation check.
    /// </summary>
    public readonly struct PlayValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation succeeded.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the reason for invalid results.
        /// </summary>
        public PlayValidationReason Reason { get; }

        private PlayValidationResult(bool isValid, PlayValidationReason reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        /// <summary>
        /// Creates a valid result.
        /// </summary>
        public static PlayValidationResult Valid()
        {
            return new PlayValidationResult(true, PlayValidationReason.None);
        }

        /// <summary>
        /// Creates an invalid result with the supplied reason.
        /// </summary>
        /// <param name="reason">Reason for validation failure.</param>
        /// <returns>An invalid validation result.</returns>
        public static PlayValidationResult Invalid(PlayValidationReason reason)
        {
            return new PlayValidationResult(false, reason);
        }
    }
}
