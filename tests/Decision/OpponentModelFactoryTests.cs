using System;
using Xunit;
using PokerEngine.Core.Game;
using PokerEngine.Decision;
using PokerEngine.Profiling;

namespace PokerEngine.Tests.Decision
{
    public class OpponentModelFactoryTests
    {
        [Fact]
        public void FromProfile_WithFreshProfile_ReturnsModelWithZeroConfidence()
        {
            // Arrange
            var profile = new OpponentProfile("test");

            // Act
            var result = OpponentModelFactory.FromProfile(profile, Street.Flop);

            // Assert
            Assert.Equal(0.0, result.Confidence);
            Assert.True(result.FoldToBet >= 0 && result.FoldToBet <= 1);
        }

        [Fact]
        public void FromProfile_WithManyFlopFolds_ReturnsHighFoldToBetAndPositiveConfidence()
        {
            // Arrange
            var profile = new OpponentProfile("test");
            
            // Record many flop folds (about 40 times)
            for (int i = 0; i < 40; i++)
            {
                profile.RecordFacingBet(Street.Flop, true);
            }

            // Act
            var result = OpponentModelFactory.FromProfile(profile, Street.Flop);

            // Assert
            Assert.True(result.FoldToBet > 0.6);
            Assert.True(result.Confidence > 0);
        }

        [Fact]
        public void FromProfile_WithPreflopStreet_UsesFoldToCbetFallback()
        {
            // Arrange
            var profile = new OpponentProfile("test");

            // Act
            var result = OpponentModelFactory.FromProfile(profile, Street.Preflop);

            // Assert
            Assert.True(result.FoldToBet >= 0 && result.FoldToBet <= 1);
        }

        [Fact]
        public void FromProfile_WithNullProfile_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => OpponentModelFactory.FromProfile(null, Street.Flop));
        }
    }
}
