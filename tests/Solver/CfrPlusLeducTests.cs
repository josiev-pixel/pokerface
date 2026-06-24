using System;
using System.Collections.Generic;
using PokerEngine.Solver;

namespace PokerEngine.Tests
{
    /// <summary>
    /// Validates CFR+ on Leduc Hold'em, which adds a second chance node and a
    /// second fixed-limit betting round beyond Kuhn poker.
    /// </summary>
    public sealed class CfrPlusLeducTests
    {
        private const int SmallIterations = 500;
        private const int LargeIterations = 5_000;

        private static IReadOnlyDictionary<string, double[]> Solve(int iterations)
        {
            var game = new LeducPoker();
            var solver = new CfrPlusSolver<LeducState>(game);
            solver.Run(iterations);
            return solver.AverageStrategy();
        }

        [Fact]
        public void AverageStrategy_ExploitabilityImprovesWithMoreIterations()
        {
            var game = new LeducPoker();
            double small = BestResponse.Exploitability(game, Solve(SmallIterations));
            double large = BestResponse.Exploitability(game, Solve(LargeIterations));

            Assert.True(large < small,
                $"Expected exploitability to fall, but {LargeIterations} iterations produced {large} versus {small} at {SmallIterations}.");
            Assert.True(large < 0.1,
                $"Leduc exploitability {large} chips/hand should be below 0.1 after {LargeIterations} iterations.");
        }

        [Fact]
        public void Solver_IsDeterministic()
        {
            var a = Solve(1_000);
            var b = Solve(1_000);

            Assert.Equal(a.Count, b.Count);
            foreach (var pair in a)
            {
                Assert.True(b.ContainsKey(pair.Key), $"Missing info set {pair.Key} on second run.");
                double[] first = pair.Value;
                double[] second = b[pair.Key];
                Assert.Equal(first.Length, second.Length);
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.Equal(first[i], second[i]);
                }
            }
        }
    }
}
