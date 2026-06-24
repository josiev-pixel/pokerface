using PokerEngine.Core;
using Xunit;

namespace PokerEngine.Tests;

public class HoleCardsTests
{
    [Fact]
    public void OrdersHigherCardFirst_RegardlessOfInput()
    {
        var a = HoleCards.Parse("KhAs");
        var b = HoleCards.Parse("AsKh");
        Assert.Equal(a, b);
        Assert.Equal(Rank.Ace, a.High.Rank);
        Assert.Equal(Rank.King, a.Low.Rank);
    }

    [Fact]
    public void DetectsPairAndSuited()
    {
        Assert.True(HoleCards.Parse("AhAd").IsPair);
        Assert.True(HoleCards.Parse("AhKh").IsSuited);
        Assert.False(HoleCards.Parse("AhKd").IsSuited);
    }

    [Fact]
    public void RejectsDuplicateCard()
    {
        Assert.Throws<System.ArgumentException>(() => HoleCards.Parse("AhAh"));
    }
}

public class StartingHandTests
{
    [Theory]
    [InlineData("AsAh", "AA")]
    [InlineData("AsKs", "AKs")]
    [InlineData("KdAc", "AKo")]
    [InlineData("9h8h", "98s")]
    [InlineData("Td9c", "T9o")]
    public void CanonicalForm_IsCorrect(string holes, string expected)
    {
        Assert.Equal(expected, HoleCards.Parse(holes).ToStartingHand().ToString());
    }

    [Theory]
    [InlineData("AA", 6)]
    [InlineData("AKs", 4)]
    [InlineData("AKo", 12)]
    public void ComboCounts_MatchTheBucket(string hand, int combos)
    {
        Assert.Equal(combos, StartingHand.Parse(hand).Combos);
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        foreach (var s in new[] { "AA", "22", "AKs", "AKo", "72o", "T9s" })
            Assert.Equal(s, StartingHand.Parse(s).ToString());
    }

    [Fact]
    public void Parse_RejectsBadSuffixes()
    {
        Assert.Throws<System.FormatException>(() => StartingHand.Parse("AAs"));  // pair + suffix
        Assert.Throws<System.FormatException>(() => StartingHand.Parse("AK"));   // missing suffix
        Assert.Throws<System.FormatException>(() => StartingHand.Parse("AKx"));  // bad suffix
    }

    [Fact]
    public void TotalCombos_AcrossAll169_Is1326()
    {
        // Sanity: the 169 buckets must cover all C(52,2)=1326 specific two-card combos.
        int total = 0;
        var ranks = "23456789TJQKA";
        for (int i = 0; i < 13; i++)
            for (int j = 0; j < 13; j++)
            {
                if (i == j) { total += StartingHand.Parse($"{ranks[i]}{ranks[i]}").Combos; }
                else if (i > j) { total += StartingHand.Parse($"{ranks[i]}{ranks[j]}s").Combos
                                         + StartingHand.Parse($"{ranks[i]}{ranks[j]}o").Combos; }
            }
        Assert.Equal(1326, total);
    }
}
