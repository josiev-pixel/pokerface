# ADR 0004: The Raylib front end is a dev/scenario tool, not a product

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (design call), owner ("a very basic front end … for testing")

## Context
pokerface's value is the **decision engine**. The owner wants a **basic Raylib front
end** purely to *set up and test scenarios*: a poker-table view that exposes all 52
cards (four rows by suit) to **click-and-drag into place** (hole cards, board), and lets
you set **pot, each player's bet, whose action it is, the board, and the button**, then
ask the engine what it would do. (We learned on a sibling project, Hardcourt Hollow,
that an unscoped dev front end quietly grows into a second product — ADR-0009 there — so
we scope this one from day one.)

## Decision
The Raylib front end (`PokerEngine.Table`) is a **developer/scenario tool**, not a
shipping product. Its job is exactly:
- render a readable poker table and a palette of all 52 cards (4 suit rows);
- **drag-and-drop** cards into player holes and the board;
- edit **stacks, bets, pot, blinds, button position, and whose action it is**;
- invoke the engine and **show its recommended action + the reasoning/EV/equity** and
  any debug readouts (range, exploit adjustment, confidence).

It is **not** for: a polished playable poker game, online play, animations, accounts,
or money. It stays a leaf project consuming `Core`/`Decision`; **tests never reference
it**, so the native Raylib dependency stays out of CI.

## Consequences
- Fast, visual way to construct exactly the spot you want and interrogate the engine —
  the most useful possible test/tuning surface for a decision engine.
- No effort sunk into product polish; engine quality stays the focus.
- If a real product UI is ever wanted, it's a separate, deliberate decision — the engine
  is UI-agnostic, so that stays cheap.

## Alternatives considered
- **CLI-only scenario setup** (type in cards/bets) — dependency-free and testable, but
  clumsy for exploring many spots; we keep a CLI harness too, but the drag-and-drop
  table is worth the small Raylib cost for human tuning.
- **A full playable poker GUI** — far more work, irrelevant to the engine goal, and the
  exact scope-creep trap we're pre-empting. Rejected.
