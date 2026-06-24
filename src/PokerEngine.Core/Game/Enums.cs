namespace PokerEngine.Core.Game
{
    /// <summary>The four betting rounds of a hand, plus a terminal marker for a finished hand.</summary>
    public enum Street
    {
        Preflop = 0,
        Flop = 1,
        Turn = 2,
        River = 3,
        Complete = 4,
    }

    /// <summary>A seat's standing in the current hand.</summary>
    public enum SeatStatus
    {
        /// <summary>Still in the hand with chips behind and able to act.</summary>
        Active = 0,
        /// <summary>Folded; out of the hand and ineligible to win.</summary>
        Folded = 1,
        /// <summary>In the hand but with no chips behind; cannot act further.</summary>
        AllIn = 2,
    }

    /// <summary>The kinds of action a player may take.</summary>
    public enum ActionKind
    {
        Fold = 0,
        Check = 1,
        Call = 2,
        Bet = 3,
        Raise = 4,
    }
}
