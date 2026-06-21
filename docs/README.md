# pokerface â€” documentation

This project follows the workspace documentation convention. Keep these current â€”
docs that drift are worse than no docs.

- **[ARCHITECTURE.md](ARCHITECTURE.md)** â€” the living design writeup: what the
  system is, its components, how data/control flows, and the load-bearing
  decisions. Update it whenever the design changes.
- **[adr/](adr/)** â€” Architecture Decision Records. One file per significant
  decision, numbered (`NNNN-title.md`), using the template in
  [`adr/0000-template.md`](adr/0000-template.md). ADRs are append-only: don't
  rewrite an accepted ADR â€” supersede it with a new one.

Quick "what / build / run / test" lives in the project root `README.md`.
Terse machine facts (stack, exact run/test commands) live in the project
`AGENTS.md`.

## When to write an ADR
Write one whenever you make a decision that's hard to reverse or that future-you
would ask "why did we do it this way?" â€” stack choice, module boundaries, a
persistence/concurrency approach, a notable trade-off. Skip it for routine,
easily-reversed choices.
