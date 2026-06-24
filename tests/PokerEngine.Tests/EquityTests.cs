using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Equity;
using Xunit;

namespace PokerEngine.Tests;

public class EquityTests
{
    private static System.Collections.Generic.List<Card> C(string s) =>
        string.IsNullOrWhiteSpace(s)
            ? new System.Collections.Generic.List<Card>()
            : s.Split(' ').Select(Card.Parse).ToList();

    [Fact]
    public void AcesVsKings_Preflop_IsAboutEightyTwoPercent()
    {
        var r = EquityCalculator.HeadsUp(C("Ah As"), C("Kh Ks"), C(""), new DeterministicRandom(1), 100_000);
        Assert.InRange(r.Equity, 0.79, 0.85);
        Assert.False(r.IsExact); // preflop runout space is sampled
    }

    [Fact]
    public void BigSlickVsLowPair_IsRoughlyACoinflip()
    {
        // AKs vs 22: the classic race, ~50/50.
        var r = EquityCalculator.HeadsUp(C("Ah Kh"), C("2c 2d"), C(""), new DeterministicRandom(2), 100_000);
        Assert.InRange(r.Equity, 0.44, 0.55);
    }

    [Fact]
    public void Domination_AkBeatsAq()
    {
        // AK vs AQ (offsuit, no shared suits): AK wins ~73%.
        var r = EquityCalculator.HeadsUp(C("As Kh"), C("Ac Qd"), C(""), new DeterministicRandom(3), 100_000);
        Assert.InRange(r.Equity, 0.69, 0.77);
    }

    [Fact]
    public void CompletedBoard_IsExactAndDeterministic()
    {
        // Full board: hero has the nut flush, villain has top two pair. Hero is 100%.
        var hero = C("Ah Qh");
        var villain = C("Ks Kd");
        var board = C("Kh 7h 2h 9s 3d"); // three hearts + Ah Qh = ace-high flush; villain has trip kings
        // Wait: villain Ks Kd + Kh on board = trips; hero has A-high flush which beats trips.
        var r = EquityCalculator.HeadsUp(hero, villain, board, new DeterministicRandom(4));
        Assert.True(r.IsExact);
        Assert.Equal(1, r.Samples);
        Assert.Equal(1.0, r.Equity, 6);
    }

    [Fact]
    public void Turn_EnumeratesExactly()
    {
        var r = EquityCalculator.HeadsUp(C("Ah Kh"), C("Qs Qd"), C("Qh 7c 2d 3s"), new DeterministicRandom(5));
        Assert.True(r.IsExact);
        // 52 - 2 - 2 - 4 = 44 remaining river cards.
        Assert.Equal(44, r.Samples);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalResult()
    {
        var a = EquityCalculator.HeadsUp(C("Ah As"), C("Kh Ks"), C(""), new DeterministicRandom(42), 20_000);
        var b = EquityCalculator.HeadsUp(C("Ah As"), C("Kh Ks"), C(""), new DeterministicRandom(42), 20_000);
        Assert.Equal(a.Win, b.Win);
        Assert.Equal(a.Tie, b.Tie);
        Assert.Equal(a.Loss, b.Loss);
    }

    [Fact]
    public void WinTieLoss_SumToOne()
    {
        var r = EquityCalculator.HeadsUp(C("Ah Kh"), C("Ad Kd"), C(""), new DeterministicRandom(6), 20_000);
        Assert.Equal(1.0, r.Win + r.Tie + r.Loss, 6);
        Assert.True(r.Tie > 0); // AKs vs AKs chops very often
    }

    [Theory]
    [InlineData(5, 0, 1)]
    [InlineData(44, 1, 44)]
    [InlineData(45, 2, 990)]
    [InlineData(52, 5, 2598960)]
    public void Combinations_AreCorrect(int n, int k, long expected)
    {
        Assert.Equal(expected, EquityCalculator.Combinations(n, k));
    }
}
