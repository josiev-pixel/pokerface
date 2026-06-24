using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Abstraction;
using Xunit;

namespace PokerEngine.Tests;

public class EquityBucketerTests
{
    private static IReadOnlyList<Card> H(string a, string b) => new[] { Card.Parse(a), Card.Parse(b) };
    private static readonly IReadOnlyList<Card> NoBoard = Array.Empty<Card>();

    [Fact]
    public void StrongHand_BucketsHigherThanTrash()
    {
        var b = new EquityBucketer(buckets: 10);
        int aces = b.Bucket(H("Ah", "As"), NoBoard);
        int trash = b.Bucket(H("7h", "2c"), NoBoard);
        Assert.True(aces >= 8, $"AA preflop should land near the top bucket, got {aces}");
        Assert.True(aces > trash, $"AA ({aces}) should out-bucket 72o ({trash})");
    }

    [Fact]
    public void Bucket_IsDeterministic()
    {
        var b = new EquityBucketer(buckets: 10);
        Assert.Equal(b.Bucket(H("Ah", "Kh"), NoBoard), b.Bucket(H("Ah", "Kh"), NoBoard));
    }

    [Fact]
    public void Bucket_StaysInRange()
    {
        var b = new EquityBucketer(buckets: 8);
        foreach (var hand in new[] { H("Ah", "As"), H("7h", "2c"), H("Td", "9d") })
            Assert.InRange(b.Bucket(hand, NoBoard), 0, b.BucketCount - 1);
    }

    [Fact]
    public void RejectsWrongHoleCardCount() =>
        Assert.Throws<ArgumentException>(() => new EquityBucketer(4).Bucket(new[] { Card.Parse("Ah") }, NoBoard));
}

public class BetSizeSetTests
{
    [Fact]
    public void Default_IsAscending()
    {
        var f = BetSizeSet.Default.Fractions;
        for (int i = 1; i < f.Count; i++) Assert.True(f[i] > f[i - 1]);
    }

    [Theory]
    [InlineData(1.0, 100, 1000, 100)]
    [InlineData(0.5, 100, 1000, 50)]
    [InlineData(2.0, 100, 40, 40)] // clamped to the stack
    public void ChipAmount_RoundsAndClamps(double fraction, int pot, int stack, int expected) =>
        Assert.Equal(expected, BetSizeSet.Default.ChipAmount(fraction, pot, stack));

    [Fact]
    public void ChipAmounts_IncludeAllIn() =>
        Assert.Contains(1000, BetSizeSet.Default.ChipAmounts(100, 1000));

    [Fact]
    public void RejectsNonPositiveFraction() =>
        Assert.Throws<ArgumentException>(() => new BetSizeSet(0.5, 0.0, 1.0));
}

public class BetSizeTranslatorTests
{
    private static readonly BetSizeTranslator T = new(BetSizeSet.Default);

    [Theory]
    [InlineData(0.6, 0.5)]   // 0.5 is nearer (0.10) than 0.75 (0.15)
    [InlineData(0.8, 0.75)]  // 0.75 nearer (0.05) than 1.0 (0.20)
    [InlineData(10.0, 1.5)]  // clamps to the top peg
    public void NearestFraction_PicksClosestPeg(double x, double expected) =>
        Assert.Equal(expected, T.NearestFraction(x));

    [Fact]
    public void PseudoHarmonic_HitsEndpointsAndStaysBetween()
    {
        Assert.Equal(0.0, BetSizeTranslator.PseudoHarmonicProbability(0.5, 0.5, 1.0));
        Assert.Equal(1.0, BetSizeTranslator.PseudoHarmonicProbability(1.0, 0.5, 1.0));
        double mid = BetSizeTranslator.PseudoHarmonicProbability(0.75, 0.5, 1.0);
        Assert.True(mid > 0.0 && mid < 1.0, $"midpoint prob {mid} should be strictly between 0 and 1");
    }

    [Fact]
    public void PseudoHarmonic_IsMonotonicInX()
    {
        double lo = BetSizeTranslator.PseudoHarmonicProbability(0.6, 0.5, 1.0);
        double hi = BetSizeTranslator.PseudoHarmonicProbability(0.9, 0.5, 1.0);
        Assert.True(hi > lo, $"prob of the larger peg should rise with x: {lo} -> {hi}");
    }

    [Fact]
    public void TranslatePseudoHarmonic_ReturnsABracketingPeg_Deterministically()
    {
        double first = T.TranslatePseudoHarmonic(0.6, new DeterministicRandom(1));
        Assert.Contains(first, new[] { 0.5, 0.75 });
        Assert.Equal(first, T.TranslatePseudoHarmonic(0.6, new DeterministicRandom(1)));
    }
}
