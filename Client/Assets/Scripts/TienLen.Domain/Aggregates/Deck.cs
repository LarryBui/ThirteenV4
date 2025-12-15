using System;
using System.Collections.Generic;
using TienLen.Domain.Enums;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    /// <summary>
    /// Represents a standard 52-card deck with shuffle and draw helpers.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<Card> _cards;
        private readonly Random _random;

        /// <summary>
        /// Creates a shuffled standard deck. Optionally accepts a <see cref="Random"/> instance for deterministic shuffles in tests.
        /// </summary>
        /// <param name="random">Optional random generator to control shuffle order.</param>
        public Deck(Random random = null)
        {
            _random = random ?? new Random();
            _cards = CreateStandardDeck();
            Shuffle();
        }

        /// <summary>
        /// Remaining card count in the deck.
        /// </summary>
        public int Count => _cards.Count;

        /// <summary>
        /// Draws all remaining cards from the deck in their current shuffled order.
        /// </summary>
        /// <returns>Sequence of cards, consuming the deck.</returns>
        public IEnumerable<Card> DrawAll()
        {
            while (_cards.Count > 0)
            {
                var index = _cards.Count - 1;
                var card = _cards[index];
                _cards.RemoveAt(index);
                yield return card;
            }
        }

        private List<Card> CreateStandardDeck()
        {
            var deck = new List<Card>(52);
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                {
                    deck.Add(new Card(rank, suit));
                }
            }
            return deck;
        }

        private void Shuffle()
        {
            var n = _cards.Count;
            while (n > 1)
            {
                n--;
                var k = _random.Next(n + 1);
                (_cards[n], _cards[k]) = (_cards[k], _cards[n]);
            }
        }
    }
}
