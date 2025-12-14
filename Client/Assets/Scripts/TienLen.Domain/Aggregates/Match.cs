using System;
using System.Collections.Generic;
using System.Linq;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    public class Match
    {
        public Guid Id { get; }
        public List<Hand> PlayerHands { get; }
        public List<Card> CurrentBoard { get; private set; }
        public int CurrentTurnIndex { get; private set; }

        public Match(Guid id, int playerCount)
        {
            Id = id;
            PlayerHands = new List<Hand>();
            for (int i = 0; i < playerCount; i++)
            {
                PlayerHands.Add(new Hand());
            }
            CurrentBoard = new List<Card>();
            CurrentTurnIndex = 0;
        }

        public void DealCards()
        {
            var deck = CreateStandardDeck();
            Shuffle(deck);

            int playerIndex = 0;
            foreach (var card in deck)
            {
                // Ideally Hand.AddCard taking a single card would be better, but wrapping in list for now
                PlayerHands[playerIndex].AddCards(new List<Card> { card }); 
                playerIndex = (playerIndex + 1) % PlayerHands.Count;
            }
        }

        private List<Card> CreateStandardDeck()
        {
            var deck = new List<Card>();
            foreach (Domain.Enums.Rank rank in Enum.GetValues(typeof(Domain.Enums.Rank)))
            {
                foreach (Domain.Enums.Suit suit in Enum.GetValues(typeof(Domain.Enums.Suit)))
                {
                    deck.Add(new Card(rank, suit));
                }
            }
            return deck;
        }

        private void Shuffle<T>(List<T> list)
        {
            var rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public void PlayTurn(int playerIndex, List<Card> cards)
        {
            if (playerIndex != CurrentTurnIndex)
            {
                throw new InvalidOperationException("Not your turn.");
            }

            var hand = PlayerHands[playerIndex];
            if (!hand.HasCards(cards))
            {
                throw new InvalidOperationException("Player does not have specified cards.");
            }

            // TODO: Add Rule Engine validation here (is this a valid combo? does it beat the board?)
            
            hand.RemoveCards(cards);
            CurrentBoard = cards; 
            
            MoveToNextTurn();
        }

        public void SkipTurn(int playerIndex)
        {
             if (playerIndex != CurrentTurnIndex)
            {
                throw new InvalidOperationException("Not your turn.");
            }
            MoveToNextTurn();
        }

        private void MoveToNextTurn()
        {
            CurrentTurnIndex = (CurrentTurnIndex + 1) % PlayerHands.Count;
        }
    }
}