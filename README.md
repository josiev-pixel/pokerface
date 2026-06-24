# pokerface

A **No-Limit Texas Hold'em decision engine** — game-theoretically strong play that also
**profiles and exploits** opponents, leaning **deterministic and explainable**. Up to 9
players; **heads-up first** (the solvable case). Plus a basic **Raylib scenario tool** to
set up spots (drag the 52 cards into place, set pot/bets/action/button) and ask the engine
what it would do.

The decision-making is documented in two companion docs — read these first:
- **[docs/POKER_THEORY.md](docs/POKER_THEORY.md)** — the theory, in plain language.
- **[docs/DECISION_ALGORITHM.md](docs/DECISION_ALGORITHM.md)** — the technical algorithm,
  mapped to the code.

See also [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and the [decision records](docs/adr/).

## Build & test
Needs the **.NET 10 SDK**.
```
dotnet build       # build PokerEngine.slnx
dotnet test        # xUnit, headless
```

## Layout
- `src/PokerEngine.*` — engine (Core, Abstraction, Solver, Profiling, Decision, Cli) + the
  Raylib `Table` scenario tool.
- `tests/` — xUnit.
- `docs/` — theory + algorithm + architecture + ADRs.

## Workflow
Claude-cloud leads and delegates routine work to local Qwen / codex-cloud (see
[`AGENTS.md`](AGENTS.md)). Studio-capable: `..\studio.ps1 -Project pokerface`.
