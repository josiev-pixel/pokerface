using System;
using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// A game-agnostic CFR+ solver for small two-player zero-sum extensive-form games.
    /// <para>
    /// It runs vanilla (full-tree) CFR+ — no Monte-Carlo sampling — which is exact and
    /// ideal for tiny games like Kuhn poker. CFR+ here means three things versus vanilla
    /// CFR: regret-matching+ for the current strategy, cumulative regret floored at zero
    /// (both in <see cref="InformationSet"/>), and <em>linear averaging</em> — iteration
    /// <c>t</c> contributes its strategy with weight <c>t</c>.
    /// </para>
    /// <para>
    /// Updates are <em>alternating</em>: on each iteration the tree is traversed twice,
    /// once updating player 0's regrets and once updating player 1's. The whole process
    /// is fully deterministic; there is no randomness anywhere.
    /// </para>
    /// </summary>
    /// <typeparam name="TState">The game-state representation traversed by the solver.</typeparam>
    public sealed class CfrPlusSolver<TState>
    {
        private readonly ICfrGame<TState> _game;
        private readonly Dictionary<string, InformationSet> _infoSets = new Dictionary<string, InformationSet>(StringComparer.Ordinal);

        /// <summary>Creates a solver for the given game.</summary>
        /// <param name="game">The extensive-form game to solve (must be two-player).</param>
        public CfrPlusSolver(ICfrGame<TState> game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            if (_game.PlayerCount != 2)
            {
                throw new ArgumentException("The CFR+ core supports two-player games only.", nameof(game));
            }
        }

        /// <summary>
        /// Runs <paramref name="iterations"/> iterations of CFR+ with alternating updates
        /// and linear averaging.
        /// </summary>
        /// <param name="iterations">Number of CFR+ iterations to run (must be &gt; 0).</param>
        public void Run(int iterations)
        {
            if (iterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations), "Iteration count must be positive.");
            }

            for (int t = 1; t <= iterations; t++)
            {
                // Linear averaging: weight this iteration's strategy contribution by t.
                double strategyWeight = t;
                for (int player = 0; player < 2; player++)
                {
                    Traverse(_game.Root, player, 1.0, 1.0, strategyWeight);
                }
            }
        }

        /// <summary>
        /// The average strategy per information set, mapping the info-set key to a
        /// probability distribution over its actions (in the game's stable action order).
        /// This is the approximate Nash equilibrium produced by the solve.
        /// </summary>
        public IReadOnlyDictionary<string, double[]> AverageStrategy()
        {
            var result = new Dictionary<string, double[]>(_infoSets.Count, StringComparer.Ordinal);
            foreach (var pair in _infoSets)
            {
                result[pair.Key] = pair.Value.AverageStrategy();
            }

            return result;
        }

        /// <summary>
        /// Counterfactual regret minimization (CFR+) traversal.
        /// Returns the expected utility of <paramref name="state"/> to <paramref name="updatingPlayer"/>,
        /// computed under the players' current strategies.
        /// </summary>
        /// <param name="state">The current state.</param>
        /// <param name="updatingPlayer">The player whose regrets/strategy are updated on this pass.</param>
        /// <param name="reachUpdating">Probability the updating player reaches this state (their own action probabilities).</param>
        /// <param name="reachOther">Probability the opponent and chance reach this state (the counterfactual reach for the updating player).</param>
        /// <param name="strategyWeight">Linear-averaging weight for this iteration.</param>
        private double Traverse(TState state, int updatingPlayer, double reachUpdating, double reachOther, double strategyWeight)
        {
            if (_game.IsTerminal(state))
            {
                return _game.Payoff(state, updatingPlayer);
            }

            if (_game.IsChance(state))
            {
                double expected = 0.0;
                foreach (var outcome in _game.ChanceOutcomes(state))
                {
                    expected += outcome.Probability *
                        Traverse(outcome.State, updatingPlayer, reachUpdating, reachOther * outcome.Probability, strategyWeight);
                }

                return expected;
            }

            int actingPlayer = _game.CurrentPlayer(state);
            IReadOnlyList<GameAction> actions = _game.LegalActions(state);
            int actionCount = actions.Count;
            string key = _game.InfoSetKey(state);
            InformationSet node = GetOrCreate(key, actionCount);

            double[] strategy = node.CurrentStrategy();
            var actionUtil = new double[actionCount];
            double nodeUtil = 0.0;

            for (int a = 0; a < actionCount; a++)
            {
                TState next = _game.Apply(state, actions[a]);
                double childReachUpdating = actingPlayer == updatingPlayer ? reachUpdating * strategy[a] : reachUpdating;
                double childReachOther = actingPlayer == updatingPlayer ? reachOther : reachOther * strategy[a];

                actionUtil[a] = Traverse(next, updatingPlayer, childReachUpdating, childReachOther, strategyWeight);
                nodeUtil += strategy[a] * actionUtil[a];
            }

            if (actingPlayer == updatingPlayer)
            {
                // Counterfactual regret: weight by the opponent/chance reach probability.
                var regret = new double[actionCount];
                for (int a = 0; a < actionCount; a++)
                {
                    regret[a] = reachOther * (actionUtil[a] - nodeUtil);
                }

                node.ObserveRegret(regret);

                // Linear averaging: accumulate weighted by the updating player's own reach and the iteration weight.
                node.AccumulateStrategy(strategy, strategyWeight * reachUpdating);
            }

            return nodeUtil;
        }

        private InformationSet GetOrCreate(string key, int actionCount)
        {
            if (!_infoSets.TryGetValue(key, out InformationSet? node))
            {
                node = new InformationSet(actionCount);
                _infoSets[key] = node;
            }

            return node;
        }
    }
}
