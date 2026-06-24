using System;
using System.Collections.Generic;
using PokerEngine.Core;
using PokerEngine.Core.Equity;

namespace PokerEngine.Tests
{
    /// <summary>
    /// Sanity checks for preflop push/fold bucket ordering, equity inputs, and
    /// blocker-aware deal weights.
    /// </summary>
    public sealed class PushFoldMatricesTests
    {
        private static readonly IReadOnlyList<Card> NoBoard = Array.Empty<Card>();

        [Fact]
        public void Buckets169_HasExpectedCountAndDistinctEntries()
        {
            IReadOnlyList<StartingHand> buckets = PushFoldMatrices.Buckets169();

            Assert.Equal(169, buckets.Count);
            Assert.Equal(169, new HashSet<StartingHand>(buckets).Count);
        }

        [Fact]
        public void MirrorAces_AreAboutEven()
        {
            IReadOnlyList<HoleCards> aces = RangeBuilder.Expand("AA");

            EquityResult result = RangeEquity.HeadsUp(aces, aces, NoBoard, new DeterministicRandom(11), 80);

            Assert.InRange(result.Equity, 0.4, 0.6);
        }

        [Fact]
        public void AcesDominateSevenTwoOffsuit()
        {
            IReadOnlyList<HoleCards> aces = RangeBuilder.Expand("AA");
            IReadOnlyList<HoleCards> sevenTwoOffsuit = RangeBuilder.Expand("72o");

            EquityResult result = RangeEquity.HeadsUp(aces, sevenTwoOffsuit, NoBoard, new DeterministicRandom(12), 80);

            Assert.True(result.Equity > 0.8, $"AA equity versus 72o was {result.Equity}.");
        }

        [Fact]
        public void DistinctOrderedComboPairCounts_AreBlockerAware()
        {
            IReadOnlyList<HoleCards> aces = RangeBuilder.Expand("AA");
            IReadOnlyList<HoleCards> kings = RangeBuilder.Expand("KK");

            Assert.Equal(36.0, PushFoldMatrices.CountDistinctOrderedComboPairs(aces, kings));
            Assert.Equal(6.0, PushFoldMatrices.CountDistinctOrderedComboPairs(aces, aces));
        }
    }
}
