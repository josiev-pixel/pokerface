namespace PokerEngine.Decision
{
    /// <summary>
    /// The tunable knobs of the baseline policy. These are the Lead-owned numbers (thresholds,
    /// sizings, mixed-strategy frequencies, the exploit cap) called out in AGENTS.md — workers
    /// wire systems, the Lead balances the poker. Defaults are reasonable heads-up starting
    /// points, not solved values; they are meant to be tuned against measured exploitability/EV.
    /// </summary>
    public sealed record EngineConfig
    {
        /// <summary>Monte-Carlo samples for equity estimation (seeded → reproducible).</summary>
        public int EquitySamples { get; init; } = 20_000;

        /// <summary>Cap on the exploitation blend weight w (w_max); a wrong read can't run away.</summary>
        public double MaxExploitWeight { get; init; } = 0.5;

        // --- Preflop, deep-stack (Chen score) ---
        /// <summary>Heads-up button/SB opens (raises first-in) when Chen ≥ this.</summary>
        public int OpenChen { get; init; } = 4;
        /// <summary>Big blind flat-calls a raise when Chen ≥ this.</summary>
        public int CallRaiseChen { get; init; } = 6;
        /// <summary>Big blind 3-bets when Chen ≥ this.</summary>
        public int ThreeBetChen { get; init; } = 9;
        /// <summary>Open-raise size in big blinds.</summary>
        public double PreflopOpenBigBlinds { get; init; } = 2.5;

        // --- Postflop equity bands (vs. the assumed/modeled opponent) ---
        /// <summary>Bet for value (first to act) at or above this equity.</summary>
        public double ValueBetEquity { get; init; } = 0.62;
        /// <summary>Hands at or below this equity are bluff candidates.</summary>
        public double BluffCeilingEquity { get; init; } = 0.38;
        /// <summary>Raise a bet for value at or above this equity.</summary>
        public double RaiseEquity { get; init; } = 0.70;

        // --- Mixed-strategy frequencies (the seeded "do X p% of the time" behavior) ---
        public double ValueBetFreq { get; init; } = 0.90;
        public double BluffFreq { get; init; } = 0.33;
        public double ValueRaiseFreq { get; init; } = 0.80;
        /// <summary>Bluff-catch frequency for hands just below the pot-odds price (defends toward MDF).</summary>
        public double MarginalDefendFreq { get; init; } = 0.5;
        /// <summary>How close to the required equity still counts as a defensible bluff-catch.</summary>
        public double MarginalDefendBand { get; init; } = 0.75;

        // --- Bet sizing (fraction of pot) ---
        public double BetSizing { get; init; } = 0.66;
        public double RaiseSizing { get; init; } = 1.0;

        public static EngineConfig Default { get; } = new();
    }
}
