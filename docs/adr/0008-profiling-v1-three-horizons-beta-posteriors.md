# ADR 0008: Profiling v1 — three-horizon stats, Beta-posterior frequencies, sample-size confidence

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design)

## Context
Exploitation needs a model of the opponent (POKER_THEORY §4, DECISION_ALGORITHM §6). Today the
`Decision` layer takes a hand-written `OpponentModel` stub (`FoldToBet`, `Confidence`). We want the
real thing: observed tendencies at three time horizons, turned into frequency estimates with a
principled confidence, so the bounded-exploit blend `w = f(c)` is driven by data, not a guess.
A wrong, over-confident read is dangerous (a best response to a bad model is itself exploitable),
so **confidence must start low and grow only with evidence** — that is the load-bearing property.

## Decision
A standalone `PokerEngine.Profiling` v1 (referencing only `Core`) that ingests observed actions and
produces an `OpponentProfile`. It does **not** reference `Decision`; wiring the profile into the
decision policy (deriving `OpponentModel` per spot) is a deliberate follow-up so this stays small.

1. **Stat counters.** A `FrequencyStat` = (opportunities, occurrences) for a tracked behavior. The
   tracked set v1: VPIP, PFR, 3-bet%, fold-to-cbet, fold-to-bet (per street), and aggression
   frequency AF = (bets+raises)/(bets+raises+calls). Each is updated as hands are observed.

2. **Three horizons.** The same counters maintained over: **lifetime** (all hands), a **recent
   window** (last N hands, exponentially decayed so tilt/dynamics surface), and the **current hand**
   (the line so far). v1 keeps lifetime + recent (decayed); current-hand range reading is a thin
   read over the live action list.

3. **Bayesian frequency estimate.** Each frequency is a Beta posterior: prior `Beta(α0, β0)` encodes
   the GTO/default rate and our trust in it; posterior mean `= (α0 + occ) / (α0 + β0 + opp)`. Few
   observations ⇒ near the prior (≈ GTO); many ⇒ near observed. This is the "start at GTO, move toward
   the read" behavior DECISION_ALGORITHM §6 calls for.

4. **Read confidence `c ∈ [0,1]`.** v1: a sample-size saturation `c = opp / (opp + K)` (K a tunable
   "half-confidence" sample count), optionally scaled by the magnitude of the deviation from the prior
   (a leak that is both well-sampled and large earns more confidence than a marginal one). `c` is what
   feeds `w = f(c)` in `Decision`, capped at `w_max`.

5. **Leak flags.** Compare each posterior frequency to its GTO baseline and emit structured flags
   (over-folds-to-bets, calling-station/low-AF, under-3-bets, etc.) with the deviation size and `c`,
   so the exploit is both explainable and bounded by how sure we are.

`OpponentProfile` exposes the posterior frequencies, per-street fold-to-bet, AF, the leak flags, and
`c`, plus enough to later synthesize a `Decision.OpponentModel` for a given spot.

## Consequences
- Profiling is independently testable: feed scripted action streams, assert the posterior frequencies,
  that confidence rises with sample size and stays low when thin, and that an obvious leak is flagged.
- Determinism holds trivially (counting + closed-form posteriors; no RNG). Decay uses a fixed factor.
- The Decision→Profiling wiring (and Bayesian *range* estimation beyond scalar frequencies) is the next
  step; v1 is the stats/confidence substrate that makes the existing bounded-exploit machinery real.

## Alternatives considered
- **Raw frequencies (occ/opp), no prior:** simple but wild on small samples (a 1/1 fold reads as
  100% fold-to-bet). The Beta prior + sample-size confidence is what keeps thin reads safe. Rejected.
- **Full per-spot range posteriors now:** the right long-term target, but heavy; v1 scalar frequencies
  already drive the existing exploit lever. Deferred.
