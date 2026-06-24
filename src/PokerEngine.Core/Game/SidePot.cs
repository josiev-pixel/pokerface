using System.Collections.Generic;

namespace PokerEngine.Core.Game
{
    /// <summary>
    /// One layer of the pot: a fixed <see cref="Amount"/> of chips contested by a fixed set of
    /// <see cref="EligibleSeats"/> (the seats that contributed to this layer and have not folded).
    /// A hand's pot decomposes into a main pot plus zero or more side pots, each capped by an
    /// all-in player's contribution. Pure data — see <see cref="PotResolver"/> for the math.
    /// </summary>
    public sealed class SidePot
    {
        public SidePot(int amount, IReadOnlyList<int> contributorSeats, IReadOnlyList<int> eligibleSeats)
        {
            Amount = amount;
            ContributorSeats = contributorSeats;
            EligibleSeats = eligibleSeats;
        }

        /// <summary>Total chips in this layer (always &gt;= 0).</summary>
        public int Amount { get; }

        /// <summary>
        /// Every seat that put chips into this layer (folded or not), in ascending order. Used to
        /// refund the layer in the degenerate case where no eligible winner remains.
        /// </summary>
        public IReadOnlyList<int> ContributorSeats { get; }

        /// <summary>
        /// Seats that may win this layer: they contributed to it and did not fold. Seat indices
        /// are in ascending order so iteration is deterministic. May be empty only in degenerate
        /// "dead money" situations (every contributor folded), in which case the layer is refunded
        /// to its <see cref="ContributorSeats"/> by <see cref="PotResolver"/>.
        /// </summary>
        public IReadOnlyList<int> EligibleSeats { get; }
    }
}
