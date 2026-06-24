using System;
using System.Diagnostics;

namespace PokerEngine.Profiling
{
    /// <summary>
    /// A Beta-posterior estimate of a 0/1 behavior frequency.
    /// </summary>
    public sealed class FrequencyStat
    {
        private readonly double _alpha0;
        private readonly double _beta0;
        private int _opportunities;
        private int _occurrences;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrequencyStat"/> class.
        /// </summary>
        /// <param name="priorStrength">The strength of the prior belief (must be > 0).</param>
        /// <param name="priorMean">The mean of the prior distribution (must be between 0 and 1).</param>
        public FrequencyStat(double priorStrength, double priorMean)
        {
            if (priorStrength <= 0)
                throw new ArgumentOutOfRangeException(nameof(priorStrength), "Prior strength must be greater than zero.");
            
            if (priorMean < 0 || priorMean > 1)
                throw new ArgumentOutOfRangeException(nameof(priorMean), "Prior mean must be between 0 and 1.");

            _alpha0 = priorStrength * priorMean;
            _beta0 = priorStrength * (1 - priorMean);
        }

        /// <summary>
        /// Records an observation of the behavior.
        /// </summary>
        /// <param name="occurred">Whether the behavior occurred.</param>
        public void Observe(bool occurred)
        {
            _opportunities++;
            if (occurred)
                _occurrences++;
        }

        /// <summary>
        /// Gets the number of opportunities observed.
        /// </summary>
        public int Opportunities => _opportunities;

        /// <summary>
        /// Gets the number of occurrences observed.
        /// </summary>
        public int Occurrences => _occurrences;

        /// <summary>
        /// Gets the posterior mean frequency estimate.
        /// </summary>
        public double PosteriorMean => (_alpha0 + _occurrences) / (_alpha0 + _beta0 + _opportunities);

        /// <summary>
        /// Gets the confidence level based on the number of observations.
        /// </summary>
        /// <param name="halfSampleCount">The half-sample count for confidence calculation (must be > 0).</param>
        /// <returns>The confidence level between 0 and 1.</returns>
        public double Confidence(double halfSampleCount = 20.0)
        {
            if (halfSampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(halfSampleCount), "Half sample count must be greater than zero.");

            return _opportunities / (_opportunities + halfSampleCount);
        }

        /// <summary>
        /// Gets the initial alpha parameter of the Beta distribution.
        /// </summary>
        public double Alpha0 => _alpha0;

        /// <summary>
        /// Gets the initial beta parameter of the Beta distribution.
        /// </summary>
        public double Beta0 => _beta0;

        /// <summary>
        /// Gets the prior mean used in the calculation.
        /// </summary>
        public double PriorMean => _alpha0 / (_alpha0 + _beta0);
    }
}
