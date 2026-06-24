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
- **Profiling→Decision wiring:** local worker authored `OpponentModelFactory` + tests cleanly.
  Lead fix-up: sourced `Confidence` from the *same* stat the model reads (the street's fold-to-bet
  / fold-to-cbet) rather than the VPIP-based `profile.Confidence` proxy, so a fold-only sample
  still yields confidence > 0 (a genuine design call on which read drives the exploit weight).
