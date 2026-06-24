# ADR 0003: Engine architecture and decision-making approach

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design call), owner (GTO + opponent exploitation, determinism lean, heads-up first)

## Context
The hard, valuable part of pokerface is **deciding well**: play game-theoretically
strong poker, while **tracking opponents** (this hand, recent, long-term), building
**profiles**, and **exploiting** their leaks — otherwise reverting to a GTO baseline.
The owner wants to **lean deterministic** (computed, reproducible, explainable
strategies over black-box AI), to **document the theory and algorithm thoroughly**, and
to **probe the frontiers** where poker math is uncertain and propose approaches.

Key facts that shape the design (expanded in `docs/POKER_THEORY.md`):
- **Heads-up (2-player) is the tractable case.** It is zero-sum, so a Nash equilibrium
  is *unexploitable* and guarantees you don't lose in expectation vs. any opponent.
  Heads-up **Limit** Hold'em is essentially solved (Cepheus, CFR+, 2015); heads-up
  **No-Limit** is not exactly solved but strongly approximated (Libratus/DeepStack, 2017).
- **Multiway (3-9) loses those guarantees.** It is not pairwise zero-sum; equilibria
  aren't unique and don't guarantee non-loss. Strong play exists by *approximation*
  (Pluribus, 2019: blueprint + depth-limited search + self-play), not by a solved,
  guaranteed equilibrium. This is a genuine frontier.
- **No-Limit bet sizing is continuous/unbounded**; solvers *discretize* it (action
  abstraction), which is approximate.
- **Real NLHE is far too large to solve directly** (~10^160 states); solvers use **card
  abstraction** + **action abstraction**, solve the abstract game with **CFR**, and
  **translate** live spots into it — where exactness erodes.

## Decision
Build the engine in clearly separated layers, **deterministic where the math allows,
approximate-but-reproducible where it doesn't, and explicit about which is which.**

1. **`Core` (exact, deterministic):** the Texas Hold'em *rules engine* (2-9 players,
   blinds/antes, button, streets, legal actions, pot + side pots, showdown), a fast
   **hand evaluator**, and **equity/EV math** (exact enumeration where small, seeded
   Monte-Carlo where large). No approximation here beyond seeded sampling.
2. **`Abstraction`:** card bucketing (by equity/strength/board texture) and action/bet
   abstraction, plus **translation** of live spots into/out of the abstraction. The
   documented seam where exactness becomes approximation.
3. **`Solver`:** the **CFR family** (start with CFR+/MCCFR; Discounted/Linear CFR as
   refinements) to compute **blueprint** equilibrium strategies for abstracted games,
   plus **subgame re-solving** to sharpen decisions live with bounded added
   exploitability. Output is a reproducible strategy, not a neural black box.
4. **`Profiling`:** opponent statistics at **three horizons** (current hand, recent
   window, long-term), range estimation, and leak detection (over-folding, over-calling,
   imbalanced sizing, etc.).
5. **`Decision` (the brain):** combine the **GTO blueprint baseline** with a **bounded
   exploit adjustment** derived from `Profiling` — deviate to punish a *reliable* read,
   clamp the deviation so we don't blow up our own exploitability, and fall back to GTO
   under uncertainty. This is the policy the front end and tests call.
6. **`Cli`:** solve / benchmark / measure **exploitability** / run scenarios headless.
7. **`Table` (Raylib):** the scenario-setup front end (ADR-0004) — a *dev/test tool*.

**Sequencing:** **heads-up first** (it is the most solvable and gives unexploitability
guarantees to validate against), then generalize the same layers to multiway with the
guarantees relaxed and the approximations documented.

**Determinism contract:** same game state + same opponent model + same seed ⇒ same
decision. Monte-Carlo and live search are seeded and budget-bounded (deterministic given
the budget). Any place determinism is necessarily lost (abstraction translation,
multiway non-uniqueness) is named in `docs/DECISION_ALGORITHM.md`. **AI/ML decision
methods are an explicitly optional research track for the frontier problems, not the
baseline** — the baseline is explainable, reproducible poker theory.

## Consequences
- A clean split between *exact* (rules, evaluation, equity), *reproducible-approximate*
  (CFR blueprints, re-solving), and *frontier* (multiway, adaptive exploitation) work —
  each documented as such.
- The unexploitability guarantee in heads-up gives a hard correctness target
  (measure exploitability in milli-big-blinds/100) to validate the solver before
  trusting it multiway.
- Real NLHE needs abstraction; we accept translation error and measure it rather than
  pretend to exactness.
- The exploit layer must be *bounded* — unbounded best-response to a mis-estimated
  opponent is itself highly exploitable. We cap deviation by read-confidence.
- Heavy compute (CFR) is a real cost; we start small (heads-up, coarse abstraction) and
  scale, profiling before optimizing.

## Alternatives considered
- **Pure neural / RL agent (black box):** can be strong, but opaque, non-deterministic,
  and hard to document/trust — against the owner's determinism lean. Kept only as an
  optional frontier experiment.
- **Pure GTO, no exploitation:** unexploitable but leaves money on the table vs. weak
  fields; the owner explicitly wants opponent profiling + exploitation. Rejected as the
  whole story (it remains the *baseline* to deviate from).
- **Pure exploitative (no GTO baseline):** maximally profitable vs. a known weak
  opponent but wildly exploitable itself and fragile to adaptation. Rejected as the
  baseline; lives as the bounded deviation layer.
- **Solve the full game directly (no abstraction):** intractable for NLHE. Rejected.
