using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine.Abstraction
{
    /// <summary>
    /// Represents a set of bet sizes as fractions of the pot.
    /// </summary>
    public sealed class BetSizeSet
    {
        private readonly IReadOnlyList<double> _fractions;

        /// <summary>
        /// Initializes a new instance of the <see cref="BetSizeSet"/> class.
        /// </summary>
        /// <param name="potFractions">The pot fractions representing bet sizes.</param>
        public BetSizeSet(params double[] potFractions)
        {
            if (potFractions == null)
            {
                throw new ArgumentNullException(nameof(potFractions));
            }

            if (potFractions.Any(f => f <= 0))
            {
                throw new ArgumentException("All bet size fractions must be greater than zero.", nameof(potFractions));
            }

            _fractions = potFractions.OrderBy(f => f).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the default set of bet sizes.
        /// </summary>
        public static BetSizeSet Default { get; } = new BetSizeSet(0.33, 0.5, 0.75, 1.0, 1.5);

        /// <summary>
        /// Gets the fractions representing bet sizes as pot fractions.
        /// </summary>
        public IReadOnlyList<double> Fractions => _fractions;

        /// <summary>
        /// Calculates the chip amount for a given pot fraction.
        /// </summary>
        /// <param name="fraction">The pot fraction.</param>
        /// <param name="pot">The current pot size.</param>
        /// <param name="stack">The player's stack size.</param>
        /// <returns>The chip amount, clamped to [1, stack].</returns>
        public int ChipAmount(double fraction, int pot, int stack)
        {
            var amount = (int)Math.Round(fraction * pot);
            return System.Math.Clamp(amount, 1, stack);
        }

        /// <summary>
        /// Gets all chip amounts for the bet sizes plus an all-in option.
        /// </summary>
        /// <param name="pot">The current pot size.</param>
        /// <param name="stack">The player's stack size.</param>
        /// <returns>A list of chip amounts including the all-in option.</returns>
        public IReadOnlyList<int> ChipAmounts(int pot, int stack)
        {
            var amounts = _fractions.Select(f => ChipAmount(f, pot, stack)).ToList();
            amounts.Add(stack); // Add all-in option
            return amounts.AsReadOnly();
        }
    }
}
