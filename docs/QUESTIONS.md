# Banked questions for the Director

The studio lead logs decisions it wants your input on here **instead of blocking**.
Each entry records the question, why it matters, and the default it proceeded with,
so nothing stalls overnight. Review these and reply / adjust; the lead folds your
answers back into the backlog and ADRs.

Format:

```
## [open] <short question>
- Context: why this came up
- Proceeding with (default): <the assumption made to keep moving>
- Reversible? yes / no   ·   ADR: docs/adr/NNNN (if one was written)
```

---

## Worklog / accountability
- **Abstraction cycle:** the local worker implemented the `PokerEngine.Abstraction` library
  (the bulk of the new code) but did not produce `tests/Abstraction/AbstractionTests.cs`. The
  lead wrote that test file directly as a small fix-up rather than re-dispatching, to keep the
  cycle moving; the library itself was worker-authored and lead-reviewed. (ADR-0007.)
- **Profiling cycle:** the local worker authored the `PokerEngine.Profiling` library (worker
  code, lead-reviewed) and its tests, but wrote the test file as UTF-16 and had one broken
  assertion (`Confidence() > Confidence(20.0)` — identical values). The lead rewrote
  `tests/Profiling/ProfilingTests.cs` in UTF-8 with corrected assertions as a fix-up. (ADR-0008.)
- **RangeEquity cycle:** local worker authored `RangeBuilder`/`RangeEquity`, but `RangeBuilder`
  had a card-index bug (`(int)Rank * 4 + suit` instead of using the `Card(Rank,Suit)` ctor —
  threw for aces), and both `RangeEquity.cs` and the test were UTF-16. Lead fixed the index bug
  (small fix-up), re-encoded `RangeEquity.cs` to UTF-8, and rewrote the test in UTF-8. Two prior
  dispatch attempts failed on a CLI quoting bug, not the worker — see the delegate-local memory.
- **CLI `leduc` + `pushfold` commands:** the local worker claimed success but did not actually
  apply the `leduc` edit to `Program.cs` (a tooling/restore failure editing the top-level-statements
  file). The lead wrote both the `leduc` (~12 lines) and `pushfold` (~40 lines) commands directly
  rather than re-dispatching to a worker that demonstrably can't edit `Program.cs`. `leduc` verified
  (exploitability 0.034 chips/hand at 3k iters). `pushfold`'s 169×169 matrix build is slow — see the
  perf TODO banked below.

## [open] Push/fold matrix build is slow for interactive CLI use
- Context: `pushfold` builds a 169×169 equity matrix (~28k `RangeEquity` calls), which takes minutes
  at useful sample counts — fine for the engine/tests (which use a tiny synthetic matrix) but slow as
  a live CLI demo. Flagged as a cost in ADR-0010.
- Proceeding with (default): ship the command with a low default sample count + a "this takes a moment"
  note; treat speed-up as a backlog item (exploit equity symmetry to compute only the upper triangle;
  cache the matrix to disk; or a faster preflop-equity path).
- Reversible? yes · ADR: docs/adr/0010
- **Profiling→Decision wiring:** local worker authored `OpponentModelFactory` + tests cleanly.
  Lead fix-up: sourced `Confidence` from the *same* stat the model reads (the street's fold-to-bet
  / fold-to-cbet) rather than the VPIP-based `profile.Confidence` proxy, so a fold-only sample
  still yields confidence > 0 (a genuine design call on which read drives the exploit weight).
