# pokerface — Architecture

> Living document. The deep material is split into [`POKER_THEORY.md`](POKER_THEORY.md)
> (plain language) and [`DECISION_ALGORITHM.md`](DECISION_ALGORITHM.md) (technical); this
> is the system-level overview.

## Overview
pokerface is a **No-Limit Texas Hold'em decision engine** (2–9 players, **heads-up
first**) with a small **Raylib scenario front end** for setting up and interrogating
spots. The engine plays a **game-theoretically strong (GTO) baseline** and **deviates to
exploit** profiled opponents, leaning **deterministic and explainable** over black-box AI.

## Context & goals
- **Goals:** make high-quality poker decisions with EV/equity/range reasoning you can
  inspect; track opponents over three horizons and exploit leaks within bounded risk;
  document the theory and algorithm thoroughly; probe the unsolved frontiers.
- **Non-goals (now):** a polished playable poker product, online/multiplayer, real money,
  GUI animation. The Raylib app is a dev/test tool only (ADR-0004).
- **Constraints:** C#/.NET 10 (ADR-0002); deterministic + reproducible (seeded);
  engine is UI-agnostic.

## Components
| Component | Responsibility | Exactness |
|---|---|---|
| `Core` | Rules/state, legal actions, pots+side-pots, showdown; hand eval; equity/EV | Exact / seeded |
| `Abstraction` | Card + action abstraction & translation | Approximate (named seam) |
| `Solver` | CFR family → blueprint; subgame re-solving; exploitability | Reproducible-approx |
| `Profiling` | Opponent stats (hand/recent/lifetime), range estimate, leaks | Estimate |
| `Decision` | GTO baseline + bounded exploit → action (+ explanation) | Policy |
| `Cli` | Solve / benchmark / measure exploitability / run scenarios | — |
| `Table` (Raylib) | Drag-and-drop scenario setup + engine readout | — |

## Data & control flow
A decision: `GameState` → legal actions (`Core`) → abstract the spot (`Abstraction`) →
baseline strategy via blueprint / re-solve (`Solver`) → opponent range + confidence
(`Profiling`) → bounded exploit blend (`Decision`) → **action + EV/equity/range/why**.
Same `(state, opponentModel, seed)` ⇒ same result. Full pipeline in DECISION_ALGORITHM §1.

## Status (what's built)
`Core` (cards/deck/PRNG, hand evaluator, equity, 169-hand abstraction, betting/state engine
with side pots), `Solver` (CFR+ validated on Kuhn), `Decision` (heads-up v1: SAGE/Chen preflop,
equity-vs-pot-odds postflop, bounded exploitation), and `Cli` (decide/equity/kuhn/demo) are
implemented and tested. `Abstraction` and `Profiling` are still planned (the `Decision` layer
uses equity-vs-random and an `OpponentModel` stub until they land). The `Table` Raylib tool is a
dev scenario viewer. Build/test instructions: [`DEVELOPMENT.md`](DEVELOPMENT.md).

## Key decisions
- **ADR-0001** — Record architecture decisions.
- **ADR-0002** — Stack: C#/.NET 10 + Raylib (`Raylib-cs`) + xUnit.
- **ADR-0003** — Engine architecture & decision approach (exact core / CFR baseline /
  bounded exploitation / heads-up first / determinism lean).
- **ADR-0004** — Raylib front end is a scoped dev/scenario tool, not a product.
- **ADR-0005** — Testing under Windows Smart App Control (xUnit v3, sources compiled into the
  test assembly; run via `./test.ps1`, not `dotnet test`).
- **ADR-0006** — "Deterministic" means no AI/ML in the decision path *and* seed-reproducible,
  but mixed (pseudorandom) strategies are expected and embraced.

## Risks & open questions
- **Multiway has no guaranteed solution** — we approximate and label it (THEORY §5).
- **Abstraction translation error** — measured, not assumed away.
- **Exploit vs. counter-exploit** — deviation is confidence-capped; needs tuning.
- **Compute cost of CFR** — start small (heads-up, coarse), profile before scaling.
- The live worklist is `docs/STUDIO_BACKLOG.md`.
