namespace PokerEngine.Decision
{
    /// <summary>
    /// A minimal opponent read for the bounded-exploitation layer. The full Profiling layer
    /// (three horizons, range estimation, leak detection) will produce richer models; this is
    /// the first, deliberately small lever: how often the opponent folds to our bets, plus how
    /// much we trust that read. Confidence drives the blend weight w = f(c), capped at w_max,
    /// so a thin or absent read keeps us on the GTO/heuristic baseline (DECISION_ALGORITHM §6).
    /// </summary>
    public sealed record OpponentModel
    {
        /// <summary>How often the opponent folds when we bet/raise, in [0,1].</summary>
        public required double FoldToBet { get; init; }

        /// <summary>Read confidence in [0,1] — grows with sample size and consistency.</summary>
        public required double Confidence { get; init; }

        /// <summary>A neutral, no-information model (confidence 0): the engine plays its baseline.</summary>
        public static OpponentModel Unknown { get; } = new() { FoldToBet = 0.5, Confidence = 0.0 };
    }
}
