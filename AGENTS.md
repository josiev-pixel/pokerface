# pokerface — project notes for agents

(Inherits the workspace workflow in `../AGENTS.md`. This file is project-specific.)

## What this is
A **No-Limit Texas Hold'em decision engine** (the priority) plus a basic **Raylib
scenario front end** for setting up and testing spots. Up to **9 players**, but
**heads-up (2-player) is built and validated first** because it's the solvable case.
Plays a **GTO baseline** and **exploits profiled opponents** within bounded risk; leans
**deterministic + explainable**. The theory and algorithm are documented in
`docs/POKER_THEORY.md` (plain language) and `docs/DECISION_ALGORITHM.md` (technical) —
**keep both current as the engine evolves; they are the source of truth.**

## Who leads
**Claude-cloud leads** (like the basketballer project): it does the hard design, theory,
and algorithm work and **directs** — delegating mechanical implementation/tests/reading
to **local Qwen** (`..\delegate-local.ps1`) and scoped patches to **codex-cloud**
(`..\escalate-codex.ps1`), reviewing everything. Spend the expensive models on judgment;
push routine work down. Studio-capable: `..\studio.ps1 -Project pokerface`.

## Stack / build / test
- **C# / .NET 10** everything (ADR-0002). Requires the .NET 10 SDK. Fresh-terminal PATH
  refresh if needed:
  `$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")`
- **Build:** `dotnet build`   **Test:** `dotnet test` (xUnit, headless).
- Solution: `PokerEngine.slnx`. Tests never reference the Raylib `Table` project.

## Layout (planned; see ARCHITECTURE.md / ADR-0003)
- `src/PokerEngine.Core` — rules/state, hand eval, equity/EV. **Exact, deterministic.**
- `src/PokerEngine.Abstraction` — card/action abstraction + translation (the approx seam).
- `src/PokerEngine.Solver` — CFR family → blueprint; subgame re-solving; exploitability.
- `src/PokerEngine.Profiling` — opponent stats (3 horizons), range estimate, leak detection.
- `src/PokerEngine.Decision` — GTO baseline + bounded exploit → action.
- `src/PokerEngine.Cli` — solve/benchmark/exploitability/scenarios.
- `src/PokerEngine.Table` — Raylib scenario tool (ADR-0004; dev/test only).
- `tests/PokerEngine.Tests` — xUnit.

## Conventions (enforce on every worker prompt)
- C#/.NET, clean and well-factored, tests for the logic; build green (warnings as errors
  in engine libraries).
- **Determinism:** one seeded PRNG owns all randomness; **never** `System.Random` or
  wall-clock seeds in Core/Decision; no result-affecting unordered iteration. Same
  `(state, opponentModel, seed)` ⇒ same action. (DECISION_ALGORITHM §9.)
- **Exact vs. approximate is explicit:** rules/eval/equity are exact; abstraction/CFR are
  reproducible-approximations; multiway is labeled an approximation (no guarantee).
- **The Lead owns the theory and the math/tuning** (CFR params, abstraction granularity,
  exploit caps `w_max`, sizing sets). Workers wire systems + tests; they don't invent
  poker theory or balance numbers.
- **Keep the two docs in lockstep with the code** — when a module lands, update
  DECISION_ALGORITHM's module map from *(planned)* to the concrete type.
- Write an ADR for any load-bearing decision; update ARCHITECTURE.md.

## Sequencing (high level — live worklist is `docs/STUDIO_BACKLOG.md`)
1. **Research** the state of poker GTO (CFR/CFR+/MCCFR, abstraction, subgame solving,
   exploitability, Cepheus/Libratus/DeepStack/Pluribus, ICM) → deepen the two docs.
2. **Core**: cards/deck (seeded), hand evaluator, game state + betting + side pots,
   equity/EV. Exhaustively tested.
3. **Heads-up solver**: CFR+ on a small abstraction; **measure exploitability**.
4. **Profiling + bounded exploit**; **Decision** policy.
5. **Raylib scenario table** to interrogate spots.
6. Generalize to multiway (approximation, labeled); probe the frontiers (DECISION §8).

## Git
Owner makes commits in normal sessions (leave clean green checkpoints + flag). In
**studio mode** the lead commits at green checkpoints on the current branch (per
`..\STUDIO.md`).
