using System;

namespace PokerEngine.Core
{
    /// <summary>
    /// The canonical "169" abstraction of a starting hand: a pair (e.g. "TT"), a suited hand
    /// ("AKs"), or an offsuit hand ("AKo"). This is the standard preflop bucket — suits beyond
    /// "same or different" don't matter before the flop — and the key under which preflop
    /// ranges, charts, and opponent stats are stored. Deterministic and exact.
    /// </summary>
    public readonly struct StartingHand : IEquatable<StartingHand>
    {
        private const string RankChars = "23456789TJQKA";

        /// <summary>Higher rank of the two (or the pair rank).</summary>
        public Rank High { get; }

        /// <summary>Lower rank of the two (equals <see cref="High"/> for a pair).</summary>
        public Rank Low { get; }

        /// <summary>True for suited non-pairs. Pairs are never "suited".</summary>
        public bool Suited { get; }

        private StartingHand(Rank high, Rank low, bool suited)
        {
            High = high;
            Low = low;
            Suited = suited;
        }

        public bool IsPair => High == Low;

        /// <summary>Number of specific card combinations this bucket covers: 6 pair, 4 suited, 12 offsuit.</summary>
        public int Combos => IsPair ? 6 : (Suited ? 4 : 12);

        public static StartingHand From(HoleCards hole) =>
            new StartingHand(hole.High.Rank, hole.Low.Rank, !hole.IsPair && hole.IsSuited);

        public static StartingHand From(Card a, Card b) => From(new HoleCards(a, b));

        /// <summary>Parse "AA", "AKs", "T9o" (case-insensitive ranks; pairs ignore any suffix).</summary>
        public static StartingHand Parse(string s)
        {
            if (s is null || s.Length < 2 || s.Length > 3) throw new FormatException($"Bad starting hand '{s}'.");
            int hi = RankChars.IndexOf(char.ToUpperInvariant(s[0]));
            int lo = RankChars.IndexOf(char.ToUpperInvariant(s[1]));
            if (hi < 0 || lo < 0) throw new FormatException($"Bad starting hand '{s}'.");
            Rank high = (Rank)(hi + 2), low = (Rank)(lo + 2);
            if (high < low) (high, low) = (low, high);

            if (high == low)
            {
                if (s.Length == 3) throw new FormatException($"A pair takes no suited/offsuit suffix: '{s}'.");
                return new StartingHand(high, low, false);
            }
            if (s.Length != 3) throw new FormatException($"Non-pair needs 's' or 'o' suffix: '{s}'.");
            bool suited = char.ToLowerInvariant(s[2]) switch
            {
                's' => true,
                'o' => false,
                _ => throw new FormatException($"Suffix must be 's' or 'o': '{s}'."),
            };
            return new StartingHand(high, low, suited);
        }

        public bool Equals(StartingHand other) => High == other.High && Low == other.Low && Suited == other.Suited;
        public override bool Equals(object? obj) => obj is StartingHand h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(High, Low, Suited);

        public override string ToString()
        {
            char h = RankChars[(int)High - 2];
            char l = RankChars[(int)Low - 2];
            if (IsPair) return $"{h}{l}";
            return $"{h}{l}{(Suited ? 's' : 'o')}";
        }
    }
}
