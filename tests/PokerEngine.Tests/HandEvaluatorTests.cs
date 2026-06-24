using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Eval;
using Xunit;

namespace PokerEngine.Tests;

public class HandEvaluatorTests
{
    private static HandValue Eval(string cards) =>
        HandEvaluator.Evaluate(cards.Split(' ').Select(Card.Parse).ToList());

    [Theory]
    [InlineData("As Ks Qs Js Ts", HandRank.StraightFlush)]
    [InlineData("9h 8h 7h 6h 5h", HandRank.StraightFlush)]
    [InlineData("Ah 2h 3h 4h 5h", HandRank.StraightFlush)] // steel wheel
    [InlineData("Ac Ad Ah As Kc", HandRank.FourOfAKind)]
    [InlineData("Kc Kd Kh 2c 2d", HandRank.FullHouse)]
    [InlineData("Ac Tc 8c 5c 3c", HandRank.Flush)]
    [InlineData("9c 8d 7h 6s 5c", HandRank.Straight)]
    [InlineData("Ac 2d 3h 4s 5c", HandRank.Straight)] // wheel
    [InlineData("Qc Qd Qh 9s 4c", HandRank.ThreeOfAKind)]
    [InlineData("Jc Jd 4h 4s 9c", HandRank.TwoPair)]
    [InlineData("8c 8d Ah 5s 2c", HandRank.Pair)]
    [InlineData("Ac Kd 9h 5s 2c", HandRank.HighCard)]
    public void Classifies_FiveCardCategories(string cards, HandRank expected)
    {
        Assert.Equal(expected, Eval(cards).Rank);
    }

    [Fact]
    public void CategoryOrdering_IsStrict()
    {
        // One representative of each category, ascending.
        var hands = new[]
        {
            "Ac Kd 9h 5s 2c", // high card
            "8c 8d Ah 5s 2c", // pair
            "Jc Jd 4h 4s 9c", // two pair
            "Qc Qd Qh 9s 4c", // trips
            "9c 8d 7h 6s 5c", // straight
            "Ac Tc 8c 5c 3c", // flush
            "Kc Kd Kh 2c 2d", // full house
            "Ac Ad Ah As Kc", // quads
            "As Ks Qs Js Ts", // straight flush
        }.Select(Eval).ToList();

        for (int i = 1; i < hands.Count; i++)
            Assert.True(hands[i] > hands[i - 1], $"hand {i} should beat hand {i - 1}");
    }

    [Fact]
    public void Wheel_IsFiveHigh_BelowSixHighStraight()
    {
        Assert.True(Eval("6c 5d 4h 3s 2c") > Eval("Ac 2d 3h 4s 5c"));
    }

    [Fact]
    public void RoyalFlush_BeatsSteelWheel()
    {
        Assert.True(Eval("As Ks Qs Js Ts") > Eval("Ah 2h 3h 4h 5h"));
    }

    [Fact]
    public void Kickers_BreakTies()
    {
        Assert.True(Eval("As Ad Kh Qs Jc") > Eval("As Ad Kh Qs 9c")); // pair of aces, J vs 9 kicker
        Assert.True(Eval("Ks Kd Qh Qs Ac") > Eval("Ks Kd Qh Qs Jc")); // two pair, A vs J kicker
    }

    [Fact]
    public void IdenticalRanks_DifferentSuits_Tie()
    {
        Assert.Equal(Eval("Ac Kc Qd Js 9h"), Eval("Ad Kh Qs Jc 9d"));
    }

    [Fact]
    public void SevenCards_PicksBestFive()
    {
        // Board + hole; best is a flush, not the lower straight available.
        var v = Eval("Ah Kh Qh Jh 9h 8c 7d");
        Assert.Equal(HandRank.Flush, v.Rank);
    }

    [Fact]
    public void SevenCards_FindsSeventHCardStraightFlush()
    {
        var v = Eval("2h 3h 4h 5h 6h Ah Kc");
        Assert.Equal(HandRank.StraightFlush, v.Rank);
    }

    /// <summary>
    /// The decisive correctness test: classify every one of the C(52,5)=2,598,960 distinct
    /// five-card hands and assert each category's count matches the known combinatorics.
    /// If any boundary (wheel, flush-vs-straight, kicker packing) is wrong, a count shifts.
    /// </summary>
    [Fact]
    public void FullDeckCensus_MatchesKnownCombinatorics()
    {
        var counts = new Dictionary<HandRank, int>();
        foreach (HandRank r in System.Enum.GetValues<HandRank>()) counts[r] = 0;

        var deck = Enumerable.Range(0, 52).Select(Card.FromIndex).ToArray();
        for (int a = 0; a < 52; a++)
        for (int b = a + 1; b < 52; b++)
        for (int c = b + 1; c < 52; c++)
        for (int d = c + 1; d < 52; d++)
        for (int e = d + 1; e < 52; e++)
            counts[HandEvaluator.EvaluateFive(deck[a], deck[b], deck[c], deck[d], deck[e]).Rank]++;

        Assert.Equal(40, counts[HandRank.StraightFlush]);
        Assert.Equal(624, counts[HandRank.FourOfAKind]);
        Assert.Equal(3744, counts[HandRank.FullHouse]);
        Assert.Equal(5108, counts[HandRank.Flush]);
        Assert.Equal(10200, counts[HandRank.Straight]);
        Assert.Equal(54912, counts[HandRank.ThreeOfAKind]);
        Assert.Equal(123552, counts[HandRank.TwoPair]);
        Assert.Equal(1098240, counts[HandRank.Pair]);
        Assert.Equal(1302540, counts[HandRank.HighCard]);
        Assert.Equal(2598960, counts.Values.Sum());
    }
}
