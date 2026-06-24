using System;
using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// Heads-up no-limit Hold'em preflop push/fold as a bucket-level
    /// <see cref="ICfrGame{TState}"/>.
    /// <para>
    /// Chance deals an ordered small-blind bucket and big-blind bucket according
    /// to injected blocker-aware weights. The small blind may jam or fold; after
    /// a jam the big blind may call or fold. Showdown EV comes from the injected
    /// small-blind equity matrix.
    /// </para>
    /// </summary>
    public sealed class HoldemPushFold : ICfrGame<PushFoldState>
    {
        /// <summary>The small blind posted before action.</summary>
        public const double SmallBlind = 0.5;

        /// <summary>The big blind posted before action.</summary>
        public const double BigBlind = 1.0;

        private static readonly GameAction Jam = new GameAction("j");
        private static readonly GameAction Fold = new GameAction("f");
        private static readonly GameAction Call = new GameAction("c");

        private static readonly IReadOnlyList<GameAction> JamOrFold = new[] { Jam, Fold };
        private static readonly IReadOnlyList<GameAction> CallOrFold = new[] { Call, Fold };

        private readonly double[,] _equity;
        private readonly IReadOnlyList<ChanceOutcome<PushFoldState>> _chanceOutcomes;

        /// <summary>
        /// Creates a push/fold game from precomputed bucket equity and deal-weight matrices.
        /// </summary>
        /// <param name="equity">Small blind showdown equity by [small blind bucket, big blind bucket].</param>
        /// <param name="weight">Non-negative chance weights by [small blind bucket, big blind bucket].</param>
        /// <param name="effectiveStack">Effective all-in stack in chips, including posted blinds.</param>
        public HoldemPushFold(double[,] equity, double[,] weight, double effectiveStack)
        {
            ArgumentNullException.ThrowIfNull(equity);
            ArgumentNullException.ThrowIfNull(weight);
            ValidateDimensions(equity, weight);
            if (!double.IsFinite(effectiveStack) || effectiveStack <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveStack), "Effective stack must be positive.");
            }

            int bucketCount = equity.GetLength(0);
            _equity = CopyEquity(equity);
            EffectiveStack = effectiveStack;
            BucketCount = bucketCount;
            _chanceOutcomes = BuildChanceOutcomes(weight);
        }

        /// <inheritdoc/>
        public int PlayerCount => 2;

        /// <inheritdoc/>
        public PushFoldState Root => PushFoldState.ChanceRoot;

        /// <summary>The effective all-in stack in chips, including posted blinds.</summary>
        public double EffectiveStack { get; }

        /// <summary>The number of preflop buckets represented by each matrix dimension.</summary>
        public int BucketCount { get; }

        /// <inheritdoc/>
        public bool IsTerminal(PushFoldState state) =>
            !state.IsChanceRoot && (state.History == "f" || state.History == "jf" || state.History == "jc");

        /// <inheritdoc/>
        public bool IsChance(PushFoldState state) => state.IsChanceRoot;

        /// <inheritdoc/>
        public IEnumerable<ChanceOutcome<PushFoldState>> ChanceOutcomes(PushFoldState state)
        {
            if (!state.IsChanceRoot)
            {
                throw new InvalidOperationException("Chance outcomes requested for a non-chance state.");
            }

            return _chanceOutcomes;
        }

        /// <inheritdoc/>
        public int CurrentPlayer(PushFoldState state)
        {
            ValidateDecisionState(state);
            return state.History.Length == 0 ? 0 : 1;
        }

        /// <inheritdoc/>
        public IReadOnlyList<GameAction> LegalActions(PushFoldState state)
        {
            ValidateDecisionState(state);
            return state.History.Length == 0 ? JamOrFold : CallOrFold;
        }

        /// <inheritdoc/>
        public string InfoSetKey(PushFoldState state)
        {
            ValidateDecisionState(state);
            return state.History.Length == 0
                ? string.Concat("S", state.SmallBlindBucket.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : string.Concat("B", state.BigBlindBucket.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <inheritdoc/>
        public PushFoldState Apply(PushFoldState state, GameAction action)
        {
            ValidateDecisionState(state);
            if (state.History.Length == 0)
            {
                if (action.Code == "j")
                {
                    return PushFoldState.Dealt(state.SmallBlindBucket, state.BigBlindBucket, "j");
                }

                if (action.Code == "f")
                {
                    return PushFoldState.Dealt(state.SmallBlindBucket, state.BigBlindBucket, "f");
                }
            }
            else if (state.History == "j")
            {
                if (action.Code == "c")
                {
                    return PushFoldState.Dealt(state.SmallBlindBucket, state.BigBlindBucket, "jc");
                }

                if (action.Code == "f")
                {
                    return PushFoldState.Dealt(state.SmallBlindBucket, state.BigBlindBucket, "jf");
                }
            }

            throw new ArgumentException($"Action '{action.Code}' is not legal for history '{state.History}'.", nameof(action));
        }

        /// <inheritdoc/>
        public double Payoff(PushFoldState state, int player)
        {
            if (player < 0 || player >= PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(player), "Player must be 0 or 1.");
            }

            if (!IsTerminal(state))
            {
                throw new InvalidOperationException("Payoff requested for a non-terminal state.");
            }

            double payoff0 = state.History switch
            {
                "f" => -SmallBlind,
                "jf" => BigBlind,
                "jc" => (_equity[state.SmallBlindBucket, state.BigBlindBucket] * (2.0 * EffectiveStack)) - EffectiveStack,
                _ => throw new InvalidOperationException($"Unknown terminal history '{state.History}'."),
            };

            return player == 0 ? payoff0 : -payoff0;
        }

        private static void ValidateDimensions(double[,] equity, double[,] weight)
        {
            if (equity.Rank != 2 || weight.Rank != 2)
            {
                throw new ArgumentException("Equity and weight must be two-dimensional matrices.");
            }

            int rows = equity.GetLength(0);
            int columns = equity.GetLength(1);
            if (rows == 0 || rows != columns)
            {
                throw new ArgumentException("Equity must be a non-empty square matrix.", nameof(equity));
            }

            if (weight.GetLength(0) != rows || weight.GetLength(1) != columns)
            {
                throw new ArgumentException("Weight must have the same square dimensions as equity.", nameof(weight));
            }
        }

        private static double[,] CopyEquity(double[,] source)
        {
            int count = source.GetLength(0);
            var copy = new double[count, count];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    double value = source[i, j];
                    if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(source), "Equity entries must be finite values in [0, 1].");
                    }

                    copy[i, j] = value;
                }
            }

            return copy;
        }

        private IReadOnlyList<ChanceOutcome<PushFoldState>> BuildChanceOutcomes(double[,] weight)
        {
            int count = weight.GetLength(0);
            double total = 0.0;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    double value = weight[i, j];
                    if (!double.IsFinite(value) || value < 0.0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(weight), "Weight entries must be finite non-negative values.");
                    }

                    total += value;
                }
            }

            if (total <= 0.0)
            {
                throw new ArgumentException("Weight matrix must contain positive total mass.", nameof(weight));
            }

            var outcomes = new List<ChanceOutcome<PushFoldState>>();
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    double value = weight[i, j];
                    if (value > 0.0)
                    {
                        outcomes.Add(new ChanceOutcome<PushFoldState>(
                            PushFoldState.Dealt(i, j),
                            value / total));
                    }
                }
            }

            return outcomes;
        }

        private static void ValidateDecisionState(PushFoldState state)
        {
            if (state.IsChanceRoot || state.History == "f" || state.History == "jf" || state.History == "jc")
            {
                throw new InvalidOperationException("Decision data requested for a non-decision state.");
            }
        }
    }

    /// <summary>
    /// Immutable state for the heads-up preflop push/fold game.
    /// </summary>
    public readonly struct PushFoldState : IEquatable<PushFoldState>
    {
        private PushFoldState(int smallBlindBucket, int bigBlindBucket, string history, bool isChanceRoot)
        {
            SmallBlindBucket = smallBlindBucket;
            BigBlindBucket = bigBlindBucket;
            History = history;
            IsChanceRoot = isChanceRoot;
        }

        /// <summary>The pre-deal chance root.</summary>
        public static PushFoldState ChanceRoot { get; } = new PushFoldState(-1, -1, string.Empty, true);

        /// <summary>The small blind bucket index, or -1 at the chance root.</summary>
        public int SmallBlindBucket { get; }

        /// <summary>The big blind bucket index, or -1 at the chance root.</summary>
        public int BigBlindBucket { get; }

        /// <summary>The public action history: empty, "j", "f", "jf", or "jc".</summary>
        public string History { get; }

        /// <summary>True for the distinguished pre-deal chance root.</summary>
        public bool IsChanceRoot { get; }

        /// <summary>Creates a dealt state before the small blind acts.</summary>
        public static PushFoldState Dealt(int smallBlindBucket, int bigBlindBucket) =>
            new PushFoldState(smallBlindBucket, bigBlindBucket, string.Empty, false);

        /// <summary>Creates a dealt state with the given public action history.</summary>
        public static PushFoldState Dealt(int smallBlindBucket, int bigBlindBucket, string history) =>
            new PushFoldState(smallBlindBucket, bigBlindBucket, history, false);

        /// <inheritdoc/>
        public bool Equals(PushFoldState other) =>
            SmallBlindBucket == other.SmallBlindBucket &&
            BigBlindBucket == other.BigBlindBucket &&
            IsChanceRoot == other.IsChanceRoot &&
            string.Equals(History, other.History, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is PushFoldState state && Equals(state);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(SmallBlindBucket, BigBlindBucket, History, IsChanceRoot);
    }
}
