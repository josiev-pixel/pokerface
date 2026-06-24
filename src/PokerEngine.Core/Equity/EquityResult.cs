namespace PokerEngine.Core.Equity
{
    /// <summary>
    /// The outcome distribution of a showdown from one player's point of view, as fractions
    /// of all evaluated runouts. <see cref="Equity"/> is the standard "share of the pot":
    /// wins count full, ties split evenly (here, two-way; multiway split handled by the caller).
    /// </summary>
    public readonly struct EquityResult
    {
        public EquityResult(double win, double tie, double loss, long samples)
        {
            Win = win;
            Tie = tie;
            Loss = loss;
            Samples = samples;
        }

        /// <summary>Fraction of runouts the hand wins outright.</summary>
        public double Win { get; }

        /// <summary>Fraction of runouts the hand ties (chops).</summary>
        public double Tie { get; }

        /// <summary>Fraction of runouts the hand loses.</summary>
        public double Loss { get; }

        /// <summary>How many runouts were evaluated (enumerated or sampled).</summary>
        public long Samples { get; }

        /// <summary>Pot share: wins + half of ties (two-way split).</summary>
        public double Equity => Win + (Tie / 2.0);

        /// <summary>True when the result came from exact enumeration of every runout.</summary>
        public bool IsExact { get; init; }

        public override string ToString() =>
            $"{Equity:P2} ({(IsExact ? "exact" : "MC")}, n={Samples})";
    }
}
