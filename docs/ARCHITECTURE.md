# pokerface â€” Architecture

> Living document. Update it when the design changes; don't let it drift from the
> code. Summarize load-bearing decisions here and link the full ADR.

## Overview
_One paragraph: what this is and the overall shape of the solution._

## Context & goals
- **Goals:** _what success looks like_
- **Non-goals:** _what we're deliberately not doing (yet)_
- **Constraints:** _stack (C#/.NET backend by default), platform, dependencies_

## High-level design
_The components and how they fit together. A simple diagram or a bullet map of
boxes-and-arrows is fine â€” clarity over ceremony._

## Key components
| Component | Responsibility | Notes |
|-----------|----------------|-------|
| _e.g. Core_ | _domain model & rules_ | |
| _e.g. Engine_ | _game/loop orchestration_ | |

## Data & control flow
_How a typical operation flows through the system, start to finish._

## Key decisions
_The load-bearing choices, each linked to its ADR in `adr/`:_
- _ADR-0001 â€” Record architecture decisions_
- _ADR-0002 â€” &lt;stack / first real decision&gt;_

## Risks & open questions
- _known risks, things still undecided_
