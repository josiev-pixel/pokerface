using Xunit;
using PokerEngine.Profiling;

namespace PokerEngine.Tests
{
    public class DecayingFrequencyStatTests
    {
        [Fact]
        public void NoObservations_PosteriorMeanEqualsPriorMean_AndConfidenceIsZero()
        {
            // Arrange
            var stat = new DecayingFrequencyStat(10, 0.5, 0.9);
            
            // Act & Assert
            Assert.Equal(stat.PriorMean, stat.PosteriorMean, precision: 10);
            Assert.Equal(0.0, stat.Confidence(20), precision: 10);
        }

        [Fact]
        public void WithDecayOne_ObservationsWorkLikeFrequencyStat()
        {
            // Arrange
            var stat = new DecayingFrequencyStat(10, 0.5, 1.0);
            
            // Act
            for (int i = 0; i < 5; i++)
                stat.Observe(true);
            for (int i = 0; i < 5; i++)
                stat.Observe(false);
            
            // Assert
            Assert.Equal(10, stat.DecayedOpportunities, precision: 9);  // Should be exactly 10
            Assert.Equal(5, stat.DecayedOccurrences, precision: 9);   // Should be exactly 5
            Assert.Equal(stat.PosteriorMean, (stat.Alpha0 + 5) / (stat.Alpha0 + stat.Beta0 + 10), precision: 10);
        }

        [Fact]
        public void Recency_RecentObservationsDominant()
        {
            // The same observation stream (20 falses then one true), under decay 0.5 vs no decay.
            var decayed = new DecayingFrequencyStat(2, 0.5, 0.5);
            var longMemory = new DecayingFrequencyStat(2, 0.5, 1.0); // decay 1.0 => never fades

            for (int i = 0; i < 20; i++)
            {
                decayed.Observe(false);
                longMemory.Observe(false);
            }
            decayed.Observe(true);
            longMemory.Observe(true);

            // Under decay the faded falses barely count, so the recent true pulls the estimate far
            // higher than the long-memory version (~0.5 vs ~0.09).
            Assert.True(decayed.PosteriorMean > longMemory.PosteriorMean + 0.2,
                $"recency should weight the recent true more: decayed {decayed.PosteriorMean} vs long-memory {longMemory.PosteriorMean}");
        }

        [Fact]
        public void BoundedEffectiveSample()
        {
            // Arrange
            var stat = new DecayingFrequencyStat(1, 1.0, 0.5);
            
            // Act - observe many trues
            for (int i = 0; i < 100; i++)
                stat.Observe(true);
            
            // Assert - effective sample size should be bounded near 2 (since 1/(1-0.5) = 2)
            Assert.True(stat.DecayedOpportunities < 2.01, "Effective sample size should be bounded");
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5)]
        public void ConstructorRejectsInvalidDecay(double invalidDecay)
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecayingFrequencyStat(1, 0.5, invalidDecay));
        }
    }
}
