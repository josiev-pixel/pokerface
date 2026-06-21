# ADR 0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-06-21
- **Deciders:** workspace convention

## Context
This project is built largely by AI agents across many sessions. Decisions made
in one session are invisible to the next unless they're written down, which leads
to re-litigated choices and accidental drift. We want a lightweight, durable way
to capture *why* the system is the way it is.

## Decision
We will record every significant architecture decision as an ADR in `docs/adr/`,
one file per decision, numbered sequentially (`NNNN-title.md`), using the format
in `0000-template.md` (Context / Decision / Consequences / Alternatives). ADRs are
append-only: an accepted ADR is never rewritten â€” it is superseded by a later ADR.

## Consequences
- Future sessions (and humans) can read the decision log to understand the design
  without reverse-engineering the code.
- Small overhead per significant decision; routine, easily-reversed choices don't
  need an ADR.
- `docs/ARCHITECTURE.md` stays the high-level living view and links the ADRs.

## Alternatives considered
- **Only a single ARCHITECTURE.md** â€” loses the history and the "why" behind
  superseded choices.
- **Decisions in commit messages / chat only** â€” not discoverable later.
