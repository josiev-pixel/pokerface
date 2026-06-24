using System;
using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// Computes information-set-aware best responses and exploitability for small
    /// two-player zero-sum games represented by <see cref="ICfrGame{TState}"/>.
    /// </summary>
    public static class BestResponse
    {
        private const int MaxPolicyImprovementPasses = 128;

        /// <summary>
        /// Computes the value, in game chips, achieved by <paramref name="responder"/>
        /// when they play a best response against <paramref name="fixedStrategy"/>.
        /// <para>
        /// The response is information-set-aware: the responder commits to one action
        /// per information set. Each candidate action is valued by summing the
        /// opponent-plus-chance reach-weighted counterfactual value of all histories
        /// in that information set. Missing opponent strategy entries fall back to a
        /// uniform distribution over the state's legal actions.
        /// </para>
        /// </summary>
        /// <typeparam name="TState">The immutable game-state representation.</typeparam>
        /// <param name="game">The game to evaluate.</param>
        /// <param name="fixedStrategy">The fixed strategy profile, keyed by information set.</param>
        /// <param name="responder">The player index that may best-respond.</param>
        /// <returns>The responder's best-response expected payoff in chips.</returns>
        public static double BestResponseValue<TState>(
            ICfrGame<TState> game,
            IReadOnlyDictionary<string, double[]> fixedStrategy,
            int responder)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(fixedStrategy);

            if (game.PlayerCount != 2)
            {
                throw new ArgumentException("Best-response evaluation supports two-player games only.", nameof(game));
            }

            if (responder < 0 || responder >= game.PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(responder), "Responder must be a valid player index.");
            }

            var choice = new Dictionary<string, int>(StringComparer.Ordinal);
            var counterfactual = new Dictionary<string, double[]>(StringComparer.Ordinal);

            double Evaluate(TState state)
            {
                if (game.IsTerminal(state))
                {
                    return game.Payoff(state, responder);
                }

                if (game.IsChance(state))
                {
                    double expected = 0.0;
                    foreach (var outcome in game.ChanceOutcomes(state))
                    {
                        expected += outcome.Probability * Evaluate(outcome.State);
                    }

                    return expected;
                }

                IReadOnlyList<GameAction> actions = game.LegalActions(state);
                string key = game.InfoSetKey(state);
                if (game.CurrentPlayer(state) == responder)
                {
                    int actionIndex = choice.TryGetValue(key, out int selected) ? selected : 0;
                    if (actionIndex < 0 || actionIndex >= actions.Count)
                    {
                        throw new InvalidOperationException($"Information set '{key}' has inconsistent action counts.");
                    }

                    return Evaluate(game.Apply(state, actions[actionIndex]));
                }

                double[] probabilities = StrategyFor(fixedStrategy, key, actions.Count);
                double value = 0.0;
                for (int a = 0; a < actions.Count; a++)
                {
                    if (probabilities[a] != 0.0)
                    {
                        value += probabilities[a] * Evaluate(game.Apply(state, actions[a]));
                    }
                }

                return value;
            }

            void Accumulate(TState state, double opponentChanceReach)
            {
                if (game.IsTerminal(state))
                {
                    return;
                }

                if (game.IsChance(state))
                {
                    foreach (var outcome in game.ChanceOutcomes(state))
                    {
                        if (outcome.Probability != 0.0)
                        {
                            Accumulate(outcome.State, opponentChanceReach * outcome.Probability);
                        }
                    }

                    return;
                }

                IReadOnlyList<GameAction> actions = game.LegalActions(state);
                string key = game.InfoSetKey(state);
                if (game.CurrentPlayer(state) == responder)
                {
                    if (!counterfactual.TryGetValue(key, out double[]? values))
                    {
                        values = new double[actions.Count];
                        counterfactual[key] = values;
                    }
                    else if (values.Length != actions.Count)
                    {
                        throw new InvalidOperationException($"Information set '{key}' has inconsistent action counts.");
                    }

                    for (int a = 0; a < actions.Count; a++)
                    {
                        TState next = game.Apply(state, actions[a]);
                        values[a] += opponentChanceReach * Evaluate(next);
                        Accumulate(next, opponentChanceReach);
                    }

                    return;
                }

                double[] probabilities = StrategyFor(fixedStrategy, key, actions.Count);
                for (int a = 0; a < actions.Count; a++)
                {
                    if (probabilities[a] != 0.0)
                    {
                        Accumulate(game.Apply(state, actions[a]), opponentChanceReach * probabilities[a]);
                    }
                }
            }

            for (int pass = 0; pass < MaxPolicyImprovementPasses; pass++)
            {
                counterfactual.Clear();
                Accumulate(game.Root, 1.0);

                bool changed = false;
                foreach (var pair in counterfactual)
                {
                    int best = BestActionIndex(pair.Value);
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

            return Evaluate(game.Root);
        }

        /// <summary>
        /// Computes exploitability in chips per hand for a two-player zero-sum strategy
        /// profile. This is half of NashConv: <c>0.5 * (BR0 + BR1)</c>.
        /// </summary>
        /// <typeparam name="TState">The immutable game-state representation.</typeparam>
        /// <param name="game">The game to evaluate.</param>
        /// <param name="strategy">The strategy profile, keyed by information set.</param>
        /// <returns>Exploitability in game chips per hand.</returns>
        public static double Exploitability<TState>(
            ICfrGame<TState> game,
            IReadOnlyDictionary<string, double[]> strategy)
        {
            double br0 = BestResponseValue(game, strategy, 0);
            double br1 = BestResponseValue(game, strategy, 1);
            double exploitability = 0.5 * (br0 + br1);
            return exploitability > 0.0 ? exploitability : 0.0;
        }

        /// <summary>
        /// Computes exploitability in milli-big-blinds per hand for a two-player
        /// zero-sum strategy profile.
        /// </summary>
        /// <typeparam name="TState">The immutable game-state representation.</typeparam>
        /// <param name="game">The game to evaluate.</param>
        /// <param name="strategy">The strategy profile, keyed by information set.</param>
        /// <param name="bigBlind">The chip value of one big blind.</param>
        /// <returns>Exploitability in milli-big-blinds per hand.</returns>
        public static double ExploitabilityMilliBigBlinds<TState>(
            ICfrGame<TState> game,
            IReadOnlyDictionary<string, double[]> strategy,
            double bigBlind)
        {
            if (bigBlind <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(bigBlind), "Big blind must be positive.");
            }

            return Exploitability(game, strategy) * 1000.0 / bigBlind;
        }

        private static int BestActionIndex(double[] values)
        {
            int best = 0;
            double bestValue = double.NegativeInfinity;
            for (int a = 0; a < values.Length; a++)
            {
                if (values[a] > bestValue)
                {
                    bestValue = values[a];
                    best = a;
                }
            }

            return best;
        }

        private static double[] StrategyFor(
            IReadOnlyDictionary<string, double[]> strategy,
            string key,
            int actionCount)
        {
            if (strategy.TryGetValue(key, out double[]? probabilities))
            {
                if (probabilities.Length != actionCount)
                {
                    throw new ArgumentException(
                        $"Strategy for information set '{key}' has {probabilities.Length} actions, but the game exposes {actionCount}.",
                        nameof(strategy));
                }

                return probabilities;
            }

            var uniform = new double[actionCount];
            double probability = 1.0 / actionCount;
            for (int a = 0; a < actionCount; a++)
            {
                uniform[a] = probability;
            }

            return uniform;
        }
    }
}
