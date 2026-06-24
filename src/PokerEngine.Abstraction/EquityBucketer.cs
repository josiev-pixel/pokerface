using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Equity;

namespace PokerEngine.Abstraction
{
    /// <summary>
    /// A card abstraction that buckets hands based on equity against a random opponent.
    /// </summary>
    public sealed class EquityBucketer : ICardAbstraction
    {
        private readonly int _buckets;
        private readonly int _samples;
        private readonly ulong _seed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EquityBucketer"/> class.
        /// </summary>
        /// <param name="buckets">The number of buckets to use.</param>
        /// <param name="samples">The number of samples to use for equity calculation.</param>
        /// <param name="seed">The seed for the random number generator.</param>
        public EquityBucketer(int buckets, int samples = 2000, ulong seed = 1)
        {
            if (buckets < 2)
            {
                throw new ArgumentException("Number of buckets must be at least 2.", nameof(buckets));
            }

            if (samples < 1)
            {
                throw new ArgumentException("Number of samples must be at least 1.", nameof(samples));
            }

            _buckets = buckets;
            _samples = samples;
            _seed = seed;
        }

        /// <summary>
        /// Gets the number of buckets in this abstraction.
        /// </summary>
        public int BucketCount => _buckets;

        /// <summary>
        /// Maps hole cards and board to a bucket index based on equity against random opponent.
        /// </summary>
        /// <param name="hole">The hole cards.</param>
        /// <param name="board">The community cards.</param>
        /// <returns>The bucket index (0 to BucketCount-1).</returns>
        public int Bucket(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
        {
            if (hole.Count != 2)
            {
                throw new ArgumentException("Hole cards must contain exactly 2 cards.", nameof(hole));
            }

            // Calculate equity using the deterministic random number generator
            var rng = new DeterministicRandom(_seed);
            var equityResult = EquityCalculator.HeadsUpVsRandom(hole, board, rng, _samples);
            var equity = equityResult.Equity;

            // Map equity to bucket index
            var bucketIndex = (int)Math.Floor(equity * _buckets);
            return Math.Clamp(bucketIndex, 0, _buckets - 1);
        }
    }
}
