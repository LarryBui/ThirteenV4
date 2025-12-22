using System.Collections.Generic;
using NUnit.Framework;
using TienLen.Domain.Enums;
using TienLen.Domain.Services;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Tests
{
    public sealed class GameRulesTests
    {
        [Test]
        public void IdentifyCombination_Single()
        {
            var cards = Cards(new Card(Rank.Three, Suit.Spades));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Single, combo.Type);
        }

        [Test]
        public void IdentifyCombination_Pair()
        {
            var cards = Cards(new Card(Rank.Three, Suit.Spades), new Card(Rank.Three, Suit.Clubs));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Pair, combo.Type);
        }

        [Test]
        public void IdentifyCombination_Triple()
        {
            var cards = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Three, Suit.Diamonds));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Triple, combo.Type);
        }

        [Test]
        public void IdentifyCombination_QuadIsBomb()
        {
            var cards = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Three, Suit.Diamonds),
                new Card(Rank.Three, Suit.Hearts));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Bomb, combo.Type);
        }

        [Test]
        public void IdentifyCombination_Straight()
        {
            var cards = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Diamonds));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Straight, combo.Type);
        }

        [Test]
        public void IdentifyCombination_ThreeConsecutivePairsIsBomb()
        {
            var cards = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Bomb, combo.Type);
        }

        [Test]
        public void IdentifyCombination_InvalidStraightWithTwo()
        {
            var cards = Cards(
                new Card(Rank.King, Suit.Spades),
                new Card(Rank.Ace, Suit.Clubs),
                new Card(Rank.Two, Suit.Diamonds));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Invalid, combo.Type);
        }

        [Test]
        public void IdentifyCombination_InvalidConsecutivePairsWithTwo()
        {
            var cards = Cards(
                new Card(Rank.Ace, Suit.Spades),
                new Card(Rank.Ace, Suit.Clubs),
                new Card(Rank.Two, Suit.Spades),
                new Card(Rank.Two, Suit.Clubs),
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Invalid, combo.Type);
        }

        [Test]
        public void IdentifyCombination_InvalidNonConsecutivePairs()
        {
            var cards = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs));
            var combo = GameRules.IdentifyCombination(cards);
            Assert.AreEqual(CardCombinationType.Invalid, combo.Type);
        }

        [Test]
        public void CanBeat_HigherSingleBeatsLowerSingle()
        {
            var prev = Cards(new Card(Rank.Three, Suit.Spades));
            var next = Cards(new Card(Rank.Three, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_HigherSuitInPair()
        {
            var prev = Cards(new Card(Rank.Eight, Suit.Spades), new Card(Rank.Eight, Suit.Clubs));
            var next = Cards(new Card(Rank.Eight, Suit.Diamonds), new Card(Rank.Eight, Suit.Hearts));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_ThreePineChopsSingleTwo()
        {
            var prev = Cards(new Card(Rank.Two, Suit.Spades));
            var next = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_QuadChopsSingleTwo()
        {
            var prev = Cards(new Card(Rank.Two, Suit.Hearts));
            var next = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Three, Suit.Diamonds),
                new Card(Rank.Three, Suit.Hearts));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_QuadChopsPairTwo()
        {
            var prev = Cards(new Card(Rank.Two, Suit.Spades), new Card(Rank.Two, Suit.Clubs));
            var next = Cards(
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Four, Suit.Diamonds),
                new Card(Rank.Four, Suit.Hearts));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_QuadChopsThreePine()
        {
            var prev = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs));
            var next = Cards(
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs),
                new Card(Rank.Six, Suit.Diamonds),
                new Card(Rank.Six, Suit.Hearts));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_FourPineChopsSingleTwo()
        {
            var prev = Cards(new Card(Rank.Two, Suit.Hearts));
            var next = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_FourPineChopsQuad()
        {
            var prev = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Three, Suit.Diamonds),
                new Card(Rank.Three, Suit.Hearts));
            var next = Cards(
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Seven, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_FourPineChopsPairTwo()
        {
            var prev = Cards(new Card(Rank.Two, Suit.Spades), new Card(Rank.Two, Suit.Clubs));
            var next = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_FivePineChopsFourPine()
        {
            var prev = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs));
            var next = Cards(
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Seven, Suit.Clubs),
                new Card(Rank.Eight, Suit.Spades),
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Nine, Suit.Spades),
                new Card(Rank.Nine, Suit.Clubs),
                new Card(Rank.Ten, Suit.Spades),
                new Card(Rank.Ten, Suit.Clubs),
                new Card(Rank.Jack, Suit.Spades),
                new Card(Rank.Jack, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        [Test]
        public void CanBeat_HigherThreePineBeatsLowerThreePine()
        {
            var prev = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs));
            var next = Cards(
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Six, Suit.Spades),
                new Card(Rank.Six, Suit.Clubs));
            Assert.IsTrue(GameRules.CanBeat(prev, next));
        }

        private static List<Card> Cards(params Card[] cards)
        {
            return new List<Card>(cards);
        }
    }
}
