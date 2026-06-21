# pokerface

A poker game. *(First test project for the local-first AI workflow.)*

Status: **scaffold** — stack and scope not yet chosen. Define them in the first
work session (e.g. `..\orchestrate.ps1 -Project pokerface` or
`..\local.ps1 -C .\`).

## Layout
- `src/` — game code
- `assets/` — cards, sprites, sounds
- `tests/` — unit/integration tests

## Workflow
This project inherits the workspace's local-first workflow (`..\AGENTS.md`):
local Qwen does most of the work, escalating to Codex-cloud / Claude only when a
task calls for it. See `..\README.md`.
