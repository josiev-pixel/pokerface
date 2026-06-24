# Studio backlog

Open items are `- [ ]`, done are `- [x]`. The studio lead works these top-to-bottom,
delegates implementation + tests to the workers, reviews, and commits at each green
checkpoint on the current branch. The Director can reorder anytime (even from the phone).

**Standing rules for all items** (enforce on every worker prompt):
- C#/.NET, clean and well-factored, tests for the logic. Build stays green (warnings as
  errors in engine libraries).
- **Determinism:** one seeded PRNG owns all randomness; never `System.Random` or
  wall-clock seeds in Core/Decision; no result-affecting unordered iteration. Same
  `(state, opponentModel, seed)` ⇒ same action.
- **Exact vs. approximate is explicit** and documented (rules/eval/equity exact; CFR/
  abstraction reproducible-approx; multiway labeled an approximation).
- **The Lead owns the poker theory and the math/tuning** (CFR params, abstraction
  granularity, exploit cap `w_max`, bet-size sets). Workers wire systems + tests.
- **Keep `docs/POKER_THEORY.md` + `docs/DECISION_ALGORITHM.md` in lockstep with the code**;
  flip module-map entries from *(planned)* to the real type as they land. ADR + update
  ARCHITECTURE.md for load-bearing decisions.
- Front end: the Raylib `Table` is a **dev/scenario tool only** (ADR-0004) — no product UI.

## Now  (research the theory, then stand up the exact core — heads-up first)
- [x] **Research pass on poker GTO.** Surveyed CFR/CFR+/Kuhn, SAGE & Nash push/fold, Chen,
  exploitability (mbb/g) + Cepheus, MDF/alpha; deepened `POKER_THEORY.md` (§7) and
  `DECISION_ALGORITHM.md` (§0, §9, §11 with citations and concrete parameters).
- [x] **Scaffold the solution** (`PokerEngine.slnx`): `Core`, `Decision`, `Solver`, `Cli`,
  `Table`, `Tests`. (Card/Suit/Rank/Deck + seeded PRNG existed; built on them.) Test loop
  works around Smart App Control (ADR-0005, `./test.ps1`).
- [x] **Hand evaluator** (`Core.Eval.HandEvaluator`): 5–7 card ranking; verified by the full
  2,598,960-hand census against known category counts.
- [x] **Game state + betting engine** (`Core.Game`): 2–9 players, blinds/antes, button,
  streets, legal actions, min-raise/short-all-in reopening, **pot + side pots**, showdown.
- [x] **Equity / EV** (`Core.Equity.EquityCalculator`): exact enumeration where small, seeded
  Monte-Carlo where large; vs-specific and vs-random; reproducible by seed.
  *(range-vs-range with card-removal still TODO — see Profiling.)*

## Next  (the decision engine — heads-up)
- [ ] **Abstraction** (`Abstraction`): coarse card buckets + a small bet-size set +
  translation. Document the error as the named approximation seam.
- [~] **CFR+ solver** (`Solver.CfrPlusSolver`): **done & validated on Kuhn** (converges to the
  α-family, value −1/18, exploitability < 0.001 chips/hand). Still TODO: Leduc, then heads-up
  Hold'em on the abstraction, and measuring exploitability there.
- [ ] **Subgame re-solving** (safe; budget-bounded) to sharpen live decisions.
- [~] **Profiling** (`Profiling`): opponent stats at hand/recent/lifetime horizons; Bayesian
  range estimate; read-confidence `c`. *(Only the `Decision.OpponentModel` stub + read
  confidence exists today; the real three-horizon profiler is TODO.)*
- [x] **Decision policy** (`Decision.DecisionEngine`, heads-up v1): baseline (SAGE/Chen preflop,
  equity-vs-pot-odds postflop with seeded mixing) + bounded exploit blend (`w=f(c)`, capped
  `w_max`); emits action + EV/equity/strategy/why. Tested: `w→0` ⇒ baseline, over-folder gets
  bluffed more, cap respected. *(Baseline is heuristic, not yet a CFR solve — DECISION §0.)*
- [~] **Raylib scenario `Table`** (ADR-0004): v1 is a scenario **viewer** (felt table, drawn
  cards, engine readout, cycle/re-seed). Drag-and-drop card palette + editable stacks/bets TODO.

## Later  (multiway + frontiers)
- [ ] Generalize the layers to **multiway (3–9)** via MCCFR self-play blueprint +
  depth-limited search; label it an approximation (no guarantee). Aggregated-field option.
- [ ] **ICM** support for tournament/payout-aware decisions.
- [ ] **Probe the frontiers** (DECISION §8): continuous sizing, adaptive opponents,
  abstraction error, real-time budget — try compute / theory / (flagged) AI approaches and
  compare to the deterministic baseline by exploitability + EV.
- [ ] Performance: profile, then a lookup hand-evaluator / `Span`/SIMD on proven hot paths.
- [ ] Hand-history import to seed long-term opponent profiles.
