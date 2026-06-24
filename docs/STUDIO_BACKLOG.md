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
- [ ] **Research pass on poker GTO** (Lead-led; delegate reading/summaries to Qwen). Survey
  CFR / CFR+ / MCCFR / Discounted-CFR, card + action abstraction, subgame/continual
  re-solving, exploitability (mbb/g), and the milestone agents (Cepheus, Libratus,
  DeepStack, Pluribus), plus ICM. **Deepen `POKER_THEORY.md` and `DECISION_ALGORITHM.md`
  with citations and concrete parameter choices.** Capture open frontiers (DECISION §8).
- [ ] **Scaffold the solution** (`PokerEngine.slnx`): `Core` + `Tests` first; stubs for
  `Abstraction`/`Solver`/`Profiling`/`Decision`/`Cli`. (Card/Suit/Rank/Deck + seeded PRNG
  may already exist — verify and build on them.)
- [ ] **Hand evaluator** (`Core.Eval`): correct 5–7 card ranking; exhaustive tests (all
  hand classes, best-of-7). Optimize to a lookup evaluator only once profiled hot.
- [ ] **Game state + betting engine** (`Core`): 2–9 players, blinds/antes, button, streets,
  legal actions, **pot + side pots** (all-in math), showdown. Property/edge-case tests.
- [ ] **Equity / EV** (`Core.Equity`): exact enumeration where small, seeded Monte-Carlo
  where large; range-vs-range with card-removal. Reproducibility test (same seed ⇒ same).

## Next  (the decision engine — heads-up)
- [ ] **Abstraction** (`Abstraction`): coarse card buckets + a small bet-size set +
  translation. Document the error as the named approximation seam.
- [ ] **CFR+ solver** (`Solver`) on a tiny heads-up game (e.g. a toy/Kuhn/Leduc first to
  validate convergence), then heads-up Hold'em on the abstraction. **Measure exploitability.**
- [ ] **Subgame re-solving** (safe; budget-bounded) to sharpen live decisions.
- [ ] **Profiling** (`Profiling`): opponent stats at hand/recent/lifetime horizons; Bayesian
  range estimate; read-confidence `c`.
- [ ] **Decision policy** (`Decision`): GTO baseline + bounded exploit blend (`w=f(c)`,
  capped `w_max`); emits action + EV/equity/range/why. Tests: `w→0` ⇒ GTO; beats a scripted
  leaky opponent for more than GTO; never exceeds the cap.
- [ ] **Raylib scenario `Table`** (ADR-0004): poker table, 52-card palette (4 suit rows),
  drag cards to holes/board, edit stacks/bets/pot/blinds/button/action, show the engine's
  recommendation + reasoning. Tests never reference this project.

## Later  (multiway + frontiers)
- [ ] Generalize the layers to **multiway (3–9)** via MCCFR self-play blueprint +
  depth-limited search; label it an approximation (no guarantee). Aggregated-field option.
- [ ] **ICM** support for tournament/payout-aware decisions.
- [ ] **Probe the frontiers** (DECISION §8): continuous sizing, adaptive opponents,
  abstraction error, real-time budget — try compute / theory / (flagged) AI approaches and
  compare to the deterministic baseline by exploitability + EV.
- [ ] Performance: profile, then a lookup hand-evaluator / `Span`/SIMD on proven hot paths.
- [ ] Hand-history import to seed long-term opponent profiles.
