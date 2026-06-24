using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Equity;

namespace PokerEngine.Decision
{
    /// <summary>
    /// The heads-up decision policy: a game-theory-flavored baseline that exploits a profiled
    /// opponent within a bounded, confidence-scaled deviation. It is deterministic and
    /// explainable — the only randomness is seeded mixed-strategy sampling ("bet p% of the
    /// time"), and it uses NO AI/ML in the decision path (POKER_THEORY §why-we-lean-deterministic,
    /// DECISION_ALGORITHM §0). The baseline is honest about its level: preflop uses the SAGE
    /// push/fold system short-stacked and the Chen score deeper; postflop uses equity-vs-pot-odds
    /// with MDF/alpha-aware mixing. Solved CFR strategies are validated separately (Solver) and
    /// will replace these heuristics street by street.
    /// </summary>
    public sealed class DecisionEngine
    {
        private readonly EngineConfig _cfg;

        public DecisionEngine(EngineConfig? config = null) => _cfg = config ?? EngineConfig.Default;

        /// <summary>
        /// Choose an action for <paramref name="spot"/>. <paramref name="rng"/> seeds equity
        /// sampling and mixed-strategy selection (same spot + model + seed ⇒ same action).
        /// Pass an <paramref name="opponent"/> model to enable bounded exploitation; omit it
        /// (or pass <see cref="OpponentModel.Unknown"/>) to play the pure baseline.
        /// </summary>
        public DecisionResult Decide(Spot spot, DeterministicRandom rng, OpponentModel? opponent = null)
        {
            ArgumentNullException.ThrowIfNull(spot);
            ArgumentNullException.ThrowIfNull(rng);
            opponent ??= OpponentModel.Unknown;
            double w = Math.Min(_cfg.MaxExploitWeight, Math.Clamp(opponent.Confidence, 0.0, 1.0));

            double equity = EstimateEquity(spot, rng);

            return spot.IsPreflop
                ? DecidePreflop(spot, equity, rng)
                : DecidePostflop(spot, equity, w, opponent, rng);
        }

        private double EstimateEquity(Spot spot, DeterministicRandom rng) =>
            EquityCalculator.HeadsUpVsRandom(
                new[] { spot.Hero.High, spot.Hero.Low }, spot.Board, rng, _cfg.EquitySamples).Equity;

        // ---------------------------------------------------------------- preflop

        private DecisionResult DecidePreflop(Spot spot, double equity, DeterministicRandom rng)
        {
            var hand = spot.Hero.ToStartingHand();
            int chen = ChenFormula.Score(hand);
            double effBB = spot.EffectiveBigBlinds;
            bool facingRaise = spot.ToCall > spot.BigBlind; // a real raise, not just completing the blind

            // Short-stack push/fold regime (SAGE) — deterministic, near-Nash.
            if (effBB <= SageSystem.MaxEffectiveBigBlinds)
            {
                int pi = SageSystem.PowerIndex(hand);
                if (!facingRaise)
                {
                    bool push = SageSystem.ShouldPush(hand, effBB);
                    int threshold = SageSystem.PushThreshold(effBB);
                    string why = $"SAGE push/fold at {effBB:0.#} BB: power index {pi} {(push ? "≥" : "<")} shove threshold {threshold}.";
                    return push
                        ? Pure(Move.Bet, spot.EffectiveStack, equity, 0, why + " Jam all-in.")
                        : Pure(Move.Fold, 0, equity, 0, why + " Fold.");
                }
                else
                {
                    bool call = SageSystem.ShouldCall(hand, effBB);
                    int threshold = SageSystem.CallThreshold(effBB);
                    string why = $"SAGE call/fold at {effBB:0.#} BB: power index {pi} {(call ? "≥" : "<")} call threshold {threshold}.";
                    return call
                        ? Pure(Move.Call, Math.Min(spot.ToCall, spot.EffectiveStack), equity, RequiredEq(spot), why + " Call the jam.")
                        : Pure(Move.Fold, 0, equity, RequiredEq(spot), why + " Fold.");
                }
            }

            // Deeper stacks: Chen-score open / 3-bet / call / fold.
            if (!facingRaise)
            {
                if (chen >= _cfg.OpenChen)
                {
                    int open = Clamp((int)Math.Round(_cfg.PreflopOpenBigBlinds * spot.BigBlind), 1, spot.EffectiveStack);
                    return Pure(Move.Bet, open, equity, 0,
                        $"Open-raise: Chen {chen} ≥ open threshold {_cfg.OpenChen} ({hand}). Raise to ~{_cfg.PreflopOpenBigBlinds:0.#} BB.");
                }
                return Pure(Move.Fold, 0, equity, 0, $"Fold: Chen {chen} < open threshold {_cfg.OpenChen} ({hand}).");
            }
            else
            {
                if (chen >= _cfg.ThreeBetChen)
                {
                    int raise = Clamp(spot.ToCall + (int)Math.Round(2.0 * spot.ToCall), 1, spot.EffectiveStack);
                    return Pure(Move.Raise, raise, equity, RequiredEq(spot),
                        $"3-bet for value: Chen {chen} ≥ {_cfg.ThreeBetChen} ({hand}).");
                }
                if (chen >= _cfg.CallRaiseChen)
                {
                    return Pure(Move.Call, Math.Min(spot.ToCall, spot.EffectiveStack), equity, RequiredEq(spot),
                        $"Call the raise: Chen {chen} ≥ {_cfg.CallRaiseChen} ({hand}).");
                }
                return Pure(Move.Fold, 0, equity, RequiredEq(spot),
                    $"Fold to the raise: Chen {chen} < call threshold {_cfg.CallRaiseChen} ({hand}).");
            }
        }

        // ---------------------------------------------------------------- postflop

        private DecisionResult DecidePostflop(Spot spot, double equity, double w, OpponentModel opp, DeterministicRandom rng)
        {
            return spot.FacingBet
                ? FacingBet(spot, equity, rng)
                : FirstToAct(spot, equity, w, opp, rng);
        }

        private DecisionResult FirstToAct(Spot spot, double equity, double w, OpponentModel opp, DeterministicRandom rng)
        {
            int bet = Clamp((int)Math.Round(_cfg.BetSizing * spot.Pot), 1, spot.EffectiveStack);
            double alpha = PokerMath.Alpha(spot.Pot, bet);

            var dist = new List<ActionOption>();
            string why;

            if (equity >= _cfg.ValueBetEquity)
            {
                // Strong: bet for value most of the time, occasionally check to protect the checking range.
                dist.Add(new ActionOption(Move.Bet, bet, _cfg.ValueBetFreq));
                dist.Add(new ActionOption(Move.Check, 0, 1 - _cfg.ValueBetFreq));
                why = $"Value: equity {equity:P1} ≥ {_cfg.ValueBetEquity:P0}. Bet ~{_cfg.BetSizing:P0} pot ({bet}) {_cfg.ValueBetFreq:P0} of the time.";
            }
            else if (equity <= _cfg.BluffCeilingEquity)
            {
                // Weak: bluff a balanced fraction, adjusted toward the opponent's fold tendency (bounded).
                double baseBluff = _cfg.BluffFreq;
                double exploitTarget = opp.FoldToBet > alpha ? Math.Min(1.0, baseBluff + 0.45) : Math.Max(0.0, baseBluff - 0.30);
                double bluff = Math.Clamp((1 - w) * baseBluff + w * exploitTarget, 0.0, 1.0);
                dist.Add(new ActionOption(Move.Bet, bet, bluff));
                dist.Add(new ActionOption(Move.Check, 0, 1 - bluff));
                why = $"Bluff candidate: equity {equity:P1} ≤ {_cfg.BluffCeilingEquity:P0}. Balanced bluff freq {baseBluff:P0}" +
                      (w > 0 ? $", exploit-adjusted to {bluff:P0} (villain folds {opp.FoldToBet:P0} vs alpha {alpha:P0}, w={w:0.##})." : ".");
            }
            else
            {
                // Medium: check for pot control / showdown value.
                dist.Add(new ActionOption(Move.Check, 0, 1.0));
                why = $"Pot control: equity {equity:P1} is marginal (between {_cfg.BluffCeilingEquity:P0} and {_cfg.ValueBetEquity:P0}). Check.";
            }

            return Sampled(dist, equity, 0.0, w, rng, why);
        }

        private DecisionResult FacingBet(Spot spot, double equity, DeterministicRandom rng)
        {
            double required = PokerMath.RequiredEquityToCall(spot.Pot, spot.ToCall);
            int call = Math.Min(spot.ToCall, spot.EffectiveStack);
            var dist = new List<ActionOption>();
            string why;

            if (equity >= _cfg.RaiseEquity)
            {
                int raise = Clamp(spot.ToCall + (int)Math.Round(_cfg.RaiseSizing * spot.Pot), call + 1, spot.EffectiveStack);
                if (raise <= call) raise = Math.Min(spot.EffectiveStack, call); // stack too short to raise → just call
                if (raise > call)
                {
                    dist.Add(new ActionOption(Move.Raise, raise, _cfg.ValueRaiseFreq));
                    dist.Add(new ActionOption(Move.Call, call, 1 - _cfg.ValueRaiseFreq));
                    why = $"Value raise: equity {equity:P1} ≥ {_cfg.RaiseEquity:P0} (pot odds need {required:P1}).";
                }
                else
                {
                    dist.Add(new ActionOption(Move.Call, call, 1.0));
                    why = $"Call (too short to raise): equity {equity:P1} ≥ pot-odds {required:P1}.";
                }
            }
            else if (equity >= required)
            {
                dist.Add(new ActionOption(Move.Call, call, 1.0));
                why = $"Call: equity {equity:P1} ≥ pot-odds break-even {required:P1}.";
            }
            else if (equity >= required * _cfg.MarginalDefendBand)
            {
                // Below the price but close: bluff-catch a fraction so we don't over-fold (defend toward MDF).
                dist.Add(new ActionOption(Move.Call, call, _cfg.MarginalDefendFreq));
                dist.Add(new ActionOption(Move.Fold, 0, 1 - _cfg.MarginalDefendFreq));
                why = $"Bluff-catch: equity {equity:P1} is just under pot-odds {required:P1}; defend {_cfg.MarginalDefendFreq:P0} to resist over-folding (MDF).";
            }
            else
            {
                dist.Add(new ActionOption(Move.Fold, 0, 1.0));
                why = $"Fold: equity {equity:P1} < pot-odds break-even {required:P1} (and not a viable bluff-catch).";
            }

            return Sampled(dist, equity, required, 0.0, rng, why);
        }

        // ---------------------------------------------------------------- helpers

        private static double RequiredEq(Spot spot) =>
            spot.FacingBet ? PokerMath.RequiredEquityToCall(spot.Pot, spot.ToCall) : 0.0;

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static DecisionResult Pure(Move move, int chips, double equity, double required, string why) =>
            new()
            {
                Move = move,
                Chips = chips,
                Strategy = new[] { new ActionOption(move, chips, 1.0) },
                Equity = equity,
                RequiredEquity = required,
                ExploitWeight = 0.0,
                Explanation = why,
            };

        private static DecisionResult Sampled(
            List<ActionOption> dist, double equity, double required, double w, DeterministicRandom rng, string why)
        {
            Normalize(dist);
            var chosen = SampleFrom(dist, rng);
            return new DecisionResult
            {
                Move = chosen.Move,
                Chips = chosen.Chips,
                Strategy = dist,
                Equity = equity,
                RequiredEquity = required,
                ExploitWeight = w,
                Explanation = why,
            };
        }

        private static void Normalize(List<ActionOption> dist)
        {
            double sum = dist.Sum(o => o.Probability);
            if (sum <= 0) { dist.Clear(); dist.Add(new ActionOption(Move.Check, 0, 1.0)); return; }
            for (int i = 0; i < dist.Count; i++)
                dist[i] = dist[i] with { Probability = dist[i].Probability / sum };
        }

        private static ActionOption SampleFrom(List<ActionOption> dist, DeterministicRandom rng)
        {
            double x = rng.NextDouble(), cum = 0;
            foreach (var o in dist)
            {
                cum += o.Probability;
                if (x < cum) return o;
            }
            return dist[^1];
        }
    }
}
