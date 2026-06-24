using System;

namespace PokerEngine.Core.Eval
{
    /// <summary>
    /// A fully-ordered strength of a five-card poker hand. Packs the <see cref="HandRank"/>
    /// category and up to five rank tiebreakers (most significant first) into one integer so
    /// two hands compare with a single integer comparison. Equal values tie (chop), exactly
    /// matching poker rules — suits never break ties in Hold'em.
    /// </summary>
    public readonly struct HandValue : IComparable<HandValue>, IEquatable<HandValue>
    {
        // Layout: [ category | t1 | t2 | t3 | t4 | t5 ], each tiebreaker 4 bits (rank 2..14).
        private const int TiebreakBits = 4;
        private const int TiebreakMask = 0xF;
        private readonly int _packed;

        private HandValue(int packed) => _packed = packed;

        /// <summary>The hand category (pair, flush, …).</summary>
        public HandRank Rank => (HandRank)(_packed >> (5 * TiebreakBits));

        /// <summary>Raw packed value; only meaningful for comparison/equality.</summary>
        public int Packed => _packed;

        /// <summary>
        /// Build a packed value from a category and its tiebreakers, most significant first.
        /// Each tiebreaker is a card rank (2..14); pass 0 for unused slots. At most five.
        /// </summary>
        public static HandValue Create(HandRank rank, params int[] tiebreakers)
        {
            if (tiebreakers.Length > 5)
                throw new ArgumentException("A five-card hand has at most five tiebreakers.", nameof(tiebreakers));
            int packed = (int)rank;
            for (int i = 0; i < 5; i++)
            {
                int t = i < tiebreakers.Length ? tiebreakers[i] : 0;
                if (t < 0 || t > TiebreakMask + 1) // ranks run 2..14; Ace=14 fits in 4 bits
                    throw new ArgumentOutOfRangeException(nameof(tiebreakers), $"Tiebreaker {t} out of range.");
                packed = (packed << TiebreakBits) | (t & TiebreakMask);
            }
            return new HandValue(packed);
        }

        public int CompareTo(HandValue other) => _packed.CompareTo(other._packed);
        public bool Equals(HandValue other) => _packed == other._packed;
        public override bool Equals(object? obj) => obj is HandValue v && Equals(v);
        public override int GetHashCode() => _packed;

        public static bool operator >(HandValue a, HandValue b) => a._packed > b._packed;
        public static bool operator <(HandValue a, HandValue b) => a._packed < b._packed;
        public static bool operator >=(HandValue a, HandValue b) => a._packed >= b._packed;
        public static bool operator <=(HandValue a, HandValue b) => a._packed <= b._packed;
        public static bool operator ==(HandValue a, HandValue b) => a._packed == b._packed;
        public static bool operator !=(HandValue a, HandValue b) => a._packed != b._packed;

        public override string ToString() => Rank.ToString();
    }
}
