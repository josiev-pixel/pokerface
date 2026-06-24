using System;

namespace PokerEngine.Solver
{
    /// <summary>
    /// The regret/strategy bookkeeping for a single decision point (information set)
    /// of an extensive-form game, identified by a string key.
    /// <para>
    /// Holds two accumulators over the actions available at this node:
    /// the cumulative regret <c>R[a]</c> (used by regret-matching+ to pick the
    /// next strategy) and the cumulative strategy <c>S[a]</c> (used to form the
    /// average strategy that actually converges to equilibrium).
    /// </para>
    /// <para>
    /// This type is game-agnostic: it knows nothing about poker, only about the
    /// number of actions at the node. The CFR+ flavour shows up in two places —
    /// <see cref="CurrentStrategy"/> uses regret-matching+ (only positive regrets
    /// count), and <see cref="ObserveRegret"/> floors cumulative regret at zero.
    /// </para>
    /// </summary>
    public sealed class InformationSet
    {
        private readonly double[] _cumulativeRegret;
        private readonly double[] _cumulativeStrategy;

        /// <summary>Creates an information set with the given number of legal actions.</summary>
        /// <param name="actionCount">Number of actions available at this decision point (must be &gt; 0).</param>
        public InformationSet(int actionCount)
        {
            if (actionCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionCount), "An information set must have at least one action.");
            }

            ActionCount = actionCount;
            _cumulativeRegret = new double[actionCount];
            _cumulativeStrategy = new double[actionCount];
        }

        /// <summary>Number of actions available at this decision point.</summary>
        public int ActionCount { get; }

        /// <summary>
        /// The current strategy by <em>regret-matching+</em>: probabilities are
        /// proportional to <c>max(R[a], 0)</c>. If no action has positive cumulative
        /// regret the strategy is uniform.
        /// </summary>
        /// <returns>A freshly allocated probability distribution over the actions.</returns>
        public double[] CurrentStrategy()
        {
            var strategy = new double[ActionCount];
            double positiveSum = 0.0;
            for (int a = 0; a < ActionCount; a++)
            {
                double r = _cumulativeRegret[a];
                if (r > 0.0)
                {
                    strategy[a] = r;
                    positiveSum += r;
                }
            }

            if (positiveSum > 0.0)
            {
                for (int a = 0; a < ActionCount; a++)
                {
                    strategy[a] /= positiveSum;
                }
            }
            else
            {
                double uniform = 1.0 / ActionCount;
                for (int a = 0; a < ActionCount; a++)
                {
                    strategy[a] = uniform;
                }
            }

            return strategy;
        }

        /// <summary>
        /// Adds <paramref name="weight"/> · <paramref name="strategy"/> into the
        /// cumulative strategy accumulator (weighted/linear averaging). CFR+ weights
        /// each iteration <c>t</c> by <c>t</c>, giving later, better strategies more pull.
        /// </summary>
        /// <param name="strategy">The strategy played at this iteration.</param>
        /// <param name="weight">The averaging weight for this iteration (e.g. the reach probability times the iteration index).</param>
        public void AccumulateStrategy(double[] strategy, double weight)
        {
            if (strategy is null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (strategy.Length != ActionCount)
            {
                throw new ArgumentException("Strategy length does not match the action count.", nameof(strategy));
            }

            for (int a = 0; a < ActionCount; a++)
            {
                _cumulativeStrategy[a] += weight * strategy[a];
            }
        }

        /// <summary>
        /// The average strategy: the cumulative strategy normalized to a probability
        /// distribution. This is the quantity that converges to the equilibrium.
        /// If nothing has been accumulated the result is uniform.
        /// </summary>
        /// <returns>A freshly allocated probability distribution over the actions.</returns>
        public double[] AverageStrategy()
        {
            var average = new double[ActionCount];
            double sum = 0.0;
            for (int a = 0; a < ActionCount; a++)
            {
                sum += _cumulativeStrategy[a];
            }

            if (sum > 0.0)
            {
                for (int a = 0; a < ActionCount; a++)
                {
                    average[a] = _cumulativeStrategy[a] / sum;
                }
            }
            else
            {
                double uniform = 1.0 / ActionCount;
                for (int a = 0; a < ActionCount; a++)
                {
                    average[a] = uniform;
                }
            }

            return average;
        }

        /// <summary>
        /// Applies a CFR+ regret update: <c>R[a] = max(R[a] + regret[a], 0)</c>.
        /// The flooring at zero is what distinguishes CFR+ from vanilla CFR and is
        /// responsible for its faster, smoother convergence.
        /// </summary>
        /// <param name="regret">Per-action counterfactual regret for this iteration.</param>
        public void ObserveRegret(double[] regret)
        {
            if (regret is null)
            {
                throw new ArgumentNullException(nameof(regret));
            }

            if (regret.Length != ActionCount)
            {
                throw new ArgumentException("Regret length does not match the action count.", nameof(regret));
            }

            for (int a = 0; a < ActionCount; a++)
            {
                double updated = _cumulativeRegret[a] + regret[a];
                _cumulativeRegret[a] = updated > 0.0 ? updated : 0.0;
            }
        }
    }
}
