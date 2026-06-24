# ADR 0002: Language and tooling stack

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design call), owner (poker engine + Raylib scenario tool)

## Context
pokerface is a **Texas Hold'em decision engine** (the priority) plus a small **Raylib
scenario front end** for setting up and testing situations. The workspace default for
backend/core is **C# / .NET**. The decision engine is compute-heavy (equilibrium
solving, equity/EV math, opponent modeling) and must be **deterministic and
reproducible** (ADR-0003).

## Decision
- **C# / .NET 10** for everything: the engine core, the solver, the CLI harness, and
  the Raylib front end (`Raylib-cs`). One language, one toolchain (`dotnet build/test`).
- **xUnit** for tests; correctness of the rules engine and the math is pinned by tests.
- The engine is a set of plain class libraries with **no UI/engine dependency**; the
  Raylib front end is a leaf that consumes them (ADR-0004). The test assembly never
  references the Raylib project, so the native dependency stays out of CI.
- Determinism: a seeded PRNG owned by the engine (not `System.Random`, whose algorithm
  isn't guaranteed stable); Monte-Carlo steps are seedable and reproducible.

## Consequences
- Fast headless build/test of the engine independent of any UI.
- C# is fast enough for CFR/equity work at the scale we target (heads-up first, then
  multiway approximations); if a hot loop needs more, we can drop to `Span`/SIMD or a
  native eval table before reaching for another language.
- A native Raylib dependency on one leaf project (allow it if Application Control blocks
  the dll).

## Alternatives considered
- **Python** (the poker-research lingua franca: lots of CFR/solver code) — rejected as
  the core: slower for the inner loops, and off the workspace's C# standard. We will
  still *read* Python references during research.
- **Rust/C++** for raw solver speed — premature; C# clarity first, optimize the proven
  hot path later. Revisit only if profiling demands it.
- **A separate engine language + C# UI** — two toolchains, marshalling overhead, drift.
  Rejected.
