using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PokerEngine.Core.Game;

namespace PokerEngine.Profiling
{
    /// <summary>
    /// Aggregates several FrequencyStats for one opponent, updated as hands are observed.
    /// </summary>
    public sealed class OpponentProfile
    {
        private readonly string _id;
        private int _handsSeen;
        private readonly Dictionary<Street, FrequencyStat> _foldToBet;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpponentProfile"/> class.
        /// </summary>
        /// <param name="opponentId">The unique identifier for this opponent.</param>
        public OpponentProfile(string opponentId)
        {
            if (string.IsNullOrEmpty(opponentId))
                throw new ArgumentException("Opponent ID cannot be null or empty.", nameof(opponentId));

            _id = opponentId;
            
            // Initialize all the FrequencyStats with sensible heads-up priors
            Vpip = new FrequencyStat(12, 0.55);
            Pfr = new FrequencyStat(12, 0.45);
            ThreeBet = new FrequencyStat(12, 0.12);
            FoldToCbet = new FrequencyStat(12, 0.45);
            
            // Initialize fold-to-bet stats for post-flop streets
            _foldToBet = new Dictionary<Street, FrequencyStat>
            {
                { Street.Flop, new FrequencyStat(12, 0.45) },
                { Street.Turn, new FrequencyStat(12, 0.45) },
                { Street.River, new FrequencyStat(12, 0.45) }
            };

            AggressiveActions = 0;
            PassiveActions = 0;
        }

        /// <summary>
        /// Gets the unique identifier for this opponent.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the number of hands seen.
        /// </summary>
        public int HandsSeen => _handsSeen;

        /// <summary>
        /// Gets the VPIP (Voluntarily Put Money In Pot) frequency statistic.
        /// </summary>
        public FrequencyStat Vpip { get; }

        /// <summary>
        /// Gets the PFR (Pre-flop Raise) frequency statistic.
        /// </summary>
        public FrequencyStat Pfr { get; }

        /// <summary>
        /// Gets the 3-Bet frequency statistic.
        /// </summary>
        public FrequencyStat ThreeBet { get; }

        /// <summary>
        /// Gets the Fold to C-bet frequency statistic.
        /// </summary>
        public FrequencyStat FoldToCbet { get; }

        /// <summary>
        /// Gets a read-only dictionary of fold-to-bet statistics by street.
        /// </summary>
        public IReadOnlyDictionary<Street, FrequencyStat> FoldToBet => _foldToBet;

        private int AggressiveActions { get; set; }
        private int PassiveActions { get; set; }

        /// <summary>
        /// Records preflop actions for this opponent.
        /// </summary>
        /// <param name="voluntarilyPutMoneyIn">Whether the opponent voluntarily put money in the pot.</param>
        /// <param name="raisedPreflop">Whether the opponent raised pre-flop.</param>
        public void RecordPreflop(bool voluntarilyPutMoneyIn, bool raisedPreflop)
        {
            _handsSeen++;
            Vpip.Observe(voluntarilyPutMoneyIn);
            Pfr.Observe(raisedPreflop);
        }

        /// <summary>
        /// Records a three-bet opportunity for this opponent.
        /// </summary>
        /// <param name="threeBet">Whether the opponent 3-bet.</param>
        public void RecordThreeBetOpportunity(bool threeBet)
        {
            ThreeBet.Observe(threeBet);
        }

        /// <summary>
        /// Records facing a continuation bet for this opponent.
        /// </summary>
        /// <param name="folded">Whether the opponent folded to the continuation bet.</param>
        public void RecordFacingCbet(bool folded)
        {
            FoldToCbet.Observe(folded);
        }

        /// <summary>
        /// Records facing a bet on a specific street for this opponent.
        /// </summary>
        /// <param name="street">The street of the bet.</param>
        /// <param name="folded">Whether the opponent folded to the bet.</param>
        public void RecordFacingBet(Street street, bool folded)
        {
            if (street == Street.Preflop)
                throw new ArgumentOutOfRangeException(nameof(street), "Preflop is not a post-flop street.");

            if (!_foldToBet.TryGetValue(street, out var stat))
                throw new ArgumentOutOfRangeException(nameof(street), "Only Flop, Turn and River streets are supported for fold-to-bet tracking.");

            stat.Observe(folded);
        }

        /// <summary>
        /// Records an aggressive action by this opponent.
        /// </summary>
        public void RecordAggressiveAction()
        {
            AggressiveActions++;
        }

        /// <summary>
        /// Records a passive action by this opponent.
        /// </summary>
        public void RecordPassiveAction()
        {
            PassiveActions++;
        }

        /// <summary>
        /// Gets the aggression frequency (aggressive actions / total actions).
        /// </summary>
        public double AggressionFrequency => (AggressiveActions + PassiveActions) == 0 ? 0.0 : (double)AggressiveActions / (AggressiveActions + PassiveActions);

        /// <summary>
        /// Gets a confidence proxy based on the number of hands seen.
        /// </summary>
        public double Confidence => Vpip.Confidence();

        /// <summary>
        /// Gets the mean fold-to-bet probability for a specific street.
        /// </summary>
        /// <param name="street">The street to get the fold-to-bet mean for.</param>
        /// <returns>The posterior mean of the fold-to-bet frequency on that street.</returns>
        public double FoldToBetMean(Street street)
        {
            if (street == Street.Preflop)
                throw new ArgumentOutOfRangeException(nameof(street), "Preflop is not a post-flop street.");

            return _foldToBet[street].PosteriorMean;
        }

        /// <summary>
        /// A record structure representing a detected leak in opponent behavior.
        /// </summary>
        public readonly record struct Leak(string Name, double Observed, double Baseline, double Confidence);

        /// <summary>
        /// Detects potential leaks in opponent profiling based on significant deviations from prior expectations.
        /// </summary>
        /// <param name="minConfidence">The minimum confidence level to consider a leak (default 0.3).</param>
        /// <param name="minDeviation">The minimum deviation from baseline to consider a leak (default 0.1).</param>
        /// <returns>A list of detected leaks, or an empty list if none found.</returns>
        public IReadOnlyList<Leak> DetectLeaks(double minConfidence = 0.3, double minDeviation = 0.1)
        {
            var leaks = new List<Leak>();

            // Check VPIP
            var vpipDiff = Math.Abs(Vpip.PosteriorMean - Vpip.PriorMean);
            if (Vpip.Confidence() >= minConfidence && vpipDiff >= minDeviation)
                leaks.Add(new Leak("VPIP", Vpip.PosteriorMean, Vpip.PriorMean, Vpip.Confidence()));

            // Check PFR
            var pfrDiff = Math.Abs(Pfr.PosteriorMean - Pfr.PriorMean);
            if (Pfr.Confidence() >= minConfidence && pfrDiff >= minDeviation)
                leaks.Add(new Leak("PFR", Pfr.PosteriorMean, Pfr.PriorMean, Pfr.Confidence()));

            // Check 3-Bet
            var threeBetDiff = Math.Abs(ThreeBet.PosteriorMean - ThreeBet.PriorMean);
            if (ThreeBet.Confidence() >= minConfidence && threeBetDiff >= minDeviation)
                leaks.Add(new Leak("3Bet", ThreeBet.PosteriorMean, ThreeBet.PriorMean, ThreeBet.Confidence()));

            // Check Fold to C-bet
            var foldToCbetDiff = Math.Abs(FoldToCbet.PosteriorMean - FoldToCbet.PriorMean);
            if (FoldToCbet.Confidence() >= minConfidence && foldToCbetDiff >= minDeviation)
                leaks.Add(new Leak("FoldToCbet", FoldToCbet.PosteriorMean, FoldToCbet.PriorMean, FoldToCbet.Confidence()));

            // Check Fold to Bet for each post-flop street
            foreach (var kvp in _foldToBet)
            {
                var diff = Math.Abs(kvp.Value.PosteriorMean - kvp.Value.PriorMean);
                if (kvp.Value.Confidence() >= minConfidence && diff >= minDeviation)
                    leaks.Add(new Leak($"FoldToBet:{kvp.Key}", kvp.Value.PosteriorMean, kvp.Value.PriorMean, kvp.Value.Confidence()));
            }

            return leaks;
        }
    }
}
