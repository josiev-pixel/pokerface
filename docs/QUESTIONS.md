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
