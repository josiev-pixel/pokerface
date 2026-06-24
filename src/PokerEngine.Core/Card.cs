using System;

namespace PokerEngine.Core
{
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3,
    }

    /// <summary>Card rank, valued so higher rank = higher number (Ace high = 14).</summary>
    public enum Rank
    {
        Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8,
        Nine = 9, Ten = 10, Jack = 11, Queen = 12, King = 13, Ace = 14,
    }

    /// <summary>
    /// A single playing card. Backed by a compact 0–51 index (rank-major: index = (rank-2)*4 + suit)
    /// so cards are cheap to store in bitsets/lookup tables for the hand evaluator and equity code.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        private const string RankChars = "23456789TJQKA";
        private const string SuitChars = "cdhs";

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public Rank Rank { get; }
        public Suit Suit { get; }

        /// <summary>0–51 index, rank-major (2c=0, 2d=1, … As=51). Stable and deterministic.</summary>
        public int Index => (((int)Rank - 2) * 4) + (int)Suit;

        public static Card FromIndex(int index)
        {
            if (index < 0 || index > 51) throw new ArgumentOutOfRangeException(nameof(index));
            return new Card((Rank)((index / 4) + 2), (Suit)(index % 4));
        }

        /// <summary>Two-char form, e.g. "As", "Td", "2c".</summary>
        public override string ToString() => $"{RankChars[(int)Rank - 2]}{SuitChars[(int)Suit]}";

        public static Card Parse(string s)
        {
            if (s is null || s.Length != 2) throw new FormatException($"Bad card '{s}'.");
            int r = RankChars.IndexOf(char.ToUpperInvariant(s[0]));
            int u = SuitChars.IndexOf(char.ToLowerInvariant(s[1]));
            if (r < 0 || u < 0) throw new FormatException($"Bad card '{s}'.");
            return new Card((Rank)(r + 2), (Suit)u);
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object? obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => Index;
        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);
    }
}
