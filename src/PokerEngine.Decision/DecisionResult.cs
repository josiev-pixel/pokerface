using System.Collections.Generic;
using System.Linq;

namespace PokerEngine.Decision
{
    /// <summary>The kind of action the engine can recommend.</summary>
    public enum Move
    {
        Fold,
        Check,
        Call,
        Bet,
        Raise,
    }

    /// <summary>
    /// One leg of a (possibly mixed) strategy: a move, the chips it commits now, and the
    /// probability the strategy assigns it. For <see cref="Move.Fold"/>/<see cref="Move.Check"/>
    /// <see cref="Chips"/> is 0; for <see cref="Move.Call"/> it is the amount to call; for
    /// <see cref="Move.Bet"/>/<see cref="Move.Raise"/> it is the total chips added now.
    /// </summary>
    public readonly record struct ActionOption(Move Move, int Chips, double Probability)
    {
        public override string ToString() =>
            Chips > 0 ? $"{Move} {Chips} ({Probability:P0})" : $"{Move} ({Probability:P0})";
    }

    /// <summary>
    /// The engine's answer for a spot: a single sampled action plus the full mixed-strategy
    /// distribution it was drawn from, the supporting numbers, the exploitation weight used,
    /// and a plain-language explanation. Everything is inspectable — the engine never returns a
    /// bare "fold" you can't interrogate (POKER_THEORY §why-we-lean-deterministic).
    /// </summary>
    public sealed record DecisionResult
    {
        public required Move Move { get; init; }

        /// <summary>Chips committed by the chosen action (see <see cref="ActionOption.Chips"/>).</summary>
        public required int Chips { get; init; }

        /// <summary>The full strategy distribution over legal actions (probabilities sum to 1).</summary>
        public required IReadOnlyList<ActionOption> Strategy { get; init; }

        /// <summary>Our estimated equity in the spot (vs. the modeled/assumed opponent).</summary>
        public required double Equity { get; init; }

        /// <summary>Pot-odds break-even equity when facing a bet (0 when we are not facing one).</summary>
        public required double RequiredEquity { get; init; }

        /// <summary>The bounded exploitation weight w∈[0, w_max] blended in (0 = pure baseline).</summary>
        public required double ExploitWeight { get; init; }

        public required string Explanation { get; init; }

        public override string ToString()
        {
            string mix = string.Join(", ", Strategy.Where(o => o.Probability > 0).Select(o => o.ToString()));
            return $"{Move}{(Chips > 0 ? " " + Chips : "")}  [{mix}]  — {Explanation}";
        }
    }
}
