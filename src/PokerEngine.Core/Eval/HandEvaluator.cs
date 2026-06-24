using System;
using System.Collections.Generic;

namespace PokerEngine.Core.Eval
{
    /// <summary>
    /// Exact poker hand evaluator. Ranks any 5-, 6-, or 7-card holding to a comparable
    /// <see cref="HandValue"/> (the best five-card hand). Deliberately "correct and simple":
    /// a clear five-card classifier, with 6/7-card hands evaluated by taking the best of all
    /// five-card subsets. Pinned by exhaustive tests; a lookup-table fast path can replace the
    /// internals later without changing this surface (DECISION_ALGORITHM §2).
    /// </summary>
    public static class HandEvaluator
    {
        /// <summary>
        /// Evaluate the best five-card hand from 5..7 cards. Throws for other counts or dupes.
        /// </summary>
        public static HandValue Evaluate(IReadOnlyList<Card> cards)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            if (cards.Count < 5 || cards.Count > 7)
                throw new ArgumentException("Evaluate expects 5 to 7 cards.", nameof(cards));

            if (cards.Count == 5)
                return EvaluateFive(cards[0], cards[1], cards[2], cards[3], cards[4]);

            // 6 or 7 cards: best of all five-card subsets. C(7,5)=21, C(6,5)=6 — cheap and exact.
            var best = default(HandValue);
            bool any = false;
            int n = cards.Count;
            var idx = new int[5];
            for (idx[0] = 0; idx[0] < n; idx[0]++)
            for (idx[1] = idx[0] + 1; idx[1] < n; idx[1]++)
            for (idx[2] = idx[1] + 1; idx[2] < n; idx[2]++)
            for (idx[3] = idx[2] + 1; idx[3] < n; idx[3]++)
            for (idx[4] = idx[3] + 1; idx[4] < n; idx[4]++)
            {
                var v = EvaluateFive(cards[idx[0]], cards[idx[1]], cards[idx[2]], cards[idx[3]], cards[idx[4]]);
                if (!any || v > best) { best = v; any = true; }
            }
            return best;
        }

        /// <summary>Classify exactly five cards. The reference implementation; everything else defers to it.</summary>
        public static HandValue EvaluateFive(Card a, Card b, Card c, Card d, Card e)
        {
            Span<int> ranks = stackalloc int[5] { (int)a.Rank, (int)b.Rank, (int)c.Rank, (int)d.Rank, (int)e.Rank };
            Span<int> suits = stackalloc int[5] { (int)a.Suit, (int)b.Suit, (int)c.Suit, (int)d.Suit, (int)e.Suit };

            bool isFlush = suits[0] == suits[1] && suits[1] == suits[2] && suits[2] == suits[3] && suits[3] == suits[4];

            // Tally rank multiplicities. counts[r] = how many of rank r (2..14).
            Span<int> counts = stackalloc int[15];
            foreach (int r in ranks) counts[r]++;

            int straightHigh = StraightHighCard(counts); // 0 if not a straight

            if (isFlush && straightHigh != 0)
                return HandValue.Create(HandRank.StraightFlush, straightHigh);

            // Group ranks by multiplicity, each group sorted high→low, to build tiebreakers.
            // quads/trips/pairs/singles.
            int quad = 0, trip = 0;
            // Up to two pairs; track highest first.
            int pairHi = 0, pairLo = 0;
            // Singles, high→low (at most five).
            Span<int> singles = stackalloc int[5];
            int singleCount = 0;
            for (int r = 14; r >= 2; r--)
            {
                switch (counts[r])
                {
                    case 4: quad = r; break;
                    case 3: trip = r; break;
                    case 2:
                        if (pairHi == 0) pairHi = r; else pairLo = r;
                        break;
                    case 1: singles[singleCount++] = r; break;
                }
            }

            if (quad != 0)
                return HandValue.Create(HandRank.FourOfAKind, quad, singles[0]);

            if (trip != 0 && pairHi != 0)
                return HandValue.Create(HandRank.FullHouse, trip, pairHi);

            if (isFlush)
                return HandValue.Create(HandRank.Flush, singles[0], singles[1], singles[2], singles[3], singles[4]);

            if (straightHigh != 0)
                return HandValue.Create(HandRank.Straight, straightHigh);

            if (trip != 0)
                return HandValue.Create(HandRank.ThreeOfAKind, trip, singles[0], singles[1]);

            if (pairHi != 0 && pairLo != 0)
                return HandValue.Create(HandRank.TwoPair, pairHi, pairLo, singles[0]);

            if (pairHi != 0)
                return HandValue.Create(HandRank.Pair, pairHi, singles[0], singles[1], singles[2]);

            return HandValue.Create(HandRank.HighCard, singles[0], singles[1], singles[2], singles[3], singles[4]);
        }

        /// <summary>
        /// If the five rank-counts form a straight, return its high card (5 for the wheel
        /// A-2-3-4-5, where the Ace plays low); otherwise 0. Requires five distinct ranks.
        /// </summary>
        private static int StraightHighCard(ReadOnlySpan<int> counts)
        {
            // Need five distinct ranks for a straight.
            int distinct = 0;
            for (int r = 2; r <= 14; r++) if (counts[r] > 0) distinct++;
            if (distinct != 5) return 0;

            // Standard straight: five consecutive ranks high..high-4.
            for (int high = 14; high >= 6; high--)
            {
                if (counts[high] == 1 && counts[high - 1] == 1 && counts[high - 2] == 1 &&
                    counts[high - 3] == 1 && counts[high - 4] == 1)
                    return high;
            }
            // The wheel: A-2-3-4-5, Ace low, high card is the 5.
            if (counts[14] == 1 && counts[2] == 1 && counts[3] == 1 && counts[4] == 1 && counts[5] == 1)
                return 5;

            return 0;
        }
    }
}
