using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Application
{
    /// <summary>
    /// Data Transfer Object representing the final result of a game.
    /// This aligns with the server's GameEndedEvent payload.
    /// </summary>
    public sealed class GameEndedResultDto
    {
        /// <summary>
        /// Seat indices in the order they finished (1st, 2nd, 3rd, 4th).
        /// </summary>
        public IReadOnlyList<int> FinishOrder { get; }

        /// <summary>
        /// Map of seat index to the list of cards remaining in their hand.
        /// </summary>
        public IReadOnlyDictionary<int, List<Card>> RemainingHands { get; }

        /// <summary>
        /// Map of userId to the gold balance change (+/-).
        /// </summary>
        public IReadOnlyDictionary<string, long> BalanceChanges { get; }

        public GameEndedResultDto(
            IReadOnlyList<int> finishOrder,
            IReadOnlyDictionary<int, List<Card>> remainingHands,
            IReadOnlyDictionary<string, long> balanceChanges)
        {
            FinishOrder = finishOrder ?? new List<int>();
            RemainingHands = remainingHands ?? new Dictionary<int, List<Card>>();
            BalanceChanges = balanceChanges ?? new Dictionary<string, long>();
        }
    }
}
