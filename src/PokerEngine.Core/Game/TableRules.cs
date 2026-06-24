using System;

namespace PokerEngine.Core.Game
{
    /// <summary>
    /// The stakes for a hand: small/big blind and an optional per-player ante. Chips are integers.
    /// Validated on construction so an invalid table can never reach the engine. A record for value
    /// equality and <c>with</c>-expressions; constructed as <c>new TableRules(sb, bb[, ante])</c>.
    /// </summary>
    public sealed record TableRules
    {
        /// <summary>Create rules, validating that <c>BigBlind &gt;= SmallBlind &gt; 0</c> and the ante is non-negative.</summary>
        // Parameters are named to match the properties so call sites can use named arguments
        // (e.g. new TableRules(SmallBlind: 1, BigBlind: 2)) exactly as a positional record would.
        public TableRules(int SmallBlind, int BigBlind, int Ante = 0)
        {
            if (SmallBlind <= 0)
                throw new ArgumentOutOfRangeException(nameof(SmallBlind), "Small blind must be positive.");
            if (BigBlind < SmallBlind)
                throw new ArgumentOutOfRangeException(nameof(BigBlind), "Big blind must be at least the small blind.");
            if (Ante < 0)
                throw new ArgumentOutOfRangeException(nameof(Ante), "Ante must be non-negative.");

            this.SmallBlind = SmallBlind;
            this.BigBlind = BigBlind;
            this.Ante = Ante;
        }

        /// <summary>The small blind.</summary>
        public int SmallBlind { get; init; }

        /// <summary>The big blind (at least the small blind).</summary>
        public int BigBlind { get; init; }

        /// <summary>The per-player ante (0 if none).</summary>
        public int Ante { get; init; }
    }
}
