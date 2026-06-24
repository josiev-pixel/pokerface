using System;
using System.Collections.Generic;
using PokerEngine.Core.Eval;

namespace PokerEngine.Core.Game
{
    /// <summary>
    /// Pure, deterministic side-pot math. Given how much each seat contributed to the pot over the
    /// whole hand, which seats folded, and the showdown strength of the seats that did not, it
    /// layers the money into a main pot plus side pots and returns each seat's gross winnings.
    /// </summary>
    /// <remarks>
    /// Side pots arise whenever players are all-in for different amounts. We peel the pot in
    /// ascending contribution levels: each level forms a layer contested only by the seats that
    /// put in at least that much. A short all-in can win only the layers it paid into. Chips are
    /// integers and always conserved: the returned winnings sum to the contributions sum. Odd chips
    /// that cannot split evenly among tied winners are dealt one at a time clockwise from the first
    /// seat left of the button, making payouts fully reproducible.
    /// </remarks>
    public static class PotResolver
    {
        /// <summary>Resolve winnings per seat. See <see cref="Resolve(IReadOnlyList{int}, IReadOnlyList{bool}, IReadOnlyList{HandValue?}, int, out IReadOnlyList{SidePot})"/> for details.</summary>
        public static int[] Resolve(
            IReadOnlyList<int> contributions,
            IReadOnlyList<bool> folded,
            IReadOnlyList<HandValue?> hands,
            int buttonSeat)
            => Resolve(contributions, folded, hands, buttonSeat, out _);

        /// <summary>
        /// Split the pot and award it.
        /// </summary>
        /// <param name="contributions">Chips each seat put in over the whole hand (each &gt;= 0).</param>
        /// <param name="folded">True for seats that folded (ineligible to win, but their chips stay in the pot).</param>
        /// <param name="hands">Showdown hand value per seat; ignored where the seat folded.</param>
        /// <param name="buttonSeat">Button index, used only to make odd-chip distribution deterministic.</param>
        /// <param name="pots">The computed layered pot structure, for inspection by tests/UI.</param>
        /// <returns>Gross winnings per seat. The sum equals the sum of <paramref name="contributions"/>.</returns>
        public static int[] Resolve(
            IReadOnlyList<int> contributions,
            IReadOnlyList<bool> folded,
            IReadOnlyList<HandValue?> hands,
            int buttonSeat,
            out IReadOnlyList<SidePot> pots)
        {
            if (contributions is null) throw new ArgumentNullException(nameof(contributions));
            if (folded is null) throw new ArgumentNullException(nameof(folded));
            if (hands is null) throw new ArgumentNullException(nameof(hands));

            int seatCount = contributions.Count;
            if (folded.Count != seatCount || hands.Count != seatCount)
                throw new ArgumentException("contributions, folded and hands must have the same length.");
            if (seatCount == 0)
            {
                pots = Array.Empty<SidePot>();
                return Array.Empty<int>();
            }

            var remaining = new int[seatCount];
            long totalContributed = 0;
            for (int i = 0; i < seatCount; i++)
            {
                int c = contributions[i];
                if (c < 0) throw new ArgumentException("Contributions must be non-negative.", nameof(contributions));
                remaining[i] = c;
                totalContributed += c;
            }

            var layers = BuildLayers(remaining, folded, seatCount);
            pots = layers;

            var winnings = new int[seatCount];
            foreach (SidePot pot in layers)
                AwardPot(pot, hands, buttonSeat, seatCount, winnings);

            // Chip-conservation guarantee: nothing created or destroyed.
            long awarded = 0;
            foreach (int w in winnings) awarded += w;
            if (awarded != totalContributed)
                throw new InvalidOperationException(
                    $"Pot resolution lost chips: awarded {awarded}, contributed {totalContributed}.");

            return winnings;
        }

        /// <summary>Peel the contributions into ascending side-pot layers (main pot first).</summary>
        private static List<SidePot> BuildLayers(int[] remaining, IReadOnlyList<bool> folded, int seatCount)
        {
            var layers = new List<SidePot>();
            while (true)
            {
                // Lowest positive remaining contribution becomes this layer's per-seat slice.
                int level = int.MaxValue;
                for (int i = 0; i < seatCount; i++)
                    if (remaining[i] > 0 && remaining[i] < level)
                        level = remaining[i];
                if (level == int.MaxValue) break; // nothing left

                int amount = 0;
                var contributors = new List<int>();
                var eligible = new List<int>();
                for (int i = 0; i < seatCount; i++)
                {
                    if (remaining[i] <= 0) continue;
                    remaining[i] -= level;
                    amount += level;
                    contributors.Add(i);              // ascending order by construction
                    if (!folded[i]) eligible.Add(i);
                }

                layers.Add(new SidePot(amount, contributors, eligible));
            }
            return layers;
        }

        /// <summary>Award one layer to the best eligible hand(s), splitting ties with deterministic odd chips.</summary>
        private static void AwardPot(
            SidePot pot,
            IReadOnlyList<HandValue?> hands,
            int buttonSeat,
            int seatCount,
            int[] winnings)
        {
            if (pot.Amount == 0) return;

            // Degenerate "dead money": every contributor to this layer folded (no one to win it).
            // Refund it to this layer's own contributors rather than vanishing chips. In normal play
            // this layer never forms, but handling it keeps the conservation guarantee total.
            if (pot.EligibleSeats.Count == 0)
            {
                RefundDeadMoney(pot, seatCount, winnings);
                return;
            }

            // Find the best showdown value among eligible seats, then collect everyone who matches.
            HandValue best = default;
            bool haveBest = false;
            foreach (int seat in pot.EligibleSeats)
            {
                HandValue? hv = hands[seat];
                if (hv is null) continue;
                if (!haveBest || hv.Value > best) { best = hv.Value; haveBest = true; }
            }

            var winners = new List<int>();
            if (haveBest)
            {
                foreach (int seat in pot.EligibleSeats)
                    if (hands[seat] is { } hv && hv == best)
                        winners.Add(seat);
            }
            else
            {
                // Single uncontested seat (everyone else folded) — no hand value needed.
                foreach (int seat in pot.EligibleSeats) winners.Add(seat);
            }

            DistributeAmong(winners, pot.Amount, buttonSeat, seatCount, winnings);
        }

        /// <summary>Split <paramref name="amount"/> among winners; odd chips go clockwise from left of button.</summary>
        private static void DistributeAmong(
            List<int> winners, int amount, int buttonSeat, int seatCount, int[] winnings)
        {
            int n = winners.Count;
            int share = amount / n;
            int remainder = amount - (share * n);

            foreach (int seat in winners) winnings[seat] += share;

            // Deal leftover chips one at a time to tied winners in clockwise order, starting at the
            // first seat to the left of the button: (button+1), (button+2), … This is the standard
            // house rule and makes the split deterministic.
            for (int step = 1; step <= seatCount && remainder > 0; step++)
            {
                int seat = ((buttonSeat + step) % seatCount + seatCount) % seatCount;
                if (winners.Contains(seat))
                {
                    winnings[seat] += 1;
                    remainder--;
                }
            }
        }

        /// <summary>Refund an all-folded layer to exactly its own contributors, conserving chips.</summary>
        private static void RefundDeadMoney(SidePot pot, int seatCount, int[] winnings)
        {
            var contributors = new List<int>(pot.ContributorSeats);
            if (contributors.Count == 0) return; // unreachable when amount > 0, but be safe
            // It is their own money returned, so any odd-chip order is fine; keep it deterministic
            // by seat order from seat 0.
            DistributeAmong(contributors, pot.Amount, buttonSeat: seatCount - 1, seatCount, winnings);
        }
    }
}
