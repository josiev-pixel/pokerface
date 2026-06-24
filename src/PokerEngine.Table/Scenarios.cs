using PokerEngine.Core;
using PokerEngine.Decision;

namespace PokerEngine.Table;

/// <summary>
/// A named heads-up spot to view, plus the optional opponent read that turns on the
/// bounded-exploit layer. This is the exact set the CLI <c>demo</c> command ships, so the
/// viewer and the CLI agree on what the engine recommends.
/// </summary>
internal sealed record Scenario(string Title, Spot Spot, OpponentModel? Opponent = null);

internal static class Scenarios
{
    /// <summary>The six canned spots, in display order (matches CLI `demo`).</summary>
    public static IReadOnlyList<Scenario> All { get; } = new[]
    {
        new Scenario(
            "Short-stack jam: Aces in the SB at 8bb",
            MakeSpot("AhAs", "", Position.InPosition, pot: 15, toCall: 5, eff: 80, bb: 10)),

        new Scenario(
            "Short-stack fold: 72o in the SB at 8bb",
            MakeSpot("7h2c", "", Position.InPosition, pot: 15, toCall: 5, eff: 80, bb: 10)),

        new Scenario(
            "Deep open: AKo on the button, 100bb",
            MakeSpot("AhKs", "", Position.InPosition, pot: 15, toCall: 5, eff: 1000, bb: 10)),

        new Scenario(
            "Flop value bet: top set on a dry board",
            MakeSpot("AcAd", "Ah 7s 2c", Position.InPosition, pot: 100, toCall: 0, eff: 1000, bb: 10)),

        new Scenario(
            "River fold: air facing a pot-sized bet",
            MakeSpot("7h2c", "Ah Ks Qd 9c 3s", Position.OutOfPosition, pot: 200, toCall: 100, eff: 1000, bb: 10)),

        new Scenario(
            "Exploit a nit: same air, but villain folds 90% (high confidence) -> bluff more",
            MakeSpot("7h2c", "Ah Ks Qd 9c", Position.InPosition, pot: 100, toCall: 0, eff: 1000, bb: 10),
            new OpponentModel { FoldToBet = 0.9, Confidence = 1.0 }),
    };

    private static Spot MakeSpot(string hero, string board, Position pos, int pot, int toCall, int eff, int bb) => new()
    {
        Hero = HoleCards.Parse(hero),
        Board = ParseCards(board),
        Position = pos,
        Pot = pot,
        ToCall = toCall,
        EffectiveStack = eff,
        BigBlind = bb,
    };

    private static IReadOnlyList<Card> ParseCards(string s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<Card>()
            : s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToList();
}
