using System;
using PokerEngine.Core.Game;
using PokerEngine.Profiling;

namespace PokerEngine.Decision
{
    /// <summary>
    /// Factory for creating OpponentModel instances from OpponentProfile data.
    /// </summary>
    public static class OpponentModelFactory
    {
        /// <summary>
        /// Creates an OpponentModel from an opponent profile and street.
        /// This adapts a three-horizon profile into the minimal model the DecisionEngine exploits,
        /// so confidence (sample-size driven) keeps a thin read on the GTO baseline.
        /// </summary>
        /// <param name="profile">The opponent profile to convert.</param>
        /// <param name="street">The street for which to get the fold-to-bet estimate.</param>
        /// <returns>An OpponentModel with FoldToBet and Confidence from the profile.</returns>
        public static OpponentModel FromProfile(OpponentProfile profile, Street street)
        {
            if (profile is null)
                throw new ArgumentNullException(nameof(profile));

            // Read both the fold-to-bet estimate AND its confidence from the SAME statistic, so
            // the exploit weight reflects how well-sampled *this* read is (not an unrelated proxy
            // like VPIP). Postflop uses the street's fold-to-bet; preflop/complete fall back to
            // fold-to-cbet. A thin sample → low confidence → the DecisionEngine stays near baseline.
            FrequencyStat stat = street switch
            {
                Street.Flop or Street.Turn or Street.River => profile.FoldToBet[street],
                Street.Preflop or Street.Complete => profile.FoldToCbet,
                _ => throw new ArgumentOutOfRangeException(nameof(street)),
            };

            return new OpponentModel
            {
                FoldToBet = stat.PosteriorMean,
                Confidence = stat.Confidence(),
            };
        }
    }
}
