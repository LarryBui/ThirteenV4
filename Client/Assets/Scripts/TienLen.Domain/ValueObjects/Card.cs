using System;
using TienLen.Domain.Enums;

namespace TienLen.Domain.ValueObjects
{
    public struct Card : IComparable<Card>, IEquatable<Card>
    {
        public Suit Suit { get; set; }
        public Rank Rank { get; set; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        // Tien Len Power Calculation:
        // Rank has primary weight (multiplied by 4), Suit has secondary weight.
        public int PowerValue => ((int)Rank * 4) + (int)Suit;

        public int CompareTo(Card other) => PowerValue.CompareTo(other.PowerValue);

        public bool Equals(Card other) => PowerValue == other.PowerValue;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => PowerValue.GetHashCode();

        public override string ToString() => $"{Rank} of {Suit}";

        public static bool operator >(Card a, Card b) => a.PowerValue > b.PowerValue;
        public static bool operator <(Card a, Card b) => a.PowerValue < b.PowerValue;
        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);
    }
}