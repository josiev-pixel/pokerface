# Decision Algorithm — Technical Reference

> The technical companion to [`POKER_THEORY.md`](POKER_THEORY.md). It specifies the
> decision pipeline, the algorithms, and **which module/type owns each problem**, so the
> code and the theory stay in lockstep. Modules marked *(planned)* don't exist yet; as
> they land, replace the note with the concrete type and keep this doc honest. Design
> basis: [ADR-0003](adr/0003-engine-architecture-and-decision-approach.md).

## 0. Module map (problem → where it's handled)
| Concern | Module *(C# project)* | Exactness |
|---|---|---|
| Rules, state, legal actions, pots/side-pots, showdown | `PokerEngine.Core` *(planned)* | **Exact** |
| Hand evaluation (5–7 cards) | `PokerEngine.Core.Eval` *(planned)* | **Exact** |
| Equity / EV (enumeration or seeded Monte-Carlo) | `PokerEngine.Core.Equity` *(planned)* | Exact / seeded-approx |
| Card + action abstraction, translation | `PokerEngine.Abstraction` *(planned)* | **Approximate** (named seam) |
| CFR solving → blueprint; subgame re-solve | `PokerEngine.Solver` *(planned)* | Reproducible-approx |
| Opponent stats, range estimation, leak detection | `PokerEngine.Profiling` *(planned)* | Estimate |
| GTO baseline + bounded exploit → action | `PokerEngine.Decision` *(planned)* | Policy |
| Solve/benchmark/exploitability/scenarios | `PokerEngine.Cli` *(planned)* | — |
| Scenario drag-and-drop table | `PokerEngine.Table` (Raylib) *(planned, [ADR-0004](adr/0004-raylib-scenario-tool-is-a-dev-tool.md))* | — |

## 1. The decision pipeline
For a live spot, `Decision.ChooseAction(state, opponentModel, seed)` runs:

```
state ──▶ [legal actions]            (Core)
      ──▶ [abstract the spot]        (Abstraction): bucket our range + board, discretize sizes
      ──▶ [baseline strategy σ*]     (Solver): blueprint lookup, optionally subgame re-solve
      ──▶ [opponent estimate]        (Profiling): range + leaks + read-confidence c∈[0,1]
      ──▶ [bounded exploit]          (Decision): σ = (1−w)·σ*  +  w·BR(opponentRange),  w = f(c)
      ──▶ action  (+ EV, equity, range, w, why)   ← always explainable
```
Under low confidence `w→0` (play GTO); under strong, repeated evidence `w` grows but is
**capped** so our own exploitability stays bounded (§6).

## 2. Exact core (no approximation beyond seeded sampling)
- **State & rules** (`Core`): players, stacks, blinds/antes, button, street, action
  pointer, legal-action generation, **pot + side pots** (correct all-in math), showdown.
  Deterministic; pure functions over an immutable `GameState`.
- **Hand evaluator** (`Core.Eval`): rank any 5–7 card hand to a comparable strength.
  Start correct-and-simple; optimize to a perfect-hash / lookup evaluator (e.g.
  Cactus-Kev / two-plus-two style) once profiling shows it's hot. Pinned by exhaustive
  tests (every 5-card hand class, 7-card best-of).
- **Equity / EV** (`Core.Equity`): exact **enumeration** when the remaining outcome space
  is small (e.g. river, or few opponents on the turn); **seeded Monte-Carlo** when large.
  Seed in → identical equity out (reproducible). Range-vs-range equity by enumerating the
  cross-product (with card-removal/blocker effects).

## 3. CFR — computing the GTO baseline
Hidden-information games are solved with **Counterfactual Regret Minimization** and its
descendants. The engine computes a strategy that converges to an **ε-Nash equilibrium**.

- **Regret matching:** at each information set *I* (everything a player knows at a
  decision point), track **counterfactual regret** `R(I,a)` for each action — how much
  better we'd have done always playing *a*. The next strategy plays actions in proportion
  to positive regret: `σ(I,a) = R⁺(I,a) / Σ_b R⁺(I,b)`.
- **Average strategy converges, not the current one:** the running **average** of σ over
  all iterations is what approaches equilibrium. We store and return the average.
- **Variants** (`Solver`), in the order we'll adopt them:
  - **CFR+** — regret-matching⁺ (floors regrets at 0) + linear averaging; far faster,
    deterministic; the algorithm that solved heads-up Limit (Cepheus). Our first solver.
  - **MCCFR** (outcome/external sampling) — samples the tree instead of full traversal;
    needed once the game is too big to sweep. Sampling is **seeded** → reproducible.
  - **Discounted/Linear CFR** — discount early regrets; a strong convergence-rate upgrade.
- **Convergence measure = exploitability.** Compute the **best response** to our average
  strategy; its value (in mbb/g) is how exploitable we are. Heads-up target: drive it
  toward ~0 and *report it*. This is the engine's hard correctness signal (§8).

## 4. Abstraction & translation — the named approximation seam
Real NLHE (~10^160 states) can't be solved directly, so `Abstraction` shrinks it, and we
**accept and measure** the error:
- **Card abstraction:** bucket strategically-similar hands (by equity distribution /
  potential / board texture — e.g. EHS or histogram clustering). Coarse first, refine later.
- **Action abstraction:** allow a small set of bet sizes (e.g. {⅓, ½, ¾, 1×, 2× pot,
  all-in}); the continuous bet dial collapses to these.
- **Translation:** map a *real* bet (say 0.6× pot) to the nearest abstract size for
  lookup, and map the abstract decision back. Translation error is exploitable; we keep
  the size set rich enough where it matters and **prefer live re-solving (§5) for big
  spots.** This module is the single documented place where "exact" becomes "approximate."

## 5. Subgame re-solving — sharpening live
A blueprint over an abstract game is blunt. Following Libratus/DeepStack, `Solver`/`Decision`
can **re-solve the current subgame** at decision time on a finer abstraction, seeded by the
blueprint's reach/values. Use **safe (re-)solving**: constrain the re-solve so it provably
**does not increase exploitability** vs. the blueprint. Budget-bounded (fixed iterations/
time) → deterministic given the budget.

## 6. Opponent modeling & bounded exploitation
- **Stats** (`Profiling`) at three horizons (hand / recent window / lifetime): VPIP, PFR,
  3-bet%, fold-to-cbet, aggression frequency, sizing tells, street-by-street fold/call/raise
  frequencies. Stored per opponent id, decaying older recent-window data.
- **Range & strategy estimate:** start from the GTO range for the line, then **Bayesian-
  update** toward the observed frequencies (a Dirichlet/Beta prior over action
  frequencies → posterior as hands accrue). Few hands → posterior ≈ GTO prior; many hands
  → posterior ≈ observed. The prior's strength encodes our default trust in GTO.
- **Read confidence** `c∈[0,1]`: grows with sample size and consistency of the leak
  (e.g. via the posterior's concentration). Feeds the blend weight `w = f(c)` capped at
  `w_max < 1`.
- **Exploit = bounded best response:** `BR(opponentRange)` is the max-EV counter-strategy
  to the *estimated* opponent. We **blend** `σ = (1−w)σ* + w·BR`, never play raw BR — a
  best response to a *wrong* model is itself wildly exploitable. The cap `w_max` is the
  knob trading "punish leaks harder" against "stay safe."

## 7. Multiway (3–9): approximation, with guarantees relaxed
Per ADR-0003/POKER_THEORY §5, multiway has no guaranteed-strong tractable solution. The
same layers generalize, but:
- The "equilibrium" is an MCCFR **self-play blueprint** (Pluribus-style), **not** an
  unexploitable solution — labeled as such in output.
- Depth-limited search with a small set of continuation strategies for the other players.
- Optionally model "the field" (all other players) as one aggregated opponent
  distribution to keep the subgame tractable.
- We **do not** claim heads-up's non-loss guarantee here; the engine surfaces a
  confidence/"approximation" flag so callers know.

## 8. Where the math is uncertain → concrete approaches
| Open problem | Compute approach | Theory approach | AI approach (optional) |
|---|---|---|---|
| Continuous bet sizing | richer size sets, finer near key nodes | size-translation bounds; pseudo-harmonic mapping | learned sizing head |
| Multiway equilibrium | bigger MCCFR self-play | aggregated-field reductions; correlated-eq notions | self-play RL (Pluribus-like) |
| Adaptive opponents | re-estimate online | bounded-deviation / regret vs. shifting target | recursive opponent models |
| Abstraction error | finer buckets + live re-solve | safe re-solving guarantees | learned card embeddings |
| Real-time budget | blueprint + targeted deep search | depth-limited values | counterfactual value nets (DeepStack-style) |

**Determinism stance:** prefer the compute + theory columns (reproducible, explainable);
treat the AI column as **labeled experiments** behind a flag, compared head-to-head
against the deterministic baseline by exploitability and EV, never silently swapped in.

## 9. Determinism & reproducibility contract
- A single seeded PRNG (`Core`) drives every stochastic step (MC equity, MCCFR sampling,
  any mixed-strategy action selection). **Same (state, opponentModel, seed) ⇒ same
  action and same EV.**
- Vanilla CFR/CFR+ are deterministic; MCCFR is deterministic *given the seed*.
- Mixed strategies (σ assigns probabilities) are reported as the full distribution; when
  a single action must be emitted, it's sampled from σ with the seed, or the doc/flag
  lets callers take the argmax for a fully deterministic line.
- Sources of *non*-reproducibility are disallowed in Core/Decision: no `System.Random`,
  no wall-clock-seeded RNG, no unordered-collection iteration affecting results.

## 10. Validation
- **Exploitability** (best-response value, mbb/g) is the north-star metric for the
  GTO baseline — computed by `Cli`, regression-tested as the solver improves.
- **Rules/eval correctness:** exhaustive unit tests (hand ranking, side-pot math, legal
  actions, showdown) — these must be exact.
- **Exploit layer:** tests that `w→0` reproduces the GTO baseline, that a clearly-leaky
  scripted opponent is beaten for more than GTO would win, and that the blend never
  exceeds `w_max`.
- **Determinism:** same-seed runs are byte-identical; this is a test, not a hope.
