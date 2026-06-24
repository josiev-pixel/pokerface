using System;
using System.Collections.Generic;
using PokerEngine.Core.Eval;

namespace PokerEngine.Core.Equity
{
    /// <summary>
    /// Computes hand-vs-hand showdown equity for Texas Hold'em. It enumerates every remaining
    /// board runout when that set is small enough (exact), and falls back to seeded
    /// Monte-Carlo sampling when it is large — so the answer is either exact or a reproducible
    /// approximation, never wall-clock-dependent (DECISION_ALGORITHM §2, §9).
    /// </summary>
    public static class EquityCalculator
    {
        /// <summary>
        /// Above this many runout combinations we sample instead of enumerating. Chosen so the
        /// flop (C(45,2)=990) and turn (44) enumerate exactly, while the preflop (~1.7M) samples.
        /// </summary>
        public const int EnumerationLimit = 200_000;

        /// <summary>Default Monte-Carlo sample count when enumeration is too large.</summary>
        public const int DefaultSamples = 50_000;

        /// <summary>
        /// Heads-up equity for <paramref name="hero"/> against <paramref name="villain"/> on the
        /// given <paramref name="board"/> (0–5 known cards). Enumerates if the remaining runout
        /// space is ≤ <see cref="EnumerationLimit"/>, else samples <paramref name="samples"/>
        /// runouts with the seeded <paramref name="rng"/>.
        /// </summary>
        public static EquityResult HeadsUp(
            IReadOnlyList<Card> hero,
            IReadOnlyList<Card> villain,
            IReadOnlyList<Card> board,
            DeterministicRandom rng,
            int samples = DefaultSamples)
        {
            if (hero is null || hero.Count != 2) throw new ArgumentException("Hero needs exactly two hole cards.", nameof(hero));
            if (villain is null || villain.Count != 2) throw new ArgumentException("Villain needs exactly two hole cards.", nameof(villain));
            if (board is null || board.Count > 5) throw new ArgumentException("Board has 0–5 cards.", nameof(board));

            // Track used cards in a 52-bit mask; reject any overlap (duplicate cards).
            ulong used = 0;
            void Use(Card c)
            {
                ulong bit = 1UL << c.Index;
                if ((used & bit) != 0) throw new ArgumentException($"Duplicate card {c}.");
                used |= bit;
            }
            foreach (var c in hero) Use(c);
            foreach (var c in villain) Use(c);
            foreach (var c in board) Use(c);

            // The deck of cards still available to complete the board.
            var deck = new List<Card>(52);
            for (int i = 0; i < 52; i++)
                if ((used & (1UL << i)) == 0) deck.Add(Card.FromIndex(i));

            int need = 5 - board.Count;
            var fixedBoard = new List<Card>(board);

            long combos = Combinations(deck.Count, need);
            if (combos <= EnumerationLimit)
                return Enumerate(hero, villain, fixedBoard, deck, need);

            return MonteCarlo(hero, villain, fixedBoard, deck, need, rng, samples);
        }

        private static EquityResult Enumerate(
            IReadOnlyList<Card> hero, IReadOnlyList<Card> villain,
            List<Card> board, List<Card> deck, int need)
        {
            long win = 0, tie = 0, loss = 0, n = 0;
            var runout = new Card[need];

            void Score()
            {
                var h = Best(hero, board, runout);
                var v = Best(villain, board, runout);
                int cmp = h.CompareTo(v);
                if (cmp > 0) win++; else if (cmp < 0) loss++; else tie++;
                n++;
            }

            // Enumerate all need-card combinations of the remaining deck.
            ChooseAndScore(deck, need, 0, runout, 0, Score);

            double inv = n == 0 ? 0 : 1.0 / n;
            return new EquityResult(win * inv, tie * inv, loss * inv, n) { IsExact = true };
        }

        private static void ChooseAndScore(List<Card> deck, int need, int start, Card[] runout, int depth, Action score)
        {
            if (depth == need) { score(); return; }
            for (int i = start; i <= deck.Count - (need - depth); i++)
            {
                runout[depth] = deck[i];
                ChooseAndScore(deck, need, i + 1, runout, depth + 1, score);
            }
        }

        private static EquityResult MonteCarlo(
            IReadOnlyList<Card> hero, IReadOnlyList<Card> villain,
            List<Card> board, List<Card> deck, int need,
            DeterministicRandom rng, int samples)
        {
            if (samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples));
            long win = 0, tie = 0, loss = 0;
            var pool = deck.ToArray();
            var runout = new Card[need];

            for (int s = 0; s < samples; s++)
            {
                // Partial Fisher–Yates: draw `need` distinct cards from the pool.
                for (int k = 0; k < need; k++)
                {
                    int j = k + rng.NextInt(0, pool.Length - k);
                    (pool[k], pool[j]) = (pool[j], pool[k]);
                    runout[k] = pool[k];
                }
                var h = Best(hero, board, runout);
                var v = Best(villain, board, runout);
                int cmp = h.CompareTo(v);
                if (cmp > 0) win++; else if (cmp < 0) loss++; else tie++;
            }

            double inv = 1.0 / samples;
            return new EquityResult(win * inv, tie * inv, loss * inv, samples) { IsExact = false };
        }

        private static HandValue Best(IReadOnlyList<Card> hole, List<Card> board, Card[] runout)
        {
            // hole(2) + board(0..5) + runout = 7 cards.
            Span<Card> seven = stackalloc Card[7];
            int n = 0;
            seven[n++] = hole[0];
            seven[n++] = hole[1];
            foreach (var c in board) seven[n++] = c;
            foreach (var c in runout) seven[n++] = c;
            // Reuse the array-based evaluator via a small local copy.
            var cards = new Card[n];
            for (int i = 0; i < n; i++) cards[i] = seven[i];
            return HandEvaluator.Evaluate(cards);
        }

        /// <summary>C(n, k) as a long; returns 1 for k=0 and 0 for k&lt;0 or k&gt;n.</summary>
        public static long Combinations(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Math.Min(k, n - k);
            long result = 1;
            for (int i = 0; i < k; i++)
            {
                result = result * (n - i) / (i + 1);
            }
            return result;
        }
    }
}
