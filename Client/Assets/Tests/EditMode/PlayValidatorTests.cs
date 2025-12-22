using System.Collections.Generic;
using NUnit.Framework;
using TienLen.Domain.Enums;
using TienLen.Domain.Services;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Tests
{
    public sealed class PlayValidatorTests
    {
        [Test]
        public void ValidatePlay_NoSelectionReturnsNoSelection()
        {
            var hand = Cards(new Card(Rank.Three, Suit.Spades));
            var result = PlayValidator.ValidatePlay(hand, new List<Card>(), new List<Card>());
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(PlayValidationReason.NoSelection, result.Reason);
        }

        [Test]
        public void ValidatePlay_CardsNotInHandReturnsCardsNotInHand()
        {
            var hand = Cards(new Card(Rank.Three, Suit.Spades));
            var selection = Cards(new Card(Rank.Four, Suit.Clubs));
            var result = PlayValidator.ValidatePlay(hand, selection, new List<Card>());
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(PlayValidationReason.CardsNotInHand, result.Reason);
        }

        [Test]
        public void ValidatePlay_InvalidCombinationReturnsInvalidCombination()
        {
            var hand = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs));
            var selection = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Four, Suit.Clubs));
            var result = PlayValidator.ValidatePlay(hand, selection, new List<Card>());
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(PlayValidationReason.InvalidCombination, result.Reason);
        }

        [Test]
        public void ValidatePlay_CannotBeatReturnsCannotBeat()
        {
            var hand = Cards(new Card(Rank.Three, Suit.Spades));
            var selection = Cards(new Card(Rank.Three, Suit.Spades));
            var board = Cards(new Card(Rank.Four, Suit.Hearts));
            var result = PlayValidator.ValidatePlay(hand, selection, board);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(PlayValidationReason.CannotBeat, result.Reason);
        }

        [Test]
        public void ValidatePlay_ValidOnNewRoundReturnsValid()
        {
            var hand = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs));
            var selection = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs));
            var result = PlayValidator.ValidatePlay(hand, selection, new List<Card>());
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void CanPass_ReturnsFalseOnNewRound()
        {
            Assert.IsFalse(PlayValidator.CanPass(new List<Card>()));
        }

        [Test]
        public void CanPass_ReturnsTrueWhenBoardHasCards()
        {
            Assert.IsTrue(PlayValidator.CanPass(Cards(new Card(Rank.Three, Suit.Spades))));
        }

        [Test]
        public void HasPlayableMove_ReturnsFalseWhenBoardEmpty()
        {
            var hand = Cards(new Card(Rank.Three, Suit.Spades));
            Assert.IsFalse(PlayValidator.HasPlayableMove(hand, new List<Card>()));
        }

        [Test]
        public void HasPlayableMove_ReturnsTrueForHigherSingle()
        {
            var hand = Cards(new Card(Rank.Three, Suit.Clubs));
            var board = Cards(new Card(Rank.Three, Suit.Spades));
            Assert.IsTrue(PlayValidator.HasPlayableMove(hand, board));
        }

        [Test]
        public void HasPlayableMove_ReturnsFalseWhenSingleTwoCannotBeBeaten()
        {
            var hand = Cards(
                new Card(Rank.Ace, Suit.Spades),
                new Card(Rank.King, Suit.Clubs),
                new Card(Rank.Three, Suit.Hearts));
            var board = Cards(new Card(Rank.Two, Suit.Hearts));
            Assert.IsFalse(PlayValidator.HasPlayableMove(hand, board));
        }

        [Test]
        public void HasPlayableMove_ReturnsTrueForQuadAgainstSingleTwo()
        {
            var hand = Cards(
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Three, Suit.Clubs),
                new Card(Rank.Three, Suit.Diamonds),
                new Card(Rank.Three, Suit.Hearts));
            var board = Cards(new Card(Rank.Two, Suit.Hearts));
            Assert.IsTrue(PlayValidator.HasPlayableMove(hand, board));
        }

        private static List<Card> Cards(params Card[] cards)
        {
            return new List<Card>(cards);
        }
    }
}
