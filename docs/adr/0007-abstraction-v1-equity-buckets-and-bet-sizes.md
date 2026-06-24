# ADR 0007: Abstraction v1 — equity buckets, a small bet-size set, pseudo-harmonic translation

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design)

## Context
Real NLHE (~10^160 states) can't be solved directly. The `Abstraction` layer is the single
documented seam where "exact" becomes "approximate" (DECISION_ALGORITHM §4): it shrinks the game
so CFR can solve it, then translates live spots in and out. We need a v1 that is coarse, cheap,
deterministic, and honest about its error — refinements (finer buckets, learned features) come later.

## Decision
Three small, composable pieces in `PokerEngine.Abstraction`, all deterministic:

1. **Card abstraction = equity buckets.** `ICardAbstraction { int BucketCount; int Bucket(hole, board); }`
   with v1 impl `EquityBucketer(int buckets, int samples, ulong seed)`: compute equity-vs-a-random-hand
   via `Core.Equity.EquityCalculator.HeadsUpVsRandom` (fixed seed + sample count → reproducible) and
   map to `clamp(floor(equity · buckets), 0, buckets−1)`. Bucket 0 = weakest, `buckets−1` = strongest.
   The same mechanism covers every street (empty board = preflop), so we don't special-case the 169
   preflop buckets — though `StartingHand` remains the exact preflop key. This is a strength-only
   (E[HS]) abstraction; potential/draw-aware histogram bucketing is a later refinement.

2. **Action abstraction = a small pot-fraction bet set.** `BetSizeSet` holds an ordered set of bet
   sizes as fractions of the pot (default {⅓, ½, ¾, 1, 1½}) plus all-in, and resolves them to integer
   chip amounts given the pot and the acting stack (clamped to the stack; all-in is always available).

3. **Translation = nearest + pseudo-harmonic.** `BetSizeTranslator` maps a *real* bet (as a pot
   fraction) into the abstract set. Two modes: **nearest** (min absolute distance — simple,
   deterministic, but exploitable at the midpoints) and **pseudo-harmonic** (Ganzfried–Sandholm): for
   a real size `x` between adjacent abstract sizes `A < x < B`, map up to `B` with probability
   `f = (x − A)(1 + B) / ((B − A)(1 + x))`, else down to `A` — a randomized mapping that is much
   harder to exploit by "bet-sizing between the pegs." The randomized pick is drawn from the engine's
   seeded PRNG (so it stays reproducible); the probability itself is exposed for inspection/testing.

## Consequences
- Exactness erodes only here, by design and on the record: equity bucketing discards card detail and
  size translation rounds the continuous bet dial. Both errors are measurable later via exploitability.
- Equity bucketing costs a Monte-Carlo equity call per lookup; fine for solving/abstraction, and the
  seed/sample count are fixed so results are reproducible. A precomputed bucket table is a later
  optimization once the solver drives enough lookups to make it hot.
- The solver (`CfrPlusSolver`) will consume `ICardAbstraction` + `BetSizeSet` to build the heads-up
  Hold'em game tree; `BetSizeTranslator` maps live spots onto the solved blueprint. Those wirings are
  follow-up backlog items, not part of this ADR.

## Alternatives considered
- **169 preflop buckets + per-street hand-strength tiers, no equity calc:** cheaper but ad hoc and
  street-inconsistent; equity bucketing is uniform and principled. Kept `StartingHand` as the exact key.
- **Histogram / potential-aware clustering (EHS², OCHS):** stronger, but heavier and not needed for a
  first solvable abstraction; flagged as the refinement path.
- **Nearest-only translation:** simplest, but exploitable at midpoints; we include it but default the
  exploit-resistant pseudo-harmonic mapping.
