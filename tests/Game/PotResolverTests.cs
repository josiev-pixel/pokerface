using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Eval;
using PokerEngine.Core.Game;
using Xunit;

namespace PokerEngine.Tests
{
    /// <summary>Side-pot math: single pots, layered side pots, ties, odd chips, and chip conservation.</summary>
    public sealed class PotResolverTests
    {
        // Build a HandValue purely for ordering in these tests: a higher seed => a stronger hand.
        private static HandValue Strength(int tier)
            => HandValue.Create((HandRank)0, tier); // same category, tiebreaker controls order

        private static HandValue? Folded => null;

        [Fact]
        public void SinglePot_BestHandTakesItAll()
        {
            var contributions = new[] { 100, 100, 100 };
            var folded = new[] { false, false, false };
            var hands = new HandValue?[] { Strength(5), Strength(9), Strength(3) };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 0);

            Assert.Equal(new[] { 0, 300, 0 }, win);
        }

        [Fact]
        public void SinglePot_SplitBetweenTiedWinners()
        {
            var contributions = new[] { 100, 100 };
            var folded = new[] { false, false };
            var hands = new HandValue?[] { Strength(7), Strength(7) };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 0);

            Assert.Equal(new[] { 100, 100 }, win);
        }

        [Fact]
        public void FoldedSeatNeverWins_AndPotGoesToOnlyLivePlayer()
        {
            var contributions = new[] { 50, 50, 50 };
            var folded = new[] { false, true, true };
            var hands = new HandValue?[] { null, null, null }; // seat 0 wins uncontested

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 2);

            Assert.Equal(new[] { 150, 0, 0 }, win);
        }

        [Fact]
        public void SidePot_ShortAllInContestsOnlyMainPot()
        {
            // Seat 0 is all-in for 100; seats 1 and 2 each put in 300.
            // Main pot = 300 (100 from each of the three) eligible to all.
            // Side pot = 400 (200 from each of seats 1,2) eligible to seats 1,2 only.
            var contributions = new[] { 100, 300, 300 };
            var folded = new[] { false, false, false };

            // Seat 0 has the best hand overall but can only win the main pot.
            var hands = new HandValue?[] { Strength(9), Strength(7), Strength(5) };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 0, out var pots);

            // Main pot to seat 0; side pot to seat 1 (best of the two big stacks).
            Assert.Equal(new[] { 300, 400, 0 }, win);

            Assert.Equal(2, pots.Count);
            Assert.Equal(300, pots[0].Amount);
            Assert.Equal(new[] { 0, 1, 2 }, pots[0].EligibleSeats.ToArray());
            Assert.Equal(400, pots[1].Amount);
            Assert.Equal(new[] { 1, 2 }, pots[1].EligibleSeats.ToArray());
        }

        [Fact]
        public void ThreeWayAllInAtDifferentAmounts_LayersCorrectly()
        {
            // Stacks all-in for 50, 120, 200.
            // Layer 1: 50*3 = 150, eligible {0,1,2}
            // Layer 2: 70*2 = 140, eligible {1,2}
            // Layer 3: 80*1 = 80,  eligible {2} (uncalled excess returns to seat 2)
            var contributions = new[] { 50, 120, 200 };
            var folded = new[] { false, false, false };
            var hands = new HandValue?[] { Strength(9), Strength(8), Strength(7) };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 0, out var pots);

            Assert.Equal(3, pots.Count);
            Assert.Equal(150, pots[0].Amount);
            Assert.Equal(140, pots[1].Amount);
            Assert.Equal(80, pots[2].Amount);

            // Seat 0 best: wins layer 1 only. Seat 1 best of {1,2}: wins layer 2. Seat 2 alone: layer 3.
            Assert.Equal(new[] { 150, 140, 80 }, win);
            Assert.Equal(contributions.Sum(), win.Sum());
        }

        [Fact]
        public void OddChip_GoesToFirstSeatLeftOfButton()
        {
            // A single odd pot of 3 with two tied winners (seat 2 contributes but loses) splits
            // 1 + 1, and the odd chip goes to the first seat left of the button. Button = seat 0 =>
            // seat 1 (button+1) gets the extra chip.
            var contributions = new[] { 1, 1, 1 };
            var folded = new[] { false, false, false };
            var hands = new HandValue?[] { Strength(6), Strength(6), Strength(3) };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 0);

            Assert.Equal(3, win.Sum());
            Assert.Equal(new[] { 1, 2, 0 }, win); // seat 1 (left of button) gets the odd chip
        }

        [Fact]
        public void OddChip_WrapsClockwiseFromButton()
        {
            // Three-way tie for an odd pot of 5 (each contributes 1, plus a folded seat adds 2).
            // 5 / 3 = 1 each, remainder 2. Button = seat 1 => preference order 2, 3, 0, 1, so the
            // two extra chips go to seats 2 then 3.
            var contributions = new[] { 1, 1, 1, 2 };
            var folded = new[] { false, false, false, true };
            var hands = new HandValue?[] { Strength(4), Strength(4), Strength(4), null };

            int[] win = PotResolver.Resolve(contributions, folded, hands, buttonSeat: 1);

            Assert.Equal(5, win.Sum());
            // Layers: level 1 over all four = 4 chips eligible {0,1,2} (seat 3 folded); level 1 more
            // from seat 3 alone = 1 chip with no eligible winner -> refunded to seat 3.
            // So the 4-chip pot splits 1/1/1 + one odd chip to seat 2 (button+1); seat 3 gets its
            // uncontested extra chip back.
            Assert.Equal(new[] { 1, 1, 2, 1 }, win);
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(42u)]
        [InlineData(9999u)]
        public void ChipConservation_OverRandomContributions(ulong seed)
        {
            var rng = new DeterministicRandom(seed);
            for (int trial = 0; trial < 200; trial++)
            {
                int seats = rng.NextInt(2, 10);
                var contributions = new int[seats];
                var folded = new bool[seats];
                var hands = new HandValue?[seats];
                long total = 0;

                for (int i = 0; i < seats; i++)
                {
                    contributions[i] = rng.NextInt(0, 500);
                    total += contributions[i];
                    folded[i] = rng.NextInt(0, 4) == 0; // ~25% fold
                    hands[i] = folded[i] ? (HandValue?)null : Strength(rng.NextInt(0, 10));
                }

                // Guarantee at least one live contributor so there is always someone to win.
                folded[0] = false;
                if (contributions[0] == 0) contributions[0] = 1;
                hands[0] = Strength(rng.NextInt(0, 10));
                total = contributions.Sum();

                int button = rng.NextInt(0, seats);
                int[] win = PotResolver.Resolve(contributions, folded, hands, button);

                Assert.Equal(total, win.Sum());
                Assert.All(win, w => Assert.True(w >= 0));
            }
        }
    }
}
