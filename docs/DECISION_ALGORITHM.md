# Decision Algorithm — Technical Reference

> The technical companion to [`POKER_THEORY.md`](POKER_THEORY.md). It specifies the
> decision pipeline, the algorithms, and **which module/type owns each problem**, so the
> code and the theory stay in lockstep. Modules marked *(planned)* don't exist yet; as
> they land, replace the note with the concrete type and keep this doc honest. Design
> basis: [ADR-0003](adr/0003-engine-architecture-and-decision-approach.md).

## 0. Module map (problem → where it's handled)
*(built)* = lives in code today; *(planned)* = not yet. Keep this honest as modules land.

| Concern | Type *(C# project)* | Exactness |
|---|---|---|
| Rules, state, legal actions, pots/side-pots, showdown | `Game.GameState` + `Game.PotResolver` *(built, Core)* | **Exact** |
| Hand evaluation (5–7 cards) | `Eval.HandEvaluator` *(built, Core)* | **Exact** |
| Equity / EV (enumeration or seeded Monte-Carlo) | `Equity.EquityCalculator` *(built, Core)* | Exact / seeded-approx |
| Preflop hand abstraction (the 169 buckets) | `StartingHand`, `HoleCards` *(built, Core)* | **Exact** |
| Card + action abstraction, translation | `PokerEngine.Abstraction` *(planned)* | **Approximate** (named seam) |
| CFR solving → blueprint; subgame re-solve | `Solver.CfrPlusSolver` *(built; validated on Kuhn)* | Reproducible-approx |
| Opponent stats, range estimation, leak detection | `PokerEngine.Profiling` *(planned)*; `Decision.OpponentModel` is the v1 stub | Estimate |
| GTO/heuristic baseline + bounded exploit → action | `Decision.DecisionEngine` *(built, heads-up v1)* | Policy |
| Settled poker arithmetic (pot odds, MDF, alpha, EV) | `Decision.PokerMath` *(built)* | **Exact** |
| Solve/benchmark/exploitability/scenarios | `PokerEngine.Cli` *(built: decide/equity/kuhn/demo)* | — |
| Scenario table | `PokerEngine.Table` (Raylib) *(dev tool, [ADR-0004](adr/0004-raylib-scenario-tool-is-a-dev-tool.md))* | — |

**Honesty on the v1 `Decision` baseline:** it is a *principled heuristic*, not a CFR solve.
Preflop uses the SAGE push/fold system short-stacked and the Chen score deeper; postflop uses
equity-vs-pot-odds with MDF/alpha-aware mixing (§11). The CFR machinery is proven separately on
Kuhn (`Solver`) and will replace these heuristics street by street as the abstraction lands.

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
Two distinct guarantees (see POKER_THEORY §7), often conflated:

- **No AI/ML in the decision path.** Decisions come from explainable poker math (equity, pot
  odds, CFR, opponent frequencies), never a neural net or LLM. Learned methods are a
  flag-gated research track (§8), benchmarked against this baseline, never silently swapped in.
- **Mixing is expected, and that's fine.** Equilibrium poker is *randomized* — the engine
  deliberately plays "do X p% of the time, Y the rest" (`DecisionEngine` samples from a mixed
  strategy σ; `DecisionResult.Strategy` reports the whole distribution). This pseudorandom
  behavior is a feature, required for unexploitability — not a breach of determinism.
- **Reproducibility ties it together.** A single seeded PRNG (`Core.DeterministicRandom`)
  drives every stochastic step (MC equity, MCCFR sampling, mixed-strategy selection). **Same
  (state, opponentModel, seed) ⇒ same action and same EV.**
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

## 11. Baseline policy parameters (v1) and their sources
The Lead owns these numbers; they are starting points to be tuned against measured
exploitability/EV, not solved values. Implemented in `Decision.EngineConfig`,
`Decision.SageSystem`, `Decision.ChenFormula`, `Decision.PokerMath`.

**Settled arithmetic** (exact; `PokerMath`):
- Pot-odds break-even equity `= toCall / (pot + toCall)`.
- Minimum Defense Frequency `MDF = pot / (pot + bet)`; bluff/required-fold `alpha = bet / (pot + bet)`;
  `alpha = 1 − MDF`. (E.g. a pot-sized bet → MDF 50%, alpha 50%; half-pot → MDF 67%, alpha 33%.)

**Preflop, short stack (≤ 10 BB) — SAGE push/fold** (Kittock & Jones, *Card Player* 2006).
Power Index `= 2·high + low (+22 pair, +2 suited)` with card values 2–10 face, J=11, Q=12,
K=13, **A=15**. Shove if PI ≥ push threshold; BB calls if PI ≥ call threshold, by whole-BB row:

| eff BB | 1 | 2 | 3 | 4 | 5 | 6 | 7+ |
|---|---|---|---|---|---|---|---|
| push ≥ | 17 | 21 | 22 | 23 | 24 | 25 | 26 |
| call ≥ | any | 17 | 24 | 26 | 28 | 29 | 30 |

This is a deterministic, near-Nash approximation; full chip-EV Nash push/fold tables (and ICM
adjustment) are a planned refinement — note "SAGE" is *Sit-And-Go Endgame*, distinct from the
separate Sklansky–Anderson chart.

**Preflop, deeper — Chen score** (Bill Chen). High-card pts A=10/K=8/Q=7/J=6/else rank÷2; pair
= 2× pts (min 5); +2 suited; gap penalty {0,1,2,4,5}; +1 straight bonus if gap ≤ 1 and high < Q;
round halves up. Defaults: open if ≥ `OpenChen` (4), call a raise if ≥ `CallRaiseChen` (6),
3-bet if ≥ `ThreeBetChen` (9).

**Postflop** (heuristic, `DecisionEngine`): equity (vs. a random hand for now — range-vs-range
is the Profiling upgrade) bucketed against pot odds; value-bet ≥ `ValueBetEquity` (0.62), bluff
candidates ≤ `BluffCeilingEquity` (0.38) at `BluffFreq` (33%) balanced by alpha, raise for value
≥ `RaiseEquity` (0.70); facing a bet, call when equity ≥ pot odds, else bluff-catch toward MDF.
All "p% of the time" choices are seeded mixes (§9).

**Bounded exploitation:** `w = min(w_max, confidence)`, `w_max = MaxExploitWeight` (0.5).
`σ = (1−w)·σ* + w·σ_exploit`; `w → 0` reproduces the baseline. The v1 lever is the opponent's
fold-to-bet frequency vs. alpha (over-folder ⇒ bluff more; calling-station ⇒ bluff less).

**Validation milestones / numbers:** CFR+ on Kuhn converges to game value −1/18 with
exploitability < 0.001 chips/hand (`Solver`); Cepheus solved heads-up *limit* to 0.986 mbb/g
(Bowling et al., *Science* 2015) — the standard the exploitability metric is measured against.

### Sources
- SAGE: Kittock & Jones, *Card Player* Vol. 19 No. 2 (2006). Nash push/fold tables:
  holdemresources.net/hune, primedope.com/equilibrium-pushbot-charts.
- Chen formula & Sklansky–Malmuth groups: thepokerbank.com; en.wikipedia.org/wiki/Texas_hold_'em_starting_hands.
- CFR/CFR+ & Kuhn: Neller & Lanctot CFR tutorial (modelai.gettysburg.edu/2013/cfr/cfr.pdf);
  Burch et al., *Revisiting CFR+ and Alternating Updates* (arXiv:1810.11542); en.wikipedia.org/wiki/Kuhn_poker.
- Exploitability / Cepheus: Bowling, Burch, Johanson, Tammelin, *Science* 2015;
  en.wikipedia.org/wiki/Cepheus_(poker_bot).
- Pot odds / MDF / alpha: blog.gtowizard.com/mdf-alpha; en.wikipedia.org/wiki/Pot_odds.
