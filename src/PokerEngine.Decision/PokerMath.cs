using System;

namespace PokerEngine.Decision
{
    /// <summary>
    /// The exact, settled poker arithmetic every decision rests on: pot odds, minimum defense
    /// frequency, and the bluffing/value balance frequency (alpha). These are definitions, not
    /// heuristics — see docs/POKER_THEORY §1 and DECISION_ALGORITHM. All sizes are in chips.
    /// </summary>
    public static class PokerMath
    {
        /// <summary>
        /// Break-even equity to call: you risk <paramref name="toCall"/> to win the current
        /// <paramref name="pot"/> (which already includes the bet you face). Calling is +EV
        /// when your equity exceeds this. = toCall / (pot + toCall).
        /// </summary>
        public static double RequiredEquityToCall(int pot, int toCall)
        {
            if (toCall < 0) throw new ArgumentOutOfRangeException(nameof(toCall));
            if (toCall == 0) return 0.0;
            return (double)toCall / (pot + toCall);
        }

        /// <summary>
        /// Minimum Defense Frequency: the share of your range you must continue with against a
        /// bet of <paramref name="bet"/> into <paramref name="pot"/> so a pure bluff can't print
        /// money. = pot / (pot + bet). Defending less invites relentless bluffing.
        /// </summary>
        public static double MinimumDefenseFrequency(int pot, int bet)
        {
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet));
            return (double)pot / (pot + bet);
        }

        /// <summary>
        /// Alpha: the fraction of the time a bet of <paramref name="bet"/> into <paramref name="pot"/>
        /// needs to take it down to break even as a pure bluff, i.e. the opponent's required
        /// fold frequency, and the bluff-to-value balance point. = bet / (pot + bet) = 1 − MDF.
        /// </summary>
        public static double Alpha(int pot, int bet)
        {
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet));
            return (double)bet / (pot + bet);
        }

        /// <summary>
        /// EV (in chips) of a call: you put in <paramref name="toCall"/> to win a final pot of
        /// (<paramref name="pot"/> + <paramref name="toCall"/>) with probability <paramref name="equity"/>.
        /// EV = equity·(pot+toCall) − toCall·(1−equity) = equity·(pot + 2·toCall) − toCall.
        /// (Simplified showdown model: ignores future streets / implied odds.)
        /// </summary>
        public static double CallEv(int pot, int toCall, double equity)
        {
            if (equity < 0 || equity > 1) throw new ArgumentOutOfRangeException(nameof(equity));
            double win = equity * (pot + toCall);
            double lose = (1 - equity) * toCall;
            return win - lose;
        }

        /// <summary>
        /// EV (in chips) of a pure bluff that bets <paramref name="bet"/> into <paramref name="pot"/>
        /// and wins immediately with probability <paramref name="foldProbability"/>, otherwise loses
        /// the bet. = foldProbability·pot − (1−foldProbability)·bet. Break-even at fold = alpha.
        /// </summary>
        public static double BluffEv(int pot, int bet, double foldProbability)
        {
            if (foldProbability < 0 || foldProbability > 1) throw new ArgumentOutOfRangeException(nameof(foldProbability));
            return foldProbability * pot - (1 - foldProbability) * bet;
        }
    }
}
