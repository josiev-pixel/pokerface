using System;
using PokerEngine.Core;

namespace PokerEngine.Abstraction
{
    /// <summary>
    /// Translates real bet sizes to abstract bet sizes using a pseudo-harmonic probability mapping.
    /// </summary>
    public sealed class BetSizeTranslator
    {
        private readonly BetSizeSet _betSizes;

        /// <summary>
        /// Initializes a new instance of the <see cref="BetSizeTranslator"/> class.
        /// </summary>
        /// <param name="betSizes">The set of bet sizes to use for translation.</param>
        public BetSizeTranslator(BetSizeSet betSizes)
        {
            _betSizes = betSizes ?? throw new ArgumentNullException(nameof(betSizes));
        }

        /// <summary>
        /// Finds the index of the nearest fraction to the given value.
        /// </summary>
        /// <param name="x">The value to find the nearest fraction for.</param>
        /// <returns>The index of the nearest fraction (lower index in case of ties).</returns>
        public int NearestIndex(double x)
        {
            if (x <= _betSizes.Fractions[0])
                return 0;

            if (x >= _betSizes.Fractions[_betSizes.Fractions.Count - 1])
                return _betSizes.Fractions.Count - 1;

            double minDiff = double.MaxValue;
            int nearestIndex = 0;

            for (int i = 0; i < _betSizes.Fractions.Count; i++)
            {
                double diff = System.Math.Abs(x - _betSizes.Fractions[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// Gets the nearest fraction to the given value.
        /// </summary>
        /// <param name="x">The value to find the nearest fraction for.</param>
        /// <returns>The nearest fraction.</returns>
        public double NearestFraction(double x)
        {
            return _betSizes.Fractions[NearestIndex(x)];
        }

        /// <summary>
        /// Computes the pseudo-harmonic probability for mapping between bet sizes.
        /// </summary>
        /// <param name="x">The input value.</param>
        /// <param name="a">The lower bound.</param>
        /// <param name="b">The upper bound.</param>
        /// <returns>The probability of choosing the larger size.</returns>
        public static double PseudoHarmonicProbability(double x, double a, double b)
        {
            if (a >= b)
                throw new ArgumentException("Parameter a must be less than parameter b.", nameof(a));

            if (x <= a)
                return 0.0;

            if (x >= b)
                return 1.0;

            var pSmaller = ((b - x) * (1.0 + a)) / ((b - a) * (1.0 + x));
            return 1.0 - pSmaller;
        }

        /// <summary>
        /// Translates a real bet size to an abstract bet size using pseudo-harmonic mapping.
        /// </summary>
        /// <param name="x">The real bet size as a pot fraction.</param>
        /// <param name="rng">The random number generator for stochastic selection.</param>
        /// <returns>The selected abstract bet size.</returns>
        public double TranslatePseudoHarmonic(double x, DeterministicRandom rng)
        {
            if (x <= _betSizes.Fractions[0])
                return _betSizes.Fractions[0];

            if (x >= _betSizes.Fractions[_betSizes.Fractions.Count - 1])
                return _betSizes.Fractions[_betSizes.Fractions.Count - 1];

            // Find the adjacent fractions
            int i = 0;
            while (i < _betSizes.Fractions.Count - 1 && x > _betSizes.Fractions[i + 1])
            {
                i++;
            }

            double a = _betSizes.Fractions[i];
            double b = _betSizes.Fractions[i + 1];

            // Compute probability of choosing larger size
            double pB = PseudoHarmonicProbability(x, a, b);

            // Return the appropriate fraction based on random selection
            return rng.NextDouble() < pB ? b : a;
        }
    }
}
