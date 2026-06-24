using System;
using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// Two-player fixed-limit Leduc Hold'em as an <see cref="ICfrGame{TState}"/>.
    /// <para>
    /// The deck has ranks J, Q, K with two copies of each rank. Each player antes 1,
    /// receives one private card, plays a fixed-limit betting round with bet size 2
    /// and at most one raise after the initial bet, sees one community card, and then
    /// plays a second fixed-limit betting round with bet size 4 and the same cap.
    /// </para>
    /// </summary>
    public sealed class LeducPoker : ICfrGame<LeducState>
    {
        /// <summary>The rank value for a Jack.</summary>
        public const int Jack = 0;

        /// <summary>The rank value for a Queen.</summary>
        public const int Queen = 1;

        /// <summary>The rank value for a King.</summary>
        public const int King = 2;

        /// <summary>The ante posted by each player.</summary>
        public const int Ante = 1;

        /// <summary>The fixed bet size in the private-card betting round.</summary>
        public const int FirstRoundBetSize = 2;

        /// <summary>The fixed bet size in the community-card betting round.</summary>
        public const int SecondRoundBetSize = 4;

        /// <summary>The maximum number of wagers in a betting round: a bet and one raise.</summary>
        public const int MaxWagersPerRound = 2;

        private static readonly char[] RankLetters = { 'J', 'Q', 'K' };

        private static readonly GameAction Check = new GameAction("x");
        private static readonly GameAction Bet = new GameAction("b");
        private static readonly GameAction Fold = new GameAction("f");
        private static readonly GameAction Call = new GameAction("c");
        private static readonly GameAction Raise = new GameAction("r");

        private static readonly IReadOnlyList<GameAction> CheckOrBet = new[] { Check, Bet };
        private static readonly IReadOnlyList<GameAction> FoldOrCall = new[] { Fold, Call };
        private static readonly IReadOnlyList<GameAction> FoldCallOrRaise = new[] { Fold, Call, Raise };

        /// <inheritdoc/>
        public int PlayerCount => 2;

        /// <inheritdoc/>
        public LeducState Root => LeducState.ChanceRoot;

        /// <summary>Returns the rank value for a physical deck card.</summary>
        /// <param name="card">A physical card id from 0 through 5.</param>
        /// <returns>The rank value: 0=J, 1=Q, 2=K.</returns>
        public static int RankOf(int card)
        {
            if (card < 0 || card >= 6)
            {
                throw new ArgumentOutOfRangeException(nameof(card), "Leduc cards are physical ids 0 through 5.");
            }

            return card / 2;
        }

        /// <summary>Returns the public rank letter for a rank value.</summary>
        /// <param name="rank">The rank value: 0=J, 1=Q, 2=K.</param>
        /// <returns>The rank letter.</returns>
        public static char RankLetter(int rank)
        {
            if (rank < 0 || rank >= RankLetters.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), "Leduc ranks are 0=J, 1=Q, and 2=K.");
            }

            return RankLetters[rank];
        }

        /// <inheritdoc/>
        public bool IsTerminal(LeducState state) => state.FoldedPlayer >= 0 || state.IsShowdown;

        /// <inheritdoc/>
        public bool IsChance(LeducState state) => state.IsChanceRoot || state.IsAwaitingCommunity;

        /// <inheritdoc/>
        public IEnumerable<ChanceOutcome<LeducState>> ChanceOutcomes(LeducState state)
        {
            if (state.IsChanceRoot)
            {
                for (int player0Card = 0; player0Card < 6; player0Card++)
                {
                    for (int player1Card = 0; player1Card < 6; player1Card++)
                    {
                        if (player0Card == player1Card)
                        {
                            continue;
                        }

                        yield return new ChanceOutcome<LeducState>(
                            LeducState.Dealt(player0Card, player1Card),
                            1.0 / 30.0);
                    }
                }

                yield break;
            }

            if (!state.IsAwaitingCommunity)
            {
                throw new InvalidOperationException("Chance outcomes requested for a non-chance state.");
            }

            for (int communityCard = 0; communityCard < 6; communityCard++)
            {
                if (communityCard == state.Player0Card || communityCard == state.Player1Card)
                {
                    continue;
                }

                string history = state.History + RankLetter(RankOf(communityCard));
                yield return new ChanceOutcome<LeducState>(
                    new LeducState(
                        state.Player0Card,
                        state.Player1Card,
                        communityCard,
                        bettingRound: 1,
                        currentPlayer: 0,
                        player0Contribution: state.Player0Contribution,
                        player1Contribution: state.Player1Contribution,
                        roundWagerCount: 0,
                        roundActionCount: 0,
                        hasPendingWager: false,
                        foldedPlayer: -1,
                        isShowdown: false,
                        history,
                        isChanceRoot: false),
                    1.0 / 4.0);
            }
        }

        /// <inheritdoc/>
        public int CurrentPlayer(LeducState state) => state.CurrentPlayer;

        /// <inheritdoc/>
        public IReadOnlyList<GameAction> LegalActions(LeducState state)
        {
            if (state.HasPendingWager)
            {
                return state.RoundWagerCount < MaxWagersPerRound ? FoldCallOrRaise : FoldOrCall;
            }

            return CheckOrBet;
        }

        /// <inheritdoc/>
        public string InfoSetKey(LeducState state)
        {
            int card = state.CurrentPlayer == 0 ? state.Player0Card : state.Player1Card;
            return string.Concat(RankLetter(RankOf(card)).ToString(), ":", state.History);
        }

        /// <inheritdoc/>
        public LeducState Apply(LeducState state, GameAction action)
        {
            if (IsTerminal(state) || IsChance(state))
            {
                throw new InvalidOperationException("Actions can only be applied to decision states.");
            }

            return action.Code switch
            {
                "x" => ApplyCheck(state),
                "b" => ApplyBet(state),
                "f" => ApplyFold(state),
                "c" => ApplyCall(state),
                "r" => ApplyRaise(state),
                _ => throw new ArgumentException($"Unknown Leduc action '{action.Code}'.", nameof(action)),
            };
        }

        /// <inheritdoc/>
        public double Payoff(LeducState state, int player)
        {
            if (!IsTerminal(state))
            {
                throw new InvalidOperationException("Payoff requested for a non-terminal Leduc state.");
            }

            double payoff0 = Player0Payoff(state);
            return player == 0 ? payoff0 : -payoff0;
        }

        private static LeducState ApplyCheck(LeducState state)
        {
            if (state.HasPendingWager)
            {
                throw new InvalidOperationException("Cannot check while facing a wager.");
            }

            string history = state.History + Check.Code;
            if (state.RoundActionCount == 1 && state.RoundWagerCount == 0)
            {
                return FinishBettingRound(state, history);
            }

            return UpdateDecisionState(
                state,
                currentPlayer: OtherPlayer(state.CurrentPlayer),
                player0Contribution: state.Player0Contribution,
                player1Contribution: state.Player1Contribution,
                roundWagerCount: state.RoundWagerCount,
                roundActionCount: state.RoundActionCount + 1,
                hasPendingWager: false,
                history);
        }

        private static LeducState ApplyBet(LeducState state)
        {
            if (state.HasPendingWager)
            {
                throw new InvalidOperationException("Cannot bet while facing a wager.");
            }

            int amount = RoundBetSize(state);
            return AddWager(state, amount, Bet.Code, state.RoundWagerCount + 1);
        }

        private static LeducState ApplyRaise(LeducState state)
        {
            if (!state.HasPendingWager)
            {
                throw new InvalidOperationException("Cannot raise without a pending wager.");
            }

            if (state.RoundWagerCount >= MaxWagersPerRound)
            {
                throw new InvalidOperationException("The betting round raise cap has already been reached.");
            }

            int amountToCall = Contribution(state, OtherPlayer(state.CurrentPlayer)) - Contribution(state, state.CurrentPlayer);
            return AddWager(state, amountToCall + RoundBetSize(state), Raise.Code, state.RoundWagerCount + 1);
        }

        private static LeducState ApplyCall(LeducState state)
        {
            if (!state.HasPendingWager)
            {
                throw new InvalidOperationException("Cannot call without a pending wager.");
            }

            int amountToCall = Contribution(state, OtherPlayer(state.CurrentPlayer)) - Contribution(state, state.CurrentPlayer);
            string history = state.History + Call.Code;
            return FinishBettingRound(AddContribution(state, state.CurrentPlayer, amountToCall, history), history);
        }

        private static LeducState ApplyFold(LeducState state)
        {
            if (!state.HasPendingWager)
            {
                throw new InvalidOperationException("Cannot fold without a pending wager.");
            }

            return new LeducState(
                state.Player0Card,
                state.Player1Card,
                state.CommunityCard,
                state.BettingRound,
                currentPlayer: -1,
                state.Player0Contribution,
                state.Player1Contribution,
                state.RoundWagerCount,
                state.RoundActionCount + 1,
                hasPendingWager: false,
                foldedPlayer: state.CurrentPlayer,
                isShowdown: false,
                state.History + Fold.Code,
                isChanceRoot: false);
        }

        private static LeducState AddWager(LeducState state, int amount, string actionCode, int roundWagerCount)
        {
            string history = state.History + actionCode;
            LeducState withContribution = AddContribution(state, state.CurrentPlayer, amount, history);
            return UpdateDecisionState(
                withContribution,
                currentPlayer: OtherPlayer(state.CurrentPlayer),
                player0Contribution: withContribution.Player0Contribution,
                player1Contribution: withContribution.Player1Contribution,
                roundWagerCount,
                roundActionCount: state.RoundActionCount + 1,
                hasPendingWager: true,
                history);
        }

        private static LeducState AddContribution(LeducState state, int player, int amount, string history)
        {
            int player0Contribution = state.Player0Contribution;
            int player1Contribution = state.Player1Contribution;
            if (player == 0)
            {
                player0Contribution += amount;
            }
            else
            {
                player1Contribution += amount;
            }

            return new LeducState(
                state.Player0Card,
                state.Player1Card,
                state.CommunityCard,
                state.BettingRound,
                state.CurrentPlayer,
                player0Contribution,
                player1Contribution,
                state.RoundWagerCount,
                state.RoundActionCount,
                state.HasPendingWager,
                state.FoldedPlayer,
                state.IsShowdown,
                history,
                isChanceRoot: false);
        }

        private static LeducState UpdateDecisionState(
            LeducState state,
            int currentPlayer,
            int player0Contribution,
            int player1Contribution,
            int roundWagerCount,
            int roundActionCount,
            bool hasPendingWager,
            string history) =>
            new LeducState(
                state.Player0Card,
                state.Player1Card,
                state.CommunityCard,
                state.BettingRound,
                currentPlayer,
                player0Contribution,
                player1Contribution,
                roundWagerCount,
                roundActionCount,
                hasPendingWager,
                foldedPlayer: -1,
                isShowdown: false,
                history,
                isChanceRoot: false);

        private static LeducState FinishBettingRound(LeducState state, string history)
        {
            if (state.BettingRound == 0)
            {
                return new LeducState(
                    state.Player0Card,
                    state.Player1Card,
                    communityCard: -1,
                    bettingRound: 1,
                    currentPlayer: -1,
                    state.Player0Contribution,
                    state.Player1Contribution,
                    roundWagerCount: 0,
                    roundActionCount: 0,
                    hasPendingWager: false,
                    foldedPlayer: -1,
                    isShowdown: false,
                    history + "|",
                    isChanceRoot: false);
            }

            return new LeducState(
                state.Player0Card,
                state.Player1Card,
                state.CommunityCard,
                state.BettingRound,
                currentPlayer: -1,
                state.Player0Contribution,
                state.Player1Contribution,
                state.RoundWagerCount,
                state.RoundActionCount,
                hasPendingWager: false,
                foldedPlayer: -1,
                isShowdown: true,
                history,
                isChanceRoot: false);
        }

        private static int RoundBetSize(LeducState state) =>
            state.BettingRound == 0 ? FirstRoundBetSize : SecondRoundBetSize;

        private static int Contribution(LeducState state, int player) =>
            player == 0 ? state.Player0Contribution : state.Player1Contribution;

        private static int OtherPlayer(int player) => 1 - player;

        private static double Player0Payoff(LeducState state)
        {
            if (state.FoldedPlayer == 0)
            {
                return -state.Player0Contribution;
            }

            if (state.FoldedPlayer == 1)
            {
                return state.Player1Contribution;
            }

            int winner = ShowdownWinner(state);
            return winner switch
            {
                0 => state.Player1Contribution,
                1 => -state.Player0Contribution,
                _ => 0.0,
            };
        }

        private static int ShowdownWinner(LeducState state)
        {
            int communityRank = RankOf(state.CommunityCard);
            int player0Rank = RankOf(state.Player0Card);
            int player1Rank = RankOf(state.Player1Card);

            bool player0Pair = player0Rank == communityRank;
            bool player1Pair = player1Rank == communityRank;
            if (player0Pair && !player1Pair)
            {
                return 0;
            }

            if (player1Pair && !player0Pair)
            {
                return 1;
            }

            if (player0Rank > player1Rank)
            {
                return 0;
            }

            if (player1Rank > player0Rank)
            {
                return 1;
            }

            return -1;
        }
    }

    /// <summary>
    /// Immutable Leduc Hold'em state: physical private cards, optional community card,
    /// chip contributions, betting-round bookkeeping, and public history.
    /// </summary>
    public readonly struct LeducState : IEquatable<LeducState>
    {
        internal LeducState(
            int player0Card,
            int player1Card,
            int communityCard,
            int bettingRound,
            int currentPlayer,
            int player0Contribution,
            int player1Contribution,
            int roundWagerCount,
            int roundActionCount,
            bool hasPendingWager,
            int foldedPlayer,
            bool isShowdown,
            string history,
            bool isChanceRoot)
        {
            Player0Card = player0Card;
            Player1Card = player1Card;
            CommunityCard = communityCard;
            BettingRound = bettingRound;
            CurrentPlayer = currentPlayer;
            Player0Contribution = player0Contribution;
            Player1Contribution = player1Contribution;
            RoundWagerCount = roundWagerCount;
            RoundActionCount = roundActionCount;
            HasPendingWager = hasPendingWager;
            FoldedPlayer = foldedPlayer;
            IsShowdown = isShowdown;
            History = history;
            IsChanceRoot = isChanceRoot;
        }

        /// <summary>The pre-deal chance node.</summary>
        public static LeducState ChanceRoot { get; } = new LeducState(
            player0Card: -1,
            player1Card: -1,
            communityCard: -1,
            bettingRound: 0,
            currentPlayer: -1,
            player0Contribution: 0,
            player1Contribution: 0,
            roundWagerCount: 0,
            roundActionCount: 0,
            hasPendingWager: false,
            foldedPlayer: -1,
            isShowdown: false,
            history: string.Empty,
            isChanceRoot: true);

        /// <summary>Player 0's physical private card id, or -1 before the deal.</summary>
        public int Player0Card { get; }

        /// <summary>Player 1's physical private card id, or -1 before the deal.</summary>
        public int Player1Card { get; }

        /// <summary>The physical community card id, or -1 before the board is revealed.</summary>
        public int CommunityCard { get; }

        /// <summary>The betting round: 0 before the community card, 1 after it.</summary>
        public int BettingRound { get; }

        /// <summary>The player to act at a decision node, or -1 outside decision nodes.</summary>
        public int CurrentPlayer { get; }

        /// <summary>Total chips committed by player 0, including the ante.</summary>
        public int Player0Contribution { get; }

        /// <summary>Total chips committed by player 1, including the ante.</summary>
        public int Player1Contribution { get; }

        /// <summary>The number of bets or raises made in the current betting round.</summary>
        public int RoundWagerCount { get; }

        /// <summary>The number of actions made in the current betting round.</summary>
        public int RoundActionCount { get; }

        /// <summary>True when the acting player is facing an unmatched bet or raise.</summary>
        public bool HasPendingWager { get; }

        /// <summary>The folded player at a fold terminal, or -1 otherwise.</summary>
        public int FoldedPlayer { get; }

        /// <summary>True when the hand reached a showdown terminal.</summary>
        public bool IsShowdown { get; }

        /// <summary>The public action history, including the round boundary and board rank.</summary>
        public string History { get; }

        /// <summary>True for the distinguished pre-deal chance root.</summary>
        public bool IsChanceRoot { get; }

        /// <summary>True for the chance node that reveals the community card.</summary>
        public bool IsAwaitingCommunity =>
            !IsChanceRoot &&
            !IsShowdown &&
            FoldedPlayer < 0 &&
            BettingRound == 1 &&
            CommunityCard < 0;

        /// <summary>Creates a dealt first-round Leduc state.</summary>
        /// <param name="player0Card">Player 0's physical private card id.</param>
        /// <param name="player1Card">Player 1's physical private card id.</param>
        /// <returns>A decision state where player 0 acts first.</returns>
        public static LeducState Dealt(int player0Card, int player1Card) =>
            new LeducState(
                player0Card,
                player1Card,
                communityCard: -1,
                bettingRound: 0,
                currentPlayer: 0,
                player0Contribution: LeducPoker.Ante,
                player1Contribution: LeducPoker.Ante,
                roundWagerCount: 0,
                roundActionCount: 0,
                hasPendingWager: false,
                foldedPlayer: -1,
                isShowdown: false,
                history: string.Empty,
                isChanceRoot: false);

        /// <inheritdoc/>
        public bool Equals(LeducState other) =>
            Player0Card == other.Player0Card &&
            Player1Card == other.Player1Card &&
            CommunityCard == other.CommunityCard &&
            BettingRound == other.BettingRound &&
            CurrentPlayer == other.CurrentPlayer &&
            Player0Contribution == other.Player0Contribution &&
            Player1Contribution == other.Player1Contribution &&
            RoundWagerCount == other.RoundWagerCount &&
            RoundActionCount == other.RoundActionCount &&
            HasPendingWager == other.HasPendingWager &&
            FoldedPlayer == other.FoldedPlayer &&
            IsShowdown == other.IsShowdown &&
            IsChanceRoot == other.IsChanceRoot &&
            string.Equals(History, other.History, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is LeducState state && Equals(state);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Player0Card);
            hash.Add(Player1Card);
            hash.Add(CommunityCard);
            hash.Add(BettingRound);
            hash.Add(CurrentPlayer);
            hash.Add(Player0Contribution);
            hash.Add(Player1Contribution);
            hash.Add(RoundWagerCount);
            hash.Add(RoundActionCount);
            hash.Add(HasPendingWager);
            hash.Add(FoldedPlayer);
            hash.Add(IsShowdown);
            hash.Add(History, StringComparer.Ordinal);
            hash.Add(IsChanceRoot);
            return hash.ToHashCode();
        }
    }
}
