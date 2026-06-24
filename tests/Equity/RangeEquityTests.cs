using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Equity;
using Xunit;

namespace PokerEngine.Tests;

public class RangeBuilderTests
{
    [Theory]
    [InlineData("AA", 6)]
    [InlineData("AKs", 4)]
    [InlineData("AKo", 12)]
    public void Expand_HasTheRightComboCount(string hand, int expected) =>
        Assert.Equal(expected, RangeBuilder.Expand(hand).Count);

    [Fact]
    public void Expand_ProducesDistinctTwoCardHands()
    {
        foreach (var h in RangeBuilder.Expand("AKo"))
            Assert.NotEqual(h.High, h.Low);
    }

    [Fact]
    public void Suited_ShareSuit_Offsuit_DiffSuit()
    {
        Assert.All(RangeBuilder.Expand("AKs"), h => Assert.Equal(h.High.Suit, h.Low.Suit));
        Assert.All(RangeBuilder.Expand("AKo"), h => Assert.NotEqual(h.High.Suit, h.Low.Suit));
    }
}

public class RangeEquityTests
{
    private static IReadOnlyList<HoleCards> R(params string[] hands) => hands.Select(HoleCards.Parse).ToList();
    private static readonly IReadOnlyList<Card> NoBoard = Array.Empty<Card>();

    [Fact]
    public void SingleMatchup_MatchesHandVsHand()
    {
        var r = RangeEquity.HeadsUp(R("AhAs"), R("KhKs"), NoBoard, new DeterministicRandom(1), 20_000);
        Assert.InRange(r.Equity, 0.79, 0.85); // AA vs KK ~82%
    }

    [Fact]
    public void MirrorRange_IsAboutEven_AfterCardRemoval()
    {
        // AA vs AA: only the non-conflicting (disjoint-suit) matchups survive; a mirror match is ~50%.
        var aces = RangeBuilder.Expand("AA");
        var r = RangeEquity.HeadsUp(aces, aces, NoBoard, new DeterministicRandom(1), 4_000);
        Assert.InRange(r.Equity, 0.45, 0.55);
        Assert.True(r.Samples > 0, "some disjoint AA-vs-AA matchups must remain");
    }

    [Fact]
    public void SameSeed_IsDeterministic()
    {
        var a = RangeEquity.HeadsUp(R("AhKh"), R("QdQc"), NoBoard, new DeterministicRandom(7), 3_000);
        var b = RangeEquity.HeadsUp(R("AhKh"), R("QdQc"), NoBoard, new DeterministicRandom(7), 3_000);
        Assert.Equal(a.Win, b.Win);
        Assert.Equal(a.Tie, b.Tie);
        Assert.Equal(a.Loss, b.Loss);
    }

    [Fact]
    public void NoValidMatchups_Throws()
    {
        // Hero AhAs vs villain AhKs — they share the Ah, so the only matchup conflicts.
        Assert.Throws<InvalidOperationException>(() =>
            RangeEquity.HeadsUp(R("AhAs"), R("AhKs"), NoBoard, new DeterministicRandom(1)));
    }

    [Fact]
    public void StartingHandOverload_ExpandsAndAverages()
    {
        var r = RangeEquity.HeadsUp(
            new[] { StartingHand.Parse("AKs") },
            new[] { StartingHand.Parse("QQ") },
            NoBoard, new DeterministicRandom(2), 2_000);
        Assert.InRange(r.Equity, 0.40, 0.55); // AKs vs QQ is a near coinflip (~46%)
    }
}
