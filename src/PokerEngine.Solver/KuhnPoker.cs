using System;
using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// Kuhn poker as an <see cref="ICfrGame{TState}"/> — the classic toy game used to
    /// validate CFR machinery against a known closed-form equilibrium.
    /// <para>
    /// Rules: a 3-card deck {J, Q, K} with J &lt; Q &lt; K. Two players each ante 1 (pot
    /// starts at 2) and are dealt one private card. There is a single betting round in
    /// which a bet/call is 1 chip. Player 0 acts first. With no bet pending a player may
    /// <em>check</em> ('p') or <em>bet</em> ('b'); facing a bet a player may <em>fold</em>
    /// ('f') or <em>call</em> ('c'). At showdown the higher card wins.
    /// </para>
    /// <para>
    /// Lines and net payoff to the bettor / higher-card winner (all relative to player 0):
    /// pp → showdown for pot 2 (±1); pbf → player 0 folds (−1); pbc → showdown for pot 4 (±2);
    /// bf → player 1 folds (+1); bc → showdown for pot 4 (±2).
    /// </para>
    /// <para>
    /// Info-set keys are the acting player's own card letter followed by the action
    /// history, e.g. "K", "Qb", "Jpb".
    /// </para>
    /// </summary>
    public sealed class KuhnPoker : ICfrGame<KuhnState>
    {
        /// <summary>Card value for the Jack (lowest).</summary>
        public const int Jack = 0;

        /// <summary>Card value for the Queen.</summary>
        public const int Queen = 1;

        /// <summary>Card value for the King (highest).</summary>
        public const int King = 2;

        private static readonly char[] CardLetters = { 'J', 'Q', 'K' };

        private static readonly GameAction Pass = new GameAction("p");
        private static readonly GameAction Bet = new GameAction("b");
        private static readonly GameAction Fold = new GameAction("f");
        private static readonly GameAction Call = new GameAction("c");

        private static readonly IReadOnlyList<GameAction> CheckOrBet = new[] { Pass, Bet };
        private static readonly IReadOnlyList<GameAction> FoldOrCall = new[] { Fold, Call };

        /// <inheritdoc/>
        public int PlayerCount => 2;

        /// <inheritdoc/>
        public KuhnState Root => KuhnState.ChanceRoot;

        /// <summary>The letter ('J', 'Q', 'K') for a card value.</summary>
        public static char CardLetter(int card) => CardLetters[card];

        /// <inheritdoc/>
        public bool IsChance(KuhnState state) => state.IsChanceRoot;

        /// <inheritdoc/>
        public IEnumerable<ChanceOutcome<KuhnState>> ChanceOutcomes(KuhnState state)
        {
            if (!state.IsChanceRoot)
            {
                throw new InvalidOperationException("Chance outcomes requested for a non-chance state.");
            }

            // The 6 equally-likely ways to deal two distinct cards from {J, Q, K}.
            for (int p0 = 0; p0 < 3; p0++)
            {
                for (int p1 = 0; p1 < 3; p1++)
                {
                    if (p0 == p1)
                    {
                        continue;
                    }

                    yield return new ChanceOutcome<KuhnState>(KuhnState.Dealt(p0, p1), 1.0 / 6.0);
                }
            }
        }

        /// <inheritdoc/>
        public bool IsTerminal(KuhnState state)
        {
            if (state.IsChanceRoot)
            {
                return false;
            }

            string h = state.History;
            return h switch
            {
                "pp" => true,
                "pbf" => true,
                "pbc" => true,
                "bf" => true,
                "bc" => true,
                _ => false,
            };
        }

        /// <inheritdoc/>
        public int CurrentPlayer(KuhnState state)
        {
            // Player 0 acts on empty and "pb" histories; player 1 acts on "p" and "b".
            return state.History.Length % 2;
        }

        /// <inheritdoc/>
        public IReadOnlyList<GameAction> LegalActions(KuhnState state)
        {
            string h = state.History;
            bool facingBet = h.Length > 0 && h[h.Length - 1] == 'b';
            return facingBet ? FoldOrCall : CheckOrBet;
        }

        /// <inheritdoc/>
        public string InfoSetKey(KuhnState state)
        {
            int card = CurrentPlayer(state) == 0 ? state.Player0Card : state.Player1Card;
            return string.Concat(CardLetter(card).ToString(), state.History);
        }

        /// <inheritdoc/>
        public KuhnState Apply(KuhnState state, GameAction action) =>
            KuhnState.Dealt(state.Player0Card, state.Player1Card, state.History + action.Code);

        /// <inheritdoc/>
        public double Payoff(KuhnState state, int player)
        {
            double payoff0 = Player0Payoff(state);
            return player == 0 ? payoff0 : -payoff0;
        }

        /// <summary>Net chips won by player 0 at a terminal state.</summary>
        private static double Player0Payoff(KuhnState state)
        {
            bool player0Wins = state.Player0Card > state.Player1Card;
            return state.History switch
            {
                // check-check: showdown for the 2-chip pot, ±1 net.
                "pp" => player0Wins ? 1.0 : -1.0,

                // check-bet-fold: player 0 folded, loses the ante.
                "pbf" => -1.0,

                // check-bet-call: showdown for the 4-chip pot, ±2 net.
                "pbc" => player0Wins ? 2.0 : -2.0,

                // bet-fold: player 1 folded, player 0 wins the ante.
                "bf" => 1.0,

                // bet-call: showdown for the 4-chip pot, ±2 net.
                "bc" => player0Wins ? 2.0 : -2.0,

                _ => throw new InvalidOperationException($"Payoff requested for non-terminal history '{state.History}'."),
            };
        }
    }

    /// <summary>
    /// An immutable Kuhn-poker state: the two dealt cards plus the public action history.
    /// The distinguished <see cref="ChanceRoot"/> value represents the pre-deal chance node.
    /// </summary>
    public readonly struct KuhnState : IEquatable<KuhnState>
    {
        private KuhnState(int player0Card, int player1Card, string history, bool isChanceRoot)
        {
            Player0Card = player0Card;
            Player1Card = player1Card;
            History = history;
            IsChanceRoot = isChanceRoot;
        }

        /// <summary>The pre-deal chance node.</summary>
        public static KuhnState ChanceRoot { get; } = new KuhnState(-1, -1, string.Empty, true);

        /// <summary>Player 0's private card value (0=J, 1=Q, 2=K), or −1 at the chance root.</summary>
        public int Player0Card { get; }

        /// <summary>Player 1's private card value (0=J, 1=Q, 2=K), or −1 at the chance root.</summary>
        public int Player1Card { get; }

        /// <summary>The public action history, e.g. "", "b", "pb", "pbc".</summary>
        public string History { get; }

        /// <summary>True for the distinguished pre-deal chance node.</summary>
        public bool IsChanceRoot { get; }

        /// <summary>A dealt state with no actions yet.</summary>
        public static KuhnState Dealt(int player0Card, int player1Card) =>
            new KuhnState(player0Card, player1Card, string.Empty, false);

        /// <summary>A dealt state with the given action history.</summary>
        public static KuhnState Dealt(int player0Card, int player1Card, string history) =>
            new KuhnState(player0Card, player1Card, history, false);

        /// <inheritdoc/>
        public bool Equals(KuhnState other) =>
            Player0Card == other.Player0Card &&
            Player1Card == other.Player1Card &&
            IsChanceRoot == other.IsChanceRoot &&
            string.Equals(History, other.History, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is KuhnState s && Equals(s);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Player0Card, Player1Card, History, IsChanceRoot);
    }
}
