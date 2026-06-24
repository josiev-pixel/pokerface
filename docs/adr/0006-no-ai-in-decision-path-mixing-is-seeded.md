# ADR 0006: "Deterministic" = no AI in the decision path + seeded mixing

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** owner (clarification), claude-cloud (design)

## Context
The project's "lean deterministic" instruction was bundling two different ideas, and the owner
clarified the intent: *"I mostly meant preferably let's not invoke AI within the decision-making
process. We can discuss if a very strong use case is found. I do very much expect we will need
pseudorandom behavior (do X a certain percentage of the time and Y a different percentage of the
time)."*

This matters because correct poker is **not** a deterministic function from cards to a single
action. Equilibrium play is *mixed*: the same hand is played different ways at set frequencies so
the opponent can't read it. A policy that always took one action per spot would be trivially
exploitable. So "deterministic" cannot mean "never randomize."

## Decision
Split the requirement into two precise, separately-testable guarantees:

1. **No AI/ML in the decision path.** Decisions are produced by explainable poker math — equity,
   pot odds, MDF/alpha, the CFR algorithm, opponent frequencies — **not** a neural network or a
   language model. Every decision is interrogable (`DecisionResult` carries equity, pot odds, the
   full mixed strategy, the exploit weight, and a plain-language explanation). Learned/neural
   methods remain an explicitly-flagged research track for frontier problems (DECISION_ALGORITHM
   §8), always benchmarked head-to-head against this baseline by exploitability/EV, never silently
   in the path. A genuinely compelling case for a learned component is a conversation to have
   explicitly, not a default.

2. **Mixing is expected; reproducibility is the real invariant.** The engine deliberately uses
   pseudorandom behavior ("bet 70%, check 30%") and reports the whole distribution alongside the
   sampled action. All such randomness flows through one seeded PRNG
   (`Core.DeterministicRandom`), so **same (state, opponentModel, seed) ⇒ same action and same
   numbers**. `System.Random`, wall-clock seeds, and result-affecting unordered iteration are
   banned in `Core`/`Decision`.

## Consequences
- The docs (POKER_THEORY §7, DECISION_ALGORITHM §9) state both guarantees explicitly so the two
  ideas stop being conflated.
- `DecisionEngine` samples mixed strategies from the seeded PRNG and surfaces `Strategy` (the full
  σ) in every result; tests assert same-seed reproducibility *and* that probabilities are honored.
- "No AI" is a design constraint on the decision path only — it does not forbid seeded
  Monte-Carlo, CFR self-play, or future learned components behind a clearly-labeled research flag.
