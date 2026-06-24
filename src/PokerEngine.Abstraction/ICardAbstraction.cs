using System;
using System.Collections.Generic;
using PokerEngine.Core;

namespace PokerEngine.Abstraction
{
    /// <summary>
    /// Represents a card abstraction that maps hole cards and board to discrete buckets.
    /// </summary>
    public interface ICardAbstraction
    {
        /// <summary>
        /// Gets the number of buckets in this abstraction.
        /// </summary>
        int BucketCount { get; }

        /// <summary>
        /// Maps hole cards and board to a bucket index.
        /// </summary>
        /// <param name="hole">The hole cards.</param>
        /// <param name="board">The community cards.</param>
        /// <returns>The bucket index (0 to BucketCount-1).</returns>
        int Bucket(IReadOnlyList<Card> hole, IReadOnlyList<Card> board);
    }
}
