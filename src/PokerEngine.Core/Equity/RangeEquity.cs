using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine.Core.Equity
{
    /// <summary>
    /// Range-vs-range showdown equity: the aggregate equity of one range of holdings against
    /// another, averaged over every non-conflicting matchup (card removal / blockers exclude
    /// matchups that share a card with each other or the board). Each matchup is evaluated by
    /// the seeded <see cref="EquityCalculator"/>, so results are reproducible by seed.
    /// </summary>
    public static class RangeEquity
    {
        /// <summary>
        /// Heads-up equity for <paramref name="heroRange"/> against <paramref name="villainRange"/>
        /// on the given board, averaged equally over all matchups whose four hole cards and the
        /// board are mutually distinct. Throws if no such matchup exists.
        /// </summary>
        public static EquityResult HeadsUp(
            IReadOnlyList<HoleCards> heroRange,
            IReadOnlyList<HoleCards> villainRange,
            IReadOnlyList<Card> board,
            DeterministicRandom rng,
            int samplesPerMatchup = 2000)
        {
            if (heroRange is null || heroRange.Count == 0)
                throw new ArgumentException("Hero range cannot be null or empty.", nameof(heroRange));
            if (villainRange is null || villainRange.Count == 0)
                throw new ArgumentException("Villain range cannot be null or empty.", nameof(villainRange));
            if (board is null) throw new ArgumentNullException(nameof(board));
            if (board.Count > 5) throw new ArgumentOutOfRangeException(nameof(board), "Board has 0–5 cards.");

            int valid = 0;
            double totalWin = 0, totalTie = 0, totalLoss = 0;

            foreach (var hero in heroRange)
            {
                foreach (var villain in villainRange)
                {
                    // Card removal: skip the matchup if any of the four hole cards / board cards collide.
                    var all = new List<Card> { hero.High, hero.Low, villain.High, villain.Low };
                    all.AddRange(board);
                    if (all.Distinct().Count() != all.Count) continue;

                    var r = EquityCalculator.HeadsUp(
                        new[] { hero.High, hero.Low },
                        new[] { villain.High, villain.Low },
                        board, rng, samplesPerMatchup);

                    totalWin += r.Win;
                    totalTie += r.Tie;
                    totalLoss += r.Loss;
                    valid++;
                }
            }

            if (valid == 0)
                throw new InvalidOperationException(
                    "There are no non-conflicting matchups for the given ranges and board.");

            return new EquityResult(totalWin / valid, totalTie / valid, totalLoss / valid, valid) { IsExact = false };
        }

        /// <summary>
        /// Heads-up equity between two ranges expressed as <see cref="StartingHand"/> buckets;
        /// each bucket is expanded to its concrete combos (<see cref="RangeBuilder"/>) before
        /// the matchup average.
        /// </summary>
        public static EquityResult HeadsUp(
            IReadOnlyList<StartingHand> heroRange,
            IReadOnlyList<StartingHand> villainRange,
            IReadOnlyList<Card> board,
            DeterministicRandom rng,
            int samplesPerMatchup = 2000)
        {
            if (heroRange is null || heroRange.Count == 0)
                throw new ArgumentException("Hero range cannot be null or empty.", nameof(heroRange));
            if (villainRange is null || villainRange.Count == 0)
                throw new ArgumentException("Villain range cannot be null or empty.", nameof(villainRange));

            var hero = heroRange.SelectMany(RangeBuilder.Expand).ToList();
            var villain = villainRange.SelectMany(RangeBuilder.Expand).ToList();
            return HeadsUp(hero, villain, board, rng, samplesPerMatchup);
        }
    }
}
