using System;
using System.Collections.Generic;
using TienLen.Domain.Enums;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Services
{
    /// <summary>
    /// Provides client-side Tien Len rule evaluation that mirrors the server domain.
    /// </summary>
    public static class GameRules
    {
        /// <summary>
        /// Determines whether the provided cards form a legal Tien Len set.
        /// </summary>
        /// <param name="cards">Cards to evaluate.</param>
        /// <returns>True when the set is valid; otherwise false.</returns>
        public static bool IsValidSet(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return false;
            }

            if (cards.Count == 1)
            {
                return true;
            }

            if (AllSameRank(cards))
            {
                return cards.Count <= 4;
            }

            if (IsStraight(cards))
            {
                return true;
            }

            if (IsConsecutivePairs(cards))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the new cards can beat the previous cards according to Tien Len rules.
        /// </summary>
        /// <param name="prevCards">Previously played cards.</param>
        /// <param name="newCards">New cards to compare.</param>
        /// <returns>True when the new cards can beat the previous cards; otherwise false.</returns>
        public static bool CanBeat(IReadOnlyList<Card> prevCards, IReadOnlyList<Card> newCards)
        {
            if (prevCards == null || newCards == null)
            {
                return false;
            }

            var isNewQuad = IsQuad(newCards);
            var isNew3Pine = IsThreeConsecutivePairs(newCards);
            var isNew4Pine = IsFourConsecutivePairs(newCards);
            var isNew5Pine = IsFiveConsecutivePairs(newCards);

            var isPrevSingle2 = prevCards.Count == 1 && prevCards[0].Rank == Rank.Two;
            var isPrevPair2 = prevCards.Count == 2 && AllSameRank(prevCards) && prevCards[0].Rank == Rank.Two;
            var isPrevQuad = IsQuad(prevCards);
            var isPrev3Pine = IsThreeConsecutivePairs(prevCards);
            var isPrev4Pine = IsFourConsecutivePairs(prevCards);
            var isPrev5Pine = IsFiveConsecutivePairs(prevCards);

            if (isNew5Pine)
            {
                if (isPrevSingle2 || isPrevPair2 || isPrevQuad || isPrev4Pine || isPrev3Pine)
                {
                    return true;
                }

                if (isPrev5Pine)
                {
                    return GetMaxPower(newCards) > GetMaxPower(prevCards);
                }
            }

            if (isNew4Pine)
            {
                if (isPrevSingle2 || isPrevPair2 || isPrevQuad || isPrev3Pine)
                {
                    return true;
                }

                if (isPrev4Pine)
                {
                    return GetMaxPower(newCards) > GetMaxPower(prevCards);
                }
            }

            if (isNewQuad)
            {
                if (isPrevSingle2 || isPrevPair2 || isPrev3Pine)
                {
                    return true;
                }

                if (isPrevQuad)
                {
                    return newCards[0].Rank > prevCards[0].Rank;
                }
            }

            if (isNew3Pine)
            {
                if (isPrevSingle2)
                {
                    return true;
                }

                if (isPrev3Pine)
                {
                    return GetMaxPower(newCards) > GetMaxPower(prevCards);
                }
            }

            if (prevCards.Count != newCards.Count)
            {
                return false;
            }

            return GetMaxPower(newCards) > GetMaxPower(prevCards);
        }

        /// <summary>
        /// Identifies the strongest combination type for the given cards.
        /// </summary>
        /// <param name="cards">Cards to evaluate.</param>
        /// <returns>The identified combination.</returns>
        public static CardCombination IdentifyCombination(IReadOnlyList<Card> cards)
        {
            if (!IsValidSet(cards))
            {
                return CardCombination.Invalid;
            }

            var sortedCards = SortByPower(cards);
            var count = sortedCards.Length;

            if (count == 1)
            {
                return new CardCombination(CardCombinationType.Single, sortedCards, sortedCards[0].PowerValue);
            }

            if (AllSameRank(sortedCards))
            {
                var value = sortedCards[count - 1].PowerValue;
                switch (count)
                {
                    case 2:
                        return new CardCombination(CardCombinationType.Pair, sortedCards, value);
                    case 3:
                        return new CardCombination(CardCombinationType.Triple, sortedCards, value);
                    case 4:
                        return new CardCombination(CardCombinationType.Bomb, sortedCards, value);
                }
            }

            if (IsStraight(sortedCards))
            {
                return new CardCombination(CardCombinationType.Straight, sortedCards, sortedCards[count - 1].PowerValue);
            }

            if (IsConsecutivePairs(sortedCards))
            {
                return new CardCombination(CardCombinationType.Bomb, sortedCards, sortedCards[count - 1].PowerValue);
            }

            return CardCombination.Invalid;
        }

        /// <summary>
        /// Checks whether the new play is a chop and returns the chop type name.
        /// </summary>
        /// <param name="prevCards">Previously played cards.</param>
        /// <param name="newCards">New cards to compare.</param>
        /// <param name="chopType">Outputs the chop type name when a chop occurs.</param>
        /// <returns>True if the play is a chop; otherwise false.</returns>
        public static bool TryDetectChop(IReadOnlyList<Card> prevCards, IReadOnlyList<Card> newCards, out string chopType)
        {
            chopType = string.Empty;

            if (!CanBeat(prevCards, newCards))
            {
                return false;
            }

            var isNew3Pine = IsThreeConsecutivePairs(newCards);
            var isNew4Pine = IsFourConsecutivePairs(newCards);
            var isNew5Pine = IsFiveConsecutivePairs(newCards);
            var isNewQuad = IsQuad(newCards);

            var isPrevSingle2 = prevCards.Count == 1 && prevCards[0].Rank == Rank.Two;
            var isPrevPair2 = prevCards.Count == 2 && AllSameRank(prevCards) && prevCards[0].Rank == Rank.Two;
            var isPrev3Pine = IsThreeConsecutivePairs(prevCards);
            var isPrev4Pine = IsFourConsecutivePairs(prevCards);
            var isPrevQuad = IsQuad(prevCards);

            if (isNew3Pine)
            {
                if (isPrevSingle2 || isPrev3Pine)
                {
                    chopType = "3-Pine";
                    return true;
                }
            }

            if (isNewQuad)
            {
                if (isPrevSingle2 || isPrevPair2 || isPrev3Pine)
                {
                    chopType = "Quad";
                    return true;
                }

                if (isPrevQuad)
                {
                    chopType = "Quad";
                    return true;
                }
            }

            if (isNew4Pine)
            {
                if (isPrevSingle2 || isPrevPair2 || isPrevQuad || isPrev3Pine)
                {
                    chopType = "4-Pine";
                    return true;
                }

                if (isPrev4Pine)
                {
                    chopType = "4-Pine";
                    return true;
                }
            }

            if (isNew5Pine)
            {
                chopType = "5-Pine";
                return true;
            }

            return false;
        }

        private static bool IsQuad(IReadOnlyList<Card> cards)
        {
            return cards != null && cards.Count == 4 && AllSameRank(cards);
        }

        private static bool IsThreeConsecutivePairs(IReadOnlyList<Card> cards)
        {
            return cards != null && cards.Count == 6 && IsConsecutivePairs(cards);
        }

        private static bool IsFourConsecutivePairs(IReadOnlyList<Card> cards)
        {
            return cards != null && cards.Count == 8 && IsConsecutivePairs(cards);
        }

        private static bool IsFiveConsecutivePairs(IReadOnlyList<Card> cards)
        {
            return cards != null && cards.Count == 10 && IsConsecutivePairs(cards);
        }

        private static int GetMaxPower(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return -1;
            }

            var maxPower = cards[0].PowerValue;
            for (var i = 1; i < cards.Count; i++)
            {
                var power = cards[i].PowerValue;
                if (power > maxPower)
                {
                    maxPower = power;
                }
            }

            return maxPower;
        }

        private static bool AllSameRank(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return false;
            }

            var rank = cards[0].Rank;
            for (var i = 1; i < cards.Count; i++)
            {
                if (cards[i].Rank != rank)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStraight(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count < 3)
            {
                return false;
            }

            var ranks = new List<int>(cards.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                var rank = cards[i].Rank;
                if (rank == Rank.Two)
                {
                    return false;
                }

                ranks.Add((int)rank);
            }

            ranks.Sort();

            for (var i = 1; i < ranks.Count; i++)
            {
                if (ranks[i] == ranks[i - 1])
                {
                    return false;
                }

                if (ranks[i] != ranks[i - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsConsecutivePairs(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count < 6 || cards.Count % 2 != 0)
            {
                return false;
            }

            var ranks = new List<int>(cards.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                var rank = cards[i].Rank;
                if (rank == Rank.Two)
                {
                    return false;
                }

                ranks.Add((int)rank);
            }

            ranks.Sort();

            var pairRanks = new List<int>(ranks.Count / 2);
            for (var i = 0; i < ranks.Count; i += 2)
            {
                if (ranks[i] != ranks[i + 1])
                {
                    return false;
                }

                pairRanks.Add(ranks[i]);
            }

            for (var i = 1; i < pairRanks.Count; i++)
            {
                if (pairRanks[i] != pairRanks[i - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static Card[] SortByPower(IReadOnlyList<Card> cards)
        {
            var sorted = new Card[cards.Count];
            for (var i = 0; i < cards.Count; i++)
            {
                sorted[i] = cards[i];
            }

            Array.Sort(sorted);
            return sorted;
        }
    }

    /// <summary>
    /// Represents the type of card combination detected by the rules.
    /// </summary>
    public enum CardCombinationType
    {
        Invalid = 0,
        Single = 1,
        Pair = 2,
        Triple = 3,
        Quad = 4,
        Straight = 5,
        Bomb = 6
    }

    /// <summary>
    /// Represents a detected combination and its evaluated power.
    /// </summary>
    public readonly struct CardCombination
    {
        /// <summary>
        /// Represents an invalid combination result.
        /// </summary>
        public static CardCombination Invalid => new CardCombination(CardCombinationType.Invalid, Array.Empty<Card>(), 0);

        /// <summary>
        /// Combination classification.
        /// </summary>
        public CardCombinationType Type { get; }

        /// <summary>
        /// Cards that form the combination, sorted by power.
        /// </summary>
        public IReadOnlyList<Card> Cards { get; }

        /// <summary>
        /// Power value of the highest card in the combination.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Number of cards in the combination.
        /// </summary>
        public int Count => Cards.Count;

        /// <summary>
        /// Creates a new combination instance.
        /// </summary>
        /// <param name="type">Combination classification.</param>
        /// <param name="cards">Cards that form the combination.</param>
        /// <param name="value">Power value of the highest card.</param>
        public CardCombination(CardCombinationType type, IReadOnlyList<Card> cards, int value)
        {
            Type = type;
            Cards = cards ?? Array.Empty<Card>();
            Value = value;
        }
    }
}
