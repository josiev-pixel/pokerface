using System;

namespace PokerEngine.Solver
{
    /// <summary>
    /// The known closed-form Nash equilibrium of Kuhn poker, encoded so tests can validate
    /// the CFR+ solver against the literature.
    /// <para>
    /// The game value to player 0 (the first actor) is exactly −1/18 per hand; player 1's is
    /// +1/18. Player 0 has a one-parameter family of optimal strategies indexed by
    /// α ∈ [0, 1/3]; player 1's optimal strategy is unique and independent of α.
    /// </para>
    /// <para><b>Player 0 (parameter α ∈ [0, 1/3]):</b></para>
    /// <list type="bullet">
    ///   <item><description>Jack: bet first with probability α; after checking and facing a bet, always fold.</description></item>
    ///   <item><description>Queen: always check first; facing a bet after a check, call with probability α + 1/3.</description></item>
    ///   <item><description>King: bet first with probability 3α; always call. (So P(bet King) = 3·P(bet Jack).)</description></item>
    /// </list>
    /// <para><b>Player 1 (unique):</b></para>
    /// <list type="bullet">
    ///   <item><description>Jack: facing a check, bet w.p. 1/3; facing a bet, fold.</description></item>
    ///   <item><description>Queen: facing a check, check; facing a bet, call w.p. 1/3.</description></item>
    ///   <item><description>King: facing a check, bet; facing a bet, call.</description></item>
    /// </list>
    /// </summary>
    public static class KuhnEquilibrium
    {
        /// <summary>The lower bound of player 0's strategy parameter α.</summary>
        public const double AlphaMin = 0.0;

        /// <summary>The upper bound of player 0's strategy parameter α.</summary>
        public const double AlphaMax = 1.0 / 3.0;

        /// <summary>The game value to player 0 (the first actor), exactly −1/18 per hand.</summary>
        /// <returns>−1/18.</returns>
        public static double ExpectedValuePlayer1() => -1.0 / 18.0;

        /// <summary>The game value to player 1 (the second actor), exactly +1/18 per hand.</summary>
        /// <returns>+1/18.</returns>
        public static double ExpectedValuePlayer2() => 1.0 / 18.0;

        /// <summary>P(player 0 bets first with a Jack) as a function of α — equals α.</summary>
        public static double Player0JackBet(double alpha) => alpha;

        /// <summary>P(player 0 bets first with a King) as a function of α — equals 3α.</summary>
        public static double Player0KingBet(double alpha) => 3.0 * alpha;

        /// <summary>P(player 0 calls a bet with a Queen after checking) as a function of α — equals α + 1/3.</summary>
        public static double Player0QueenCall(double alpha) => alpha + (1.0 / 3.0);

        /// <summary>P(player 1 bets a Jack after a check) — exactly 1/3.</summary>
        public static double Player1JackBetAfterCheck() => 1.0 / 3.0;

        /// <summary>P(player 1 calls a bet with a Queen) — exactly 1/3.</summary>
        public static double Player1QueenCall() => 1.0 / 3.0;

        /// <summary>True if <paramref name="alpha"/> lies in the valid range [0, 1/3] (within tolerance).</summary>
        public static bool IsAlphaInRange(double alpha, double tolerance = 1e-9) =>
            alpha >= AlphaMin - tolerance && alpha <= AlphaMax + tolerance;

        /// <summary>
        /// Recovers the strategy parameter α from a solved P(player 0 bets a King), i.e. α = P / 3,
        /// then clamps to the valid range to absorb tiny numerical slack.
        /// </summary>
        /// <param name="kingBetProbability">The solved probability that player 0 bets first with a King.</param>
        /// <returns>The implied α, clamped to [0, 1/3].</returns>
        public static double RecoverAlphaFromKingBet(double kingBetProbability)
        {
            double alpha = kingBetProbability / 3.0;
            return Math.Clamp(alpha, AlphaMin, AlphaMax);
        }

        /// <summary>
        /// Checks whether a solved player-0 strategy is a member of the α-family within
        /// <paramref name="tolerance"/>: α (recovered from the King's bet frequency) is in
        /// range, the Jack bets with probability ≈ α, and the Queen calls with probability ≈ α + 1/3.
        /// </summary>
        /// <param name="jackBet">Solved P(player 0 bets first with a Jack).</param>
        /// <param name="queenCall">Solved P(player 0 calls a bet with a Queen).</param>
        /// <param name="kingBet">Solved P(player 0 bets first with a King).</param>
        /// <param name="tolerance">Allowed absolute deviation for each frequency.</param>
        /// <returns>True if the strategy is consistent with the α-family.</returns>
        public static bool IsPlayer0InAlphaFamily(double jackBet, double queenCall, double kingBet, double tolerance)
        {
            double alpha = RecoverAlphaFromKingBet(kingBet);
            return IsAlphaInRange(alpha, tolerance) &&
                Math.Abs(jackBet - Player0JackBet(alpha)) <= tolerance &&
                Math.Abs(queenCall - Player0QueenCall(alpha)) <= tolerance;
        }
    }
}
