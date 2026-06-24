using System.Collections.Generic;
using PokerEngine.Core;

namespace PokerEngine.Decision
{
    /// <summary>Heads-up position. The button/small blind acts last postflop (in position).</summary>
    public enum Position
    {
        OutOfPosition,
        InPosition,
    }

    /// <summary>
    /// A single decision point ("spot") presented to the engine: our cards, the board, the
    /// money, and whose turn it is. Deliberately decoupled from the full betting engine so the
    /// decision policy can be exercised directly from tests, the CLI, and the scenario table.
    /// All chip amounts are integers.
    /// </summary>
    public sealed record Spot
    {
        public required HoleCards Hero { get; init; }

        /// <summary>Community cards: 0 (preflop), 3 (flop), 4 (turn) or 5 (river).</summary>
        public required IReadOnlyList<Card> Board { get; init; }

        public required Position Position { get; init; }

        /// <summary>Chips already in the pot when it is our turn — includes a bet we are facing.</summary>
        public required int Pot { get; init; }

        /// <summary>Chips we must put in to continue. 0 means the action is checked to us / we open.</summary>
        public required int ToCall { get; init; }

        /// <summary>The smaller of the two remaining stacks (chips behind) — the most we can win/lose.</summary>
        public required int EffectiveStack { get; init; }

        public required int BigBlind { get; init; }

        public bool IsPreflop => Board.Count == 0;
        public bool FacingBet => ToCall > 0;
        public double EffectiveBigBlinds => (double)EffectiveStack / BigBlind;

        public string StreetName => Board.Count switch
        {
            0 => "preflop",
            3 => "flop",
            4 => "turn",
            5 => "river",
            _ => $"board[{Board.Count}]",
        };
    }
}
