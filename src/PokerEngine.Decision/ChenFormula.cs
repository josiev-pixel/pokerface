using System;
using PokerEngine.Core;

namespace PokerEngine.Decision
{
    /// <summary>
    /// Bill Chen's preflop starting-hand strength score — a widely-used, deterministic heuristic
    /// for ranking the 169 hands. Higher is stronger (AA = 20, 72o = −1). Used as the baseline
    /// preflop strength signal when stacks are deep enough that we're not in push/fold mode
    /// (see <see cref="SageSystem"/> for the short-stack regime). Reference values cross-checked
    /// in docs/POKER_THEORY §preflop.
    /// </summary>
    public static class ChenFormula
    {
        /// <summary>The Chen score for a canonical starting hand (rounded; halves round up).</summary>
        public static int Score(StartingHand hand)
        {
            double pts = HighCardPoints(hand.High);

            if (hand.IsPair)
            {
                pts = Math.Max(pts * 2.0, 5.0); // pair = 2× high-card points, floored at 5
                return Round(pts);
            }

            if (hand.Suited) pts += 2.0;

            int gap = (int)hand.High - (int)hand.Low - 1; // ranks strictly between (Ace high)
            pts -= GapPenalty(gap);

            // Straight bonus: connected/one-gappers that can't make the nut straight too easily —
            // both cards below a Queen (high card is Jack or lower).
            if (gap <= 1 && (int)hand.High < (int)Rank.Queen) pts += 1.0;

            return Round(pts);
        }

        public static int Score(HoleCards hole) => Score(hole.ToStartingHand());

        private static double HighCardPoints(Rank rank) => rank switch
        {
            Rank.Ace => 10.0,
            Rank.King => 8.0,
            Rank.Queen => 7.0,
            Rank.Jack => 6.0,
            _ => (int)rank / 2.0, // Ten→5, Nine→4.5, … Two→1
        };

        private static int GapPenalty(int gap) => gap switch
        {
            <= 0 => 0,
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 5, // gap of 4 or more
        };

        // "Round half-points up" — 4.5 → 5, −1.5 → −1 (toward positive infinity).
        private static int Round(double pts) => (int)Math.Round(pts, MidpointRounding.ToPositiveInfinity);
    }
}
