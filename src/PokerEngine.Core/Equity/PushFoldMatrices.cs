using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine.Core.Equity
{
    /// <summary>
    /// Builds the bucket-level equity and blocker-aware deal-weight matrices used by
    /// heads-up preflop push/fold solves.
    /// </summary>
    public static class PushFoldMatrices
    {
        private static readonly IReadOnlyList<Card> EmptyBoard = Array.Empty<Card>();

        /// <summary>
        /// Returns the canonical 169 starting-hand buckets in deterministic order:
        /// pairs first for each high rank, then suited and offsuit non-pairs while
        /// iterating both ranks from Ace down to Deuce. The returned list count is 169.
        /// </summary>
        public static IReadOnlyList<StartingHand> Buckets169()
        {
            var buckets = new List<StartingHand>(169);
            for (int high = (int)Rank.Ace; high >= (int)Rank.Two; high--)
            {
                for (int low = high; low >= (int)Rank.Two; low--)
                {
                    Rank highRank = (Rank)high;
                    Rank lowRank = (Rank)low;
                    if (high == low)
                    {
                        buckets.Add(StartingHand.Parse(RankText(highRank) + RankText(lowRank)));
                    }
                    else
                    {
                        string prefix = RankText(highRank) + RankText(lowRank);
                        buckets.Add(StartingHand.Parse(prefix + "s"));
                        buckets.Add(StartingHand.Parse(prefix + "o"));
                    }
                }
            }

            if (buckets.Count != 169)
            {
                throw new InvalidOperationException("The canonical preflop bucket set must contain 169 entries.");
            }

            return buckets;
        }

        /// <summary>
        /// Builds a full 169-by-169 push/fold data set using seeded range-vs-range
        /// equity and blocker-aware ordered combo-pair counts.
        /// </summary>
        /// <param name="rng">The deterministic random source used by Monte-Carlo equity.</param>
        /// <param name="samplesPerMatchup">Samples per concrete matchup when preflop equity is sampled.</param>
        public static PushFoldData Build(DeterministicRandom rng, int samplesPerMatchup = 200)
        {
            ArgumentNullException.ThrowIfNull(rng);
            if (samplesPerMatchup <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerMatchup), "Sample count must be positive.");
            }

            IReadOnlyList<StartingHand> buckets = Buckets169();
            var expanded = buckets.Select(RangeBuilder.Expand).ToArray();
            int count = buckets.Count;
            var equity = new double[count, count];
            var weight = new double[count, count];

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    equity[i, j] = RangeEquity.HeadsUp(expanded[i], expanded[j], EmptyBoard, rng, samplesPerMatchup).Equity;
                    weight[i, j] = CountDistinctOrderedComboPairs(expanded[i], expanded[j]);
                }
            }

            return new PushFoldData(buckets, equity, weight);
        }

        /// <summary>
        /// Counts ordered concrete combo pairs whose four hole cards are all distinct.
        /// </summary>
        /// <param name="smallBlindCombos">The small blind bucket expanded to concrete combos.</param>
        /// <param name="bigBlindCombos">The big blind bucket expanded to concrete combos.</param>
        public static double CountDistinctOrderedComboPairs(
            IReadOnlyList<HoleCards> smallBlindCombos,
            IReadOnlyList<HoleCards> bigBlindCombos)
        {
            ArgumentNullException.ThrowIfNull(smallBlindCombos);
            ArgumentNullException.ThrowIfNull(bigBlindCombos);

            double count = 0.0;
            foreach (HoleCards smallBlind in smallBlindCombos)
            {
                foreach (HoleCards bigBlind in bigBlindCombos)
                {
                    if (AreDisjoint(smallBlind, bigBlind))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool AreDisjoint(HoleCards first, HoleCards second) =>
            first.High != second.High &&
            first.High != second.Low &&
            first.Low != second.High &&
            first.Low != second.Low;

        private static string RankText(Rank rank) =>
            rank switch
            {
                Rank.Two => "2",
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "T",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unknown rank."),
            };
    }

    /// <summary>
    /// Immutable holder for push/fold bucket metadata and precomputed matrices.
    /// </summary>
    public readonly struct PushFoldData
    {
        /// <summary>Creates a push/fold matrix data set.</summary>
        /// <param name="buckets">The bucket order shared by both matrix dimensions.</param>
        /// <param name="equity">Small blind equity by [small blind bucket, big blind bucket].</param>
        /// <param name="weight">Chance weight by [small blind bucket, big blind bucket].</param>
        public PushFoldData(IReadOnlyList<StartingHand> buckets, double[,] equity, double[,] weight)
        {
            Buckets = buckets ?? throw new ArgumentNullException(nameof(buckets));
            Equity = equity ?? throw new ArgumentNullException(nameof(equity));
            Weight = weight ?? throw new ArgumentNullException(nameof(weight));
        }

        /// <summary>The bucket order shared by both matrix dimensions.</summary>
        public IReadOnlyList<StartingHand> Buckets { get; }

        /// <summary>Small blind showdown equity by [small blind bucket, big blind bucket].</summary>
        public double[,] Equity { get; }

        /// <summary>Blocker-aware ordered concrete combo-pair counts by bucket pair.</summary>
        public double[,] Weight { get; }
    }
}
