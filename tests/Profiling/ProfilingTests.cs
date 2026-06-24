using System;
using System.Linq;
using PokerEngine.Core.Game;
using PokerEngine.Profiling;
using Xunit;

namespace PokerEngine.Tests;

public class FrequencyStatTests
{
    [Fact]
    public void NoObservations_SitsAtPriorMean_WithZeroConfidence()
    {
        var stat = new FrequencyStat(10, 0.5);
        Assert.Equal(0.5, stat.PosteriorMean, 9);
        Assert.Equal(0.0, stat.Confidence(20.0), 9);
    }

    [Fact]
    public void ManyTrueObservations_MovePosteriorTowardOne()
    {
        var stat = new FrequencyStat(10, 0.5); // alpha0=5, beta0=5
        for (int i = 0; i < 30; i++) stat.Observe(true);
        // (5 + 30) / (10 + 30) = 0.875
        Assert.True(stat.PosteriorMean > 0.75 && stat.PosteriorMean < 1.0, $"posterior was {stat.PosteriorMean}");
    }

    [Fact]
    public void Confidence_RisesWithSampleSize()
    {
        var few = new FrequencyStat(10, 0.5);
        for (int i = 0; i < 5; i++) few.Observe(true);
        var many = new FrequencyStat(10, 0.5);
        for (int i = 0; i < 30; i++) many.Observe(true);
        Assert.True(many.Confidence() > few.Confidence(), $"{many.Confidence()} should exceed {few.Confidence()}");
    }

    [Fact]
    public void ThinSample_StaysNearPrior()
    {
        var stat = new FrequencyStat(12, 0.45);
        stat.Observe(true);
        stat.Observe(true);
        // (5.4 + 2) / (12 + 2) = 0.529 -- the prior still dominates.
        Assert.True(stat.PosteriorMean < 0.6, $"posterior was {stat.PosteriorMean}");
    }
}

public class OpponentProfileTests
{
    [Fact]
    public void OverFolder_IsDetectedAsAFoldToBetLeak()
    {
        var p = new OpponentProfile("villain");
        for (int i = 0; i < 40; i++) p.RecordFacingBet(Street.Flop, folded: true);

        Assert.True(p.FoldToBetMean(Street.Flop) > 0.6, $"fold-to-bet was {p.FoldToBetMean(Street.Flop)}");
        Assert.Contains(p.DetectLeaks(), leak => leak.Name == "FoldToBet:Flop");
    }

    [Fact]
    public void ThinSample_ProducesNoLeaks()
    {
        var p = new OpponentProfile("villain");
        p.RecordPreflop(voluntarilyPutMoneyIn: true, raisedPreflop: true);
        p.RecordPreflop(voluntarilyPutMoneyIn: false, raisedPreflop: false);
        Assert.Empty(p.DetectLeaks()); // confidence too low to flag anything
    }

    [Theory]
    [InlineData(7, 3, 0.7)]
    [InlineData(0, 0, 0.0)]
    public void AggressionFrequency_IsAggressiveOverTotal(int aggressive, int passive, double expected)
    {
        var p = new OpponentProfile("villain");
        for (int i = 0; i < aggressive; i++) p.RecordAggressiveAction();
        for (int i = 0; i < passive; i++) p.RecordPassiveAction();
        Assert.Equal(expected, p.AggressionFrequency, 9);
    }

    [Fact]
    public void RecordFacingBet_RejectsPreflop() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new OpponentProfile("v").RecordFacingBet(Street.Preflop, true));
}
