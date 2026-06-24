using System.Collections.Generic;

namespace PokerEngine.Solver
{
    /// <summary>
    /// A small, perfect-recall, two-player zero-sum extensive-form game that the
    /// CFR+ solver can traverse exhaustively.
    /// <para>
    /// The game is modelled as a tree of states of type <typeparamref name="TState"/>.
    /// Three kinds of node exist: <em>chance</em> nodes (the deal), <em>decision</em>
    /// nodes (a player acts), and <em>terminal</em> nodes (payoffs are known). The
    /// solver asks the game to classify each state and to expand it.
    /// </para>
    /// <para>
    /// The contract is deliberately minimal so that the same CFR+ engine can later
    /// solve other tiny games (Leduc, simplified push/fold spots, …) — not just Kuhn.
    /// Implementations must be deterministic: identical states must classify and
    /// expand identically every time.
    /// </para>
    /// </summary>
    /// <typeparam name="TState">The immutable game-state representation.</typeparam>
    public interface ICfrGame<TState>
    {
        /// <summary>The number of players. For the CFR+ core this is always 2.</summary>
        int PlayerCount { get; }

        /// <summary>The root state, before any cards are dealt.</summary>
        TState Root { get; }

        /// <summary>True when <paramref name="state"/> is a leaf with known payoffs.</summary>
        bool IsTerminal(TState state);

        /// <summary>
        /// True when <paramref name="state"/> is a chance node (e.g. the deal). The solver
        /// expands it by iterating <see cref="ChanceOutcomes"/> rather than by player choice.
        /// </summary>
        bool IsChance(TState state);

        /// <summary>
        /// The utility of a terminal <paramref name="state"/> to <paramref name="player"/>,
        /// in chips. Zero-sum: the two players' payoffs sum to zero.
        /// </summary>
        /// <param name="state">A state for which <see cref="IsTerminal"/> is true.</param>
        /// <param name="player">The player index (0-based) whose payoff is requested.</param>
        double Payoff(TState state, int player);

        /// <summary>
        /// The possible outcomes of a chance <paramref name="state"/>, each paired with
        /// its probability. Probabilities sum to 1.
        /// </summary>
        /// <param name="state">A state for which <see cref="IsChance"/> is true.</param>
        IEnumerable<ChanceOutcome<TState>> ChanceOutcomes(TState state);

        /// <summary>The 0-based index of the player to act at a decision <paramref name="state"/>.</summary>
        int CurrentPlayer(TState state);

        /// <summary>
        /// The legal actions at a decision <paramref name="state"/>. The returned order is
        /// stable and defines the action indices used in regret/strategy arrays.
        /// </summary>
        IReadOnlyList<GameAction> LegalActions(TState state);

        /// <summary>
        /// The information-set key for the acting player at a decision <paramref name="state"/> —
        /// everything that player knows (their private card plus the public action history).
        /// States the acting player cannot distinguish must share a key.
        /// </summary>
        string InfoSetKey(TState state);

        /// <summary>The successor state after taking <paramref name="action"/> at <paramref name="state"/>.</summary>
        TState Apply(TState state, GameAction action);
    }

    /// <summary>One outcome of a chance node: a successor state and its probability.</summary>
    /// <typeparam name="TState">The game-state representation.</typeparam>
    public readonly struct ChanceOutcome<TState>
    {
        /// <summary>Creates a chance outcome.</summary>
        public ChanceOutcome(TState state, double probability)
        {
            State = state;
            Probability = probability;
        }

        /// <summary>The successor state produced by this outcome.</summary>
        public TState State { get; }

        /// <summary>The probability of this outcome (the outcomes of a node sum to 1).</summary>
        public double Probability { get; }
    }

    /// <summary>
    /// A game action, identified by a stable code (e.g. "p" pass/check, "b" bet,
    /// "c" call, "f" fold). The code keeps info-set keys readable and game-agnostic.
    /// </summary>
    public readonly struct GameAction
    {
        /// <summary>Creates an action with the given short code.</summary>
        public GameAction(string code)
        {
            Code = code;
        }

        /// <summary>A short, stable code used in histories and info-set keys.</summary>
        public string Code { get; }

        /// <inheritdoc/>
        public override string ToString() => Code;
    }
}
