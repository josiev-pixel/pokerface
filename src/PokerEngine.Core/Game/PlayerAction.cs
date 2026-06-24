namespace PokerEngine.Core.Game
{
    /// <summary>
    /// A single action by the player to act. <see cref="To"/> is a "to" amount: the total chips the
    /// player will have committed <em>on the current street</em> once the action resolves — the
    /// natural representation for No-Limit bets and raises (e.g. "raise to 60"). For
    /// <see cref="ActionKind.Fold"/> and <see cref="ActionKind.Check"/> it is unused and 0. For
    /// <see cref="ActionKind.Call"/> the engine derives the actual amount from the current bet and
    /// the caller's stack (so <see cref="To"/> is left 0 and need not be supplied).
    /// </summary>
    public readonly struct PlayerAction
    {
        public PlayerAction(ActionKind kind, int to)
        {
            Kind = kind;
            To = to;
        }

        /// <summary>What the player is doing.</summary>
        public ActionKind Kind { get; }

        /// <summary>Target total street commitment after a <see cref="ActionKind.Bet"/>/<see cref="ActionKind.Raise"/>; otherwise 0.</summary>
        public int To { get; }

        /// <summary>Fold and forfeit the hand.</summary>
        public static PlayerAction Fold() => new(ActionKind.Fold, 0);

        /// <summary>Check (only legal when there is nothing to call).</summary>
        public static PlayerAction Check() => new(ActionKind.Check, 0);

        /// <summary>Call the current bet (amount resolved by the engine; all-in if short).</summary>
        public static PlayerAction Call() => new(ActionKind.Call, 0);

        /// <summary>Open the betting to a total street commitment of <paramref name="to"/>.</summary>
        public static PlayerAction BetTo(int to) => new(ActionKind.Bet, to);

        /// <summary>Raise to a total street commitment of <paramref name="to"/>.</summary>
        public static PlayerAction RaiseTo(int to) => new(ActionKind.Raise, to);

        public override string ToString() => Kind switch
        {
            ActionKind.Fold => "Fold",
            ActionKind.Check => "Check",
            ActionKind.Call => "Call",
            ActionKind.Bet => $"Bet to {To}",
            ActionKind.Raise => $"Raise to {To}",
            _ => Kind.ToString(),
        };
    }
}
