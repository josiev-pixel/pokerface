using System;
using PokerEngine.Core;

namespace PokerEngine.Decision
{
    /// <summary>
    /// The SAGE ("Sit-And-Go Endgame") heads-up push/fold system of Kittock &amp; Jones
    /// (Card Player, 2006): a simple, near-Nash rule for short stacks (≈10 BB or fewer) where
    /// the game collapses to "the small blind jams all-in or folds; the big blind calls or folds".
    /// It scores a hand by a Power Index and compares it to a stack-dependent threshold. This is
    /// a deterministic, unexploitable-by-design approximation of the Nash push/fold equilibrium
    /// (full Nash tables are a later refinement — DECISION_ALGORITHM §preflop).
    ///
    /// Note the SAGE card values are NOT the normal ranks: Ace counts as 15.
    /// </summary>
    public static class SageSystem
    {
        /// <summary>The largest effective stack (in big blinds) for which SAGE is intended.</summary>
        public const int MaxEffectiveBigBlinds = 10;

        /// <summary>
        /// SAGE Power Index: 2·(high card value) + (low card value), +22 for a pair, +2 if suited.
        /// Card values: 2–10 = face, J=11, Q=12, K=13, A=15. Range 8 (32o) … 67 (AA).
        /// </summary>
        public static int PowerIndex(StartingHand hand)
        {
            int hi = CardValue(hand.High);
            int lo = CardValue(hand.Low);
            int pi = 2 * hi + lo;
            if (hand.IsPair) pi += 22;
            if (hand.Suited) pi += 2;
            return pi;
        }

        public static int PowerIndex(HoleCards hole) => PowerIndex(hole.ToStartingHand());

        /// <summary>True if the small blind should shove this hand at the given effective stack.</summary>
        public static bool ShouldPush(StartingHand hand, double effectiveBigBlinds) =>
            PowerIndex(hand) >= PushThreshold(effectiveBigBlinds);

        /// <summary>True if the big blind should call a shove with this hand at the given effective stack.</summary>
        public static bool ShouldCall(StartingHand hand, double effectiveBigBlinds) =>
            PowerIndex(hand) >= CallThreshold(effectiveBigBlinds);

        /// <summary>Minimum Power Index to shove, by effective stack in BB (table rows 1..7+).</summary>
        public static int PushThreshold(double effectiveBigBlinds) => Row(effectiveBigBlinds) switch
        {
            1 => 17,
            2 => 21,
            3 => 22,
            4 => 23,
            5 => 24,
            6 => 25,
            _ => 26, // 7 BB and up (within the push/fold regime)
        };

        /// <summary>Minimum Power Index to call a shove, by effective stack in BB (table rows 1..7+).</summary>
        public static int CallThreshold(double effectiveBigBlinds) => Row(effectiveBigBlinds) switch
        {
            1 => 0, // call with any two
            2 => 17,
            3 => 24,
            4 => 26,
            5 => 28,
            6 => 29,
            _ => 30, // 7 BB and up
        };

        // Map an effective stack to a table row 1..7 (the SAGE table is indexed by whole BB).
        private static int Row(double effectiveBigBlinds)
        {
            if (effectiveBigBlinds < 1) throw new ArgumentOutOfRangeException(nameof(effectiveBigBlinds));
            int r = (int)Math.Round(effectiveBigBlinds, MidpointRounding.AwayFromZero);
            return Math.Clamp(r, 1, 7);
        }

        private static int CardValue(Rank rank) => rank switch
        {
            Rank.Ace => 15,
            Rank.King => 13,
            Rank.Queen => 12,
            Rank.Jack => 11,
            _ => (int)rank, // 2..10
        };
    }
}
