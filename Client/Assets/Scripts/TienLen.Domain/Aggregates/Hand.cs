using System;
using System.Collections.Generic;
using System.Linq;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    public class Hand
    {
        private readonly List<Card> _cards = new List<Card>();

        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

        public void AddCards(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
            SortHand();
        }

        public void RemoveCards(IEnumerable<Card> cardsToRemove)
        {
            foreach (var card in cardsToRemove)
            {
                if (!_cards.Remove(card))
                {
                    throw new InvalidOperationException($"Card {card} not found in hand.");
                }
            }
        }

        // Sorts specifically for UI display and logic: 3s on left, 2s on right
        private void SortHand()
        {
            _cards.Sort();
        }

        public bool HasCards(IEnumerable<Card> targetCards)
        {
            var tempHand = new List<Card>(_cards);
            foreach (var card in targetCards)
            {
                if (!tempHand.Remove(card))
                {
                    return false;
                }
            }
            return true;
        }
    }
}