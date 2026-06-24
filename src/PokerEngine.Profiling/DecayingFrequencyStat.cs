using System;

namespace PokerEngine.Profiling
{
    /// <summary>
    /// A Beta-posterior estimate of a 0/1 behavior frequency where each new observation scales the accumulated evidence by a decay factor first,
    /// so older observations fade.
    /// </summary>
    public sealed class DecayingFrequencyStat
    {
        private readonly double _alpha0;
        private readonly double _beta0;
        private readonly double _decay;
        private double _decayedOccurrences;
        private double _decayedOpportunities;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecayingFrequencyStat"/> class.
        /// </summary>
        /// <param name="priorStrength">The strength of the prior belief (must be > 0).</param>
        /// <param name="priorMean">The mean of the prior distribution (must be between 0 and 1).</param>
        /// <param name="decay">The decay factor for observations (must be between 0 and 1, exclusive of 0).</param>
        public DecayingFrequencyStat(double priorStrength, double priorMean, double decay)
        {
            if (priorStrength <= 0)
                throw new ArgumentOutOfRangeException(nameof(priorStrength), "Prior strength must be greater than zero.");
            
            if (priorMean < 0 || priorMean > 1)
                throw new ArgumentOutOfRangeException(nameof(priorMean), "Prior mean must be between 0 and 1.");

            if (decay <= 0 || decay > 1)
                throw new ArgumentOutOfRangeException(nameof(decay), "Decay must be greater than zero and less than or equal to one.");

            _alpha0 = priorStrength * priorMean;
            _beta0 = priorStrength * (1 - priorMean);
            _decay = decay;
            _decayedOccurrences = 0;
            _decayedOpportunities = 0;
        }

        /// <summary>
        /// Records an observation of the behavior.
        /// </summary>
        /// <param name="occurred">Whether the behavior occurred.</param>
        public void Observe(bool occurred)
        {
            _decayedOccurrences *= _decay;
            _decayedOpportunities *= _decay;
            _decayedOpportunities += 1;
            if (occurred)
                _decayedOccurrences += 1;
        }

        /// <summary>
        /// Gets the number of decayed opportunities observed.
        /// </summary>
        public double DecayedOpportunities => _decayedOpportunities;

        /// <summary>
        /// Gets the number of decayed occurrences observed.
        /// </summary>
        public double DecayedOccurrences => _decayedOccurrences;

        /// <summary>
        /// Gets the posterior mean frequency estimate based on decayed observations.
        /// </summary>
        public double PosteriorMean => (_alpha0 + _decayedOccurrences) / (_alpha0 + _beta0 + _decayedOpportunities);

        /// <summary>
        /// Gets the confidence level based on the number of decayed observations.
        /// </summary>
        /// <param name="halfSampleCount">The half-sample count for confidence calculation (must be > 0).</param>
        /// <returns>The confidence level between 0 and 1.</returns>
        public double Confidence(double halfSampleCount = 20.0)
        {
            if (halfSampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(halfSampleCount), "Half sample count must be greater than zero.");

            return _decayedOpportunities / (_decayedOpportunities + halfSampleCount);
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
        /// Gets the decay factor used for observations.
        /// </summary>
        public double Decay => _decay;

        /// <summary>
        /// Gets the prior mean used in the calculation.
        /// </summary>
        public double PriorMean => _alpha0 / (_alpha0 + _beta0);
    }
}
