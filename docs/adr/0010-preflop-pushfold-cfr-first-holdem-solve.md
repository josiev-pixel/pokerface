# ADR 0010: Heads-up preflop push/fold via CFR — the first real-Hold'em solve

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design)

## Context
The solver is validated on Kuhn and Leduc (ADR-0009), and we have exact Hold'em equity
(`Core.Equity`), the 169-bucket abstraction (`StartingHand`), range-vs-range equity
(`RangeEquity`), and reusable exploitability (`Solver.BestResponse`). The next milestone is the
first solve of an actual Hold'em situation. Full multi-street NLHE on an abstraction is large and
compute-heavy; the right *first* slice is the **short-stack heads-up preflop push/fold game**,
which is (a) genuinely solved poker theory, (b) tiny once abstracted, and (c) lets us validate the
whole pipeline against an independent reference — the SAGE heuristic and near-zero exploitability.

The key to tractability is to avoid dealing ~1.6M specific combo pairs through the CFR tree. Instead
we collapse cards to the **169 preflop buckets** and precompute a **169×169 equity matrix** with
`RangeEquity` (bucket-vs-bucket, blocker-aware). The push/fold game is then a small matrix game:
chance picks a (SB-bucket, BB-bucket) pair with the right weight, and all-in payoffs read straight
from the matrix. CFR+ solves it in milliseconds.

## Decision
Add `HoldemPushFold` as an `ICfrGame` in `PokerEngine.Solver` (it may reference `Core` for the
bucket/equity types; if a project-reference is undesirable, inject the precomputed equity matrix +
bucket weights so the Solver stays Core-independent — preferred). Parameters: effective stack `S`
(big blinds), small blind 0.5, big blind 1.

- **Chance:** deal a (SB bucket *i*, BB bucket *j*) pair with weight proportional to the number of
  non-conflicting combo pairs for (*i*, *j*) (so blockers and combo multiplicities are respected).
- **SB decision** (info set = own bucket *i*): **jam** all-in for `S`, or **fold** (loses the small
  blind). 169 info sets.
- **BB decision** (info set = own bucket *j*, given SB jammed): **call** (showdown) or **fold**
  (loses the big blind). 169 info sets.
- **Payoffs** (chips, zero-sum, relative to SB): fold by SB → −0.5 (BB +0.5). SB jam, BB fold →
  +1 (SB wins BB's blind). SB jam, BB call → all-in showdown for a pot of `2S`; SB net EV
  `= equity[i,j]·2S − S` using the precomputed matrix (equity already includes ties). 

Provide a small builder that precomputes the 169×169 equity matrix and the weight matrix once
(seeded `RangeEquity`, modest samples), so the game construction is reproducible and fast to re-use.

**Validation** (tests): solve with CFR+; (1) **exploitability** via `BestResponse` is near zero
(it's a 2p zero-sum equilibrium); (2) the solved SB jam frequency is **monotone-ish in stack** and
**broadly agrees with SAGE** at a couple of stacks (e.g. premium buckets jam at all tested stacks,
72o folds at 10bb; the overall jam % is in the right ballpark — assert ranges, not exact cells,
since SAGE is itself an approximation); (3) determinism. Expose a CLI `pushfold` command to print
the solved jam/call ranges and exploitability.

## Consequences
- First end-to-end real-Hold'em solve, tying together equity + abstraction + CFR + exploitability.
- Gives a *computed* push/fold blueprint the `Decision` layer can later use in place of (or to
  validate) the SAGE heuristic short-stacked.
- The 169×169 equity matrix precompute is the main cost; seeded and cacheable. Bucket-level
  abstraction means the all-in EV is an average over combos — a small, documented approximation
  (exact combo-level dealing is a later refinement if exploitability demands it).
- Sets the template for the harder multi-street abstracted solve (next: add a flop with a small
  bet-size set via `BetSizeSet`, measuring translation error).

## Alternatives considered
- **Deal exact combos through the tree:** exact but ~1.6M chance leaves × thousands of iterations —
  too slow for a test and unnecessary at this fidelity. Bucket matrix is the standard reduction.
- **Skip CFR, just trust SAGE:** SAGE is an approximation; the point is to *compute* the equilibrium
  and *measure* it, which is the project's whole thesis. Rejected.
