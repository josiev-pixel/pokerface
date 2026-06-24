using System;
using System.Collections.Generic;
using PokerEngine.Solver;

namespace PokerEngine.Tests
{
    /// <summary>
    /// Validates the CFR+ solver on Kuhn poker against its known closed-form equilibrium:
    /// game value −1/18, player 0's α-family, player 1's unique strategy, determinism, and
    /// low exploitability of the recovered average strategy.
    /// </summary>
    public sealed class CfrPlusKuhnTests
    {
        private const int Iterations = 100_000;

        // Action indices follow KuhnPoker's stable order: {pass, bet} and {fold, call}.
        private const int Pass = 0;
        private const int Bet = 1;
        private const int Fold = 0;
        private const int Call = 1;

        private static IReadOnlyDictionary<string, double[]> Solve(int iterations)
        {
            var game = new KuhnPoker();
            var solver = new CfrPlusSolver<KuhnState>(game);
            solver.Run(iterations);
            return solver.AverageStrategy();
        }

        [Fact]
        public void GameValue_ConvergesToMinusOneEighteenth()
        {
            var strategy = Solve(Iterations);
            double value0 = GameValuePlayer0(strategy);

            Assert.Equal(KuhnEquilibrium.ExpectedValuePlayer1(), value0, precision: 2); // |.| < 0.005-ish
            Assert.True(Math.Abs(value0 - (-1.0 / 18.0)) < 0.005,
                $"Player 0 game value {value0} not within 0.005 of -1/18.");
        }

        [Fact]
        public void Player0Strategy_IsMemberOfAlphaFamily()
        {
            var strategy = Solve(Iterations);

            double jackBet = strategy["J"][Bet];
            double queenCall = strategy["Qpb"][Call];
            double kingBet = strategy["K"][Bet];

            double alpha = KuhnEquilibrium.RecoverAlphaFromKingBet(kingBet);

            Assert.InRange(alpha, KuhnEquilibrium.AlphaMin - 0.02, KuhnEquilibrium.AlphaMax + 0.02);
            Assert.True(Math.Abs(jackBet - alpha) < 0.02,
                $"P(Jack bets)={jackBet} not ≈ α={alpha}.");
            Assert.True(Math.Abs(queenCall - (alpha + 1.0 / 3.0)) < 0.02,
                $"P(Queen calls)={queenCall} not ≈ α+1/3={alpha + 1.0 / 3.0}.");
            Assert.True(KuhnEquilibrium.IsPlayer0InAlphaFamily(jackBet, queenCall, kingBet, 0.02));

            // Player 0 always folds a Jack after checking and facing a bet, and always calls a King bet.
            Assert.True(strategy["Jpb"][Fold] > 0.98, "Jack should fold after check-bet.");
            Assert.True(strategy["Kpb"][Call] > 0.98, "King should call after check-bet.");
        }

        [Fact]
        public void Player1Strategy_MatchesUniqueSolution()
        {
            var strategy = Solve(Iterations);

            // Jack: bet 1/3 facing a check; fold facing a bet.
            Assert.True(Math.Abs(strategy["Jp"][Bet] - 1.0 / 3.0) < 0.02,
                $"P(P1 bets Jack after check)={strategy["Jp"][Bet]} not ≈ 1/3.");
            Assert.True(strategy["Jb"][Fold] > 0.98, "P1 Jack should fold facing a bet.");

            // Queen: check facing a check; call 1/3 facing a bet.
            Assert.True(strategy["Qp"][Pass] > 0.98, "P1 Queen should check facing a check.");
            Assert.True(Math.Abs(strategy["Qb"][Call] - 1.0 / 3.0) < 0.02,
                $"P(P1 calls Queen)={strategy["Qb"][Call]} not ≈ 1/3.");

            // King: bet facing a check; call facing a bet.
            Assert.True(strategy["Kp"][Bet] > 0.98, "P1 King should bet facing a check.");
            Assert.True(strategy["Kb"][Call] > 0.98, "P1 King should call facing a bet.");
        }

        [Fact]
        public void Solver_IsDeterministic()
        {
            var a = Solve(20_000);
            var b = Solve(20_000);

            Assert.Equal(a.Count, b.Count);
            foreach (var pair in a)
            {
                Assert.True(b.ContainsKey(pair.Key), $"Missing info set {pair.Key} on second run.");
                double[] first = pair.Value;
                double[] second = b[pair.Key];
                Assert.Equal(first.Length, second.Length);
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.Equal(first[i], second[i]); // exact: no RNG, identical traversal order.
                }
            }
        }

        [Fact]
        public void AverageStrategy_HasLowExploitability()
        {
            var strategy = Solve(Iterations);
            double exploitability = Exploitability(strategy);

            Assert.True(exploitability < 0.01,
                $"Exploitability {exploitability} chips/hand should be below 0.01.");
        }

        // ----- Evaluation helpers (independent of the solver's own traversal) -----

        /// <summary>Expected value to player 0 when both players play <paramref name="strategy"/>.</summary>
        private static double GameValuePlayer0(IReadOnlyDictionary<string, double[]> strategy)
        {
            var game = new KuhnPoker();
            double total = 0.0;
            foreach (var outcome in game.ChanceOutcomes(game.Root))
            {
                total += outcome.Probability * ExpectedValue(game, outcome.State, strategy);
            }

            return total;
        }

        /// <summary>EV to player 0 of a dealt subtree under a fixed strategy profile.</summary>
        private static double ExpectedValue(KuhnPoker game, KuhnState state, IReadOnlyDictionary<string, double[]> strategy)
        {
            if (game.IsTerminal(state))
            {
                return game.Payoff(state, 0);
            }

            var actions = game.LegalActions(state);
            double[] probs = strategy[game.InfoSetKey(state)];
            double value = 0.0;
            for (int a = 0; a < actions.Count; a++)
            {
                if (probs[a] == 0.0)
                {
                    continue;
                }

                value += probs[a] * ExpectedValue(game, game.Apply(state, actions[a]), strategy);
            }

            return value;
        }

        /// <summary>
        /// Exploitability in chips/hand: the sum over both players of how much a best response
        /// gains over the strategy's own game value. Zero at exact equilibrium.
        /// </summary>
        private static double Exploitability(IReadOnlyDictionary<string, double[]> strategy)
        {
            double br0 = BestResponseValue(strategy, bestResponder: 0);
            double br1 = BestResponseValue(strategy, bestResponder: 1);

            // br0 is player 0's payoff when player 0 best-responds and player 1 plays `strategy`.
            // br1 is player 1's payoff (to player 1) when player 1 best-responds. In a zero-sum
            // game at equilibrium br0 = -br1; the gap br0 + br1 is total exploitability.
            return br0 + br1;
        }

        /// <summary>
        /// The best-response value (to <paramref name="bestResponder"/>) when that player plays a best
        /// response and the opponent plays <paramref name="strategy"/>, averaged over the deal.
        /// <para>
        /// Crucially this is an <em>information-set-aware</em> best response: the best responder
        /// must commit to a single action per information set (it does not know the opponent's
        /// card), so we pick the action that maximizes the opponent-reach-weighted counterfactual
        /// value, summed over the deals that reach each info set. A naive per-node maximization
        /// would illegally peek at the opponent's hand and wildly overstate exploitability.
        /// We solve for the best pure response by a short fixed-point over the per-info-set choices,
        /// which converges in a couple of passes on Kuhn's tiny tree.
        /// </para>
        /// </summary>
        private static double BestResponseValue(IReadOnlyDictionary<string, double[]> strategy, int bestResponder)
        {
            var game = new KuhnPoker();
            var choice = new Dictionary<string, int>(StringComparer.Ordinal);

            // EV to the best responder given the currently-fixed action choices at its info sets.
            double Evaluate(KuhnState state)
            {
                if (game.IsTerminal(state))
                {
                    return game.Payoff(state, bestResponder);
                }

                var actions = game.LegalActions(state);
                if (game.CurrentPlayer(state) == bestResponder)
                {
                    int a = choice.TryGetValue(game.InfoSetKey(state), out int c) ? c : 0;
                    return Evaluate(game.Apply(state, actions[a]));
                }

                double[] probs = strategy[game.InfoSetKey(state)];
                double value = 0.0;
                for (int a = 0; a < actions.Count; a++)
                {
                    if (probs[a] != 0.0)
                    {
                        value += probs[a] * Evaluate(game.Apply(state, actions[a]));
                    }
                }

                return value;
            }

            // Accumulate opponent-reach-weighted value of each action at each best-responder info set.
            var counterfactual = new Dictionary<string, double[]>(StringComparer.Ordinal);
            void Accumulate(KuhnState state, double opponentReach)
            {
                if (game.IsTerminal(state))
                {
                    return;
                }

                var actions = game.LegalActions(state);
                if (game.CurrentPlayer(state) == bestResponder)
                {
                    string key = game.InfoSetKey(state);
                    if (!counterfactual.TryGetValue(key, out double[]? values))
                    {
                        values = new double[actions.Count];
                        counterfactual[key] = values;
                    }

                    for (int a = 0; a < actions.Count; a++)
                    {
                        values[a] += opponentReach * Evaluate(game.Apply(state, actions[a]));
                        Accumulate(game.Apply(state, actions[a]), opponentReach);
                    }
                }
                else
                {
                    double[] probs = strategy[game.InfoSetKey(state)];
                    for (int a = 0; a < actions.Count; a++)
                    {
                        if (probs[a] != 0.0)
                        {
                            Accumulate(game.Apply(state, actions[a]), opponentReach * probs[a]);
                        }
                    }
                }
            }

            // Fixed point: re-derive counterfactual values under current choices, then greedily
            // switch each info set to its best action. Converges quickly on this tiny tree.
            for (int pass = 0; pass < 16; pass++)
            {
                counterfactual.Clear();
                foreach (var outcome in game.ChanceOutcomes(game.Root))
                {
                    Accumulate(outcome.State, outcome.Probability);
                }

                bool changed = false;
                foreach (var pair in counterfactual)
                {
                    int best = 0;
                    double bestValue = double.NegativeInfinity;
                    for (int a = 0; a < pair.Value.Length; a++)
                    {
                        if (pair.Value[a] > bestValue)
                        {
                            bestValue = pair.Value[a];
                            best = a;
                        }
                    }

                    if (!choice.TryGetValue(pair.Key, out int current) || current != best)
                    {
                        choice[pair.Key] = best;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            double total = 0.0;
            foreach (var outcome in game.ChanceOutcomes(game.Root))
            {
                total += outcome.Probability * Evaluate(outcome.State);
            }

            return total;
        }
    }
}
