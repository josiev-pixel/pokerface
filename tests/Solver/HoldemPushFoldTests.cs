using System;
using System.Collections.Generic;
using PokerEngine.Solver;

namespace PokerEngine.Tests
{
    /// <summary>
    /// Validates the first Hold'em-shaped CFR game against a tiny synthetic
    /// bucket matrix so the test stays fast and independent of equity precompute.
    /// </summary>
    public sealed class HoldemPushFoldTests
    {
        private const int Jam = 0;
        private const int Iterations = 20_000;

        private static readonly double[,] Equity =
        {
            { 0.5, 0.8, 0.9 },
            { 0.2, 0.5, 0.7 },
            { 0.1, 0.3, 0.5 },
        };

        private static readonly double[,] Weight =
        {
            { 1.0, 1.0, 1.0 },
            { 1.0, 1.0, 1.0 },
            { 1.0, 1.0, 1.0 },
        };

        private static IReadOnlyDictionary<string, double[]> Solve(int iterations)
        {
            var game = new HoldemPushFold(Equity, Weight, 10.0);
            var solver = new CfrPlusSolver<PushFoldState>(game);
            solver.Run(iterations);
            return solver.AverageStrategy();
        }

        [Fact]
        public void AverageStrategy_HasLowExploitability()
        {
            var game = new HoldemPushFold(Equity, Weight, 10.0);
            IReadOnlyDictionary<string, double[]> strategy = Solve(Iterations);

            double exploitability = BestResponse.Exploitability(game, strategy);

            Assert.True(exploitability < 0.01,
                $"Exploitability {exploitability} chips/hand should be below 0.01.");
        }

        [Fact]
        public void Solver_IsDeterministic()
        {
            IReadOnlyDictionary<string, double[]> first = Solve(Iterations);
            IReadOnlyDictionary<string, double[]> second = Solve(Iterations);

            Assert.Equal(first.Count, second.Count);
            foreach (KeyValuePair<string, double[]> pair in first)
            {
                Assert.True(second.ContainsKey(pair.Key), $"Missing info set {pair.Key} on second run.");
                double[] firstProbabilities = pair.Value;
                double[] secondProbabilities = second[pair.Key];
                Assert.Equal(firstProbabilities.Length, secondProbabilities.Length);
                for (int i = 0; i < firstProbabilities.Length; i++)
                {
                    Assert.Equal(firstProbabilities[i], secondProbabilities[i]);
                }
            }
        }

        [Fact]
        public void StrongestSmallBlindBucket_JamsNearlyAlways()
        {
            IReadOnlyDictionary<string, double[]> strategy = Solve(Iterations);

            Assert.True(strategy["S0"][Jam] > 0.98,
                $"Strongest small blind bucket jammed with probability {strategy["S0"][Jam]}.");
        }

        [Fact]
        public void PayoffModel_MatchesPushFoldSpecification()
        {
            var game = new HoldemPushFold(Equity, Weight, 10.0);
            PushFoldState dealt = PushFoldState.Dealt(0, 1);
            PushFoldState smallBlindFold = game.Apply(dealt, new GameAction("f"));
            PushFoldState jam = game.Apply(dealt, new GameAction("j"));
            PushFoldState bigBlindFold = game.Apply(jam, new GameAction("f"));
            PushFoldState call = game.Apply(jam, new GameAction("c"));

            Assert.Equal(-0.5, game.Payoff(smallBlindFold, 0));
            Assert.Equal(1.0, game.Payoff(bigBlindFold, 0));
            Assert.Equal(6.0, game.Payoff(call, 0));
        }
    }
}
