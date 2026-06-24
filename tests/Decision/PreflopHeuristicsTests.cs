using PokerEngine.Core;
using PokerEngine.Decision;
using Xunit;

namespace PokerEngine.Tests;

public class PokerMathTests
{
    [Fact]
    public void RequiredEquity_HalfPotCall()
    {
        // $50 to call into a pot that already shows $100 (incl. the bet) → 50/150 = 33.3%.
        Assert.Equal(1.0 / 3.0, PokerMath.RequiredEquityToCall(100, 50), 6);
    }

    [Fact]
    public void RequiredEquity_NothingToCall_IsZero() =>
        Assert.Equal(0.0, PokerMath.RequiredEquityToCall(100, 0), 6);

    [Theory]
    [InlineData(100, 100, 0.5)]      // pot-sized bet → MDF 50%
    [InlineData(100, 50, 2.0 / 3.0)] // half-pot bet → MDF 66.7%
    public void Mdf_IsPotOverPotPlusBet(int pot, int bet, double expected) =>
        Assert.Equal(expected, PokerMath.MinimumDefenseFrequency(pot, bet), 6);

    [Theory]
    [InlineData(100, 100, 0.5)]       // pot-sized bet → alpha 50%
    [InlineData(100, 50, 1.0 / 3.0)]  // half-pot → alpha 33.3%
    public void Alpha_IsBetOverPotPlusBet(int pot, int bet, double expected) =>
        Assert.Equal(expected, PokerMath.Alpha(pot, bet), 6);

    [Fact]
    public void AlphaAndMdf_SumToOne() =>
        Assert.Equal(1.0, PokerMath.Alpha(100, 75) + PokerMath.MinimumDefenseFrequency(100, 75), 6);

    [Fact]
    public void BluffEv_BreaksEvenAtAlpha()
    {
        // A pure bluff betting 100 into 100 breaks even when fold% = alpha = 50%.
        Assert.Equal(0.0, PokerMath.BluffEv(100, 100, 0.5), 6);
    }
}

public class ChenFormulaTests
{
    [Theory]
    [InlineData("AA", 20)]
    [InlineData("KK", 16)]
    [InlineData("AKs", 12)]
    [InlineData("AKo", 10)]
    [InlineData("TT", 10)]
    [InlineData("JTs", 9)]
    [InlineData("A5o", 5)]
    [InlineData("22", 5)]
    [InlineData("72o", -1)]
    public void Score_MatchesReferenceValues(string hand, int expected) =>
        Assert.Equal(expected, ChenFormula.Score(StartingHand.Parse(hand)));
}

public class SageSystemTests
{
    [Theory]
    [InlineData("AA", 67)]   // max: 2*15 + 15 + 22
    [InlineData("JTs", 34)]  // 2*11 + 10 + 2
    [InlineData("32o", 8)]   // min: 2*3 + 2
    public void PowerIndex_MatchesReference(string hand, int expected) =>
        Assert.Equal(expected, SageSystem.PowerIndex(StartingHand.Parse(hand)));

    [Fact]
    public void ShortStack_PushesPremiumsFoldsTrash()
    {
        // At 5 BB the SB shoves if PI ≥ 24.
        Assert.True(SageSystem.ShouldPush(StartingHand.Parse("AJo"), 5));  // PI 41
        Assert.False(SageSystem.ShouldPush(StartingHand.Parse("72o"), 5)); // PI 16
    }

    [Fact]
    public void BigBlind_CallsTighterThanSbPushes()
    {
        // At 5 BB call threshold (28) is tighter than push threshold (24).
        Assert.Equal(24, SageSystem.PushThreshold(5));
        Assert.Equal(28, SageSystem.CallThreshold(5));
    }

    [Fact]
    public void AtOneBigBlind_BigBlindCallsAnyTwo() =>
        Assert.True(SageSystem.ShouldCall(StartingHand.Parse("72o"), 1));
}
