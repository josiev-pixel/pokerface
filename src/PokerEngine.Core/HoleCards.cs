using System;

namespace PokerEngine.Core
{
    /// <summary>
    /// A player's two private cards. Stored with the higher rank first (ties broken by suit
    /// order) so equal holdings compare equal regardless of input order. Exact card data —
    /// no abstraction here (that lives in <see cref="StartingHand"/> and the Abstraction layer).
    /// </summary>
    public readonly struct HoleCards : IEquatable<HoleCards>
    {
        public HoleCards(Card a, Card b)
        {
            if (a == b) throw new ArgumentException($"Hole cards must differ ({a}).");
            // Higher card first: by rank, then by suit index as a stable tiebreak.
            if (a.Rank > b.Rank || (a.Rank == b.Rank && (int)a.Suit > (int)b.Suit))
            {
                High = a; Low = b;
            }
            else
            {
                High = b; Low = a;
            }
        }

        public Card High { get; }
        public Card Low { get; }

        public bool IsPair => High.Rank == Low.Rank;
        public bool IsSuited => High.Suit == Low.Suit;

        /// <summary>The 169-bucket canonical form (e.g. "AA", "AKs", "T9o").</summary>
        public StartingHand ToStartingHand() => StartingHand.From(this);

        /// <summary>Parse a four-char string like "AsKh".</summary>
        public static HoleCards Parse(string s)
        {
            if (s is null || s.Length != 4) throw new FormatException($"Hole cards need four chars, got '{s}'.");
            return new HoleCards(Card.Parse(s.Substring(0, 2)), Card.Parse(s.Substring(2, 2)));
        }

        public bool Equals(HoleCards other) => High == other.High && Low == other.Low;
        public override bool Equals(object? obj) => obj is HoleCards h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(High.Index, Low.Index);
        public override string ToString() => $"{High}{Low}";
    }
}
