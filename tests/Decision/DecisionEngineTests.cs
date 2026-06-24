using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Decision;
using Xunit;

namespace PokerEngine.Tests;

public class DecisionEngineTests
{
    private static readonly DecisionEngine Engine = new();

    private static IReadOnlyList<Card> Board(string s) =>
        string.IsNullOrWhiteSpace(s) ? new List<Card>() : s.Split(' ').Select(Card.Parse).ToList();

    private static Spot Spot(string hero, string board, Position pos, int pot, int toCall, int eff, int bb = 10) =>
        new()
        {
            Hero = HoleCards.Parse(hero),
            Board = Board(board),
            Position = pos,
            Pot = pot,
            ToCall = toCall,
            EffectiveStack = eff,
            BigBlind = bb,
        };

    private static double ProbOf(DecisionResult r, Move m) =>
        r.Strategy.Where(o => o.Move == m).Sum(o => o.Probability);

    // ---- Preflop: short-stack push/fold (SAGE) ----

    [Fact]
    public void ShortStack_Aces_ShoveAllIn()
    {
        // 8 BB effective, SB first-in (just the blind to complete). Aces always jam.
        var spot = Spot("AhAs", "", Position.InPosition, pot: 15, toCall: 5, eff: 80);
        var r = Engine.Decide(spot, new DeterministicRandom(1));
        Assert.Equal(Move.Bet, r.Move);
        Assert.Equal(80, r.Chips); // all-in for the effective stack
    }

    [Fact]
    public void ShortStack_SevenTwo_Folds()
    {
        var spot = Spot("7h2c", "", Position.InPosition, pot: 15, toCall: 5, eff: 80);
        var r = Engine.Decide(spot, new DeterministicRandom(1));
        Assert.Equal(Move.Fold, r.Move);
    }

    // ---- Preflop: deeper stacks (Chen) ----

    [Fact]
    public void DeepStack_StrongHand_OpensRaise()
    {
        var spot = Spot("AhKs", "", Position.InPosition, pot: 15, toCall: 5, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(1));
        Assert.Equal(Move.Bet, r.Move);
        Assert.True(r.Chips > 0);
    }

    [Fact]
    public void DeepStack_Trash_Folds()
    {
        var spot = Spot("7h2c", "", Position.InPosition, pot: 15, toCall: 5, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(1));
        Assert.Equal(Move.Fold, r.Move);
    }

    // ---- Postflop ----

    [Fact]
    public void Postflop_TopSet_BetsForValue()
    {
        // Hero flops top set; first to act on a dry board → mostly value bets.
        var spot = Spot("AcAd", "Ah 7s 2c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(7));
        Assert.True(r.Equity > 0.62, $"equity was {r.Equity}");
        Assert.True(ProbOf(r, Move.Bet) >= 0.85);
    }

    [Fact]
    public void Postflop_AirFacingPotBet_Folds()
    {
        // Hero has air on a big board facing a pot-sized bet; below the pot-odds price → fold.
        var spot = Spot("7h2c", "Ah Ks Qd", Position.OutOfPosition, pot: 200, toCall: 100, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(7));
        Assert.True(r.Equity < r.RequiredEquity, $"equity {r.Equity} vs required {r.RequiredEquity}");
        Assert.Equal(Move.Fold, r.Move);
    }

    [Fact]
    public void Strategy_ProbabilitiesSumToOne()
    {
        var spot = Spot("Th9h", "Ah 7s 2c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(3));
        Assert.Equal(1.0, r.Strategy.Sum(o => o.Probability), 6);
    }

    [Fact]
    public void SameSeed_IsDeterministic()
    {
        var spot = Spot("Th9h", "Ah 7s 2c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);
        var a = Engine.Decide(spot, new DeterministicRandom(99));
        var b = Engine.Decide(spot, new DeterministicRandom(99));
        Assert.Equal(a.Move, b.Move);
        Assert.Equal(a.Chips, b.Chips);
        Assert.Equal(a.Equity, b.Equity);
    }

    // ---- Bounded exploitation ----

    [Fact]
    public void NoModel_PlaysBaseline_ExploitWeightZero()
    {
        var spot = Spot("7h2c", "Ah Ks Qd 9c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(5));
        Assert.Equal(0.0, r.ExploitWeight);
    }

    [Fact]
    public void OverFoldingOpponent_BluffsMoreThanBaseline()
    {
        // Same weak hand + same seed; the only difference is the opponent read.
        var spot = Spot("7h2c", "Ah Ks Qd 9c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);

        var baseline = Engine.Decide(spot, new DeterministicRandom(5), OpponentModel.Unknown);
        var vsNit = Engine.Decide(spot, new DeterministicRandom(5),
            new OpponentModel { FoldToBet = 0.9, Confidence = 1.0 });

        Assert.True(baseline.Equity <= 0.38, $"need a bluff candidate; equity {baseline.Equity}");
        Assert.True(ProbOf(vsNit, Move.Bet) > ProbOf(baseline, Move.Bet),
            $"expected more bluffing vs a nit: {ProbOf(vsNit, Move.Bet)} vs {ProbOf(baseline, Move.Bet)}");
    }

    [Fact]
    public void ExploitWeight_IsCappedAtWMax()
    {
        var spot = Spot("7h2c", "Ah Ks Qd 9c", Position.InPosition, pot: 100, toCall: 0, eff: 1000);
        var r = Engine.Decide(spot, new DeterministicRandom(5),
            new OpponentModel { FoldToBet = 0.9, Confidence = 1.0 }); // confidence 1.0
        Assert.Equal(0.5, r.ExploitWeight); // capped at EngineConfig.MaxExploitWeight
    }
}
