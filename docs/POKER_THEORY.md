# Poker Decision-Making — The Theory, in Plain Language

> The non-technical companion to [`DECISION_ALGORITHM.md`](DECISION_ALGORITHM.md) (which
> maps these ideas to the code). This doc explains *what good poker decisions are and why*,
> what is mathematically settled, and where the math runs out and we have to get clever.
> Scope: No-Limit Texas Hold'em, 2–9 players, **heads-up (2 players) first** because it's
> the part we can actually solve.

## 1. What "deciding well" even means
Poker is a game of **hidden information** and **chance**. You never know the opponent's
cards; you only know the odds. So a decision can't be judged by whether it won the pot —
only by whether it makes the most money **on average**, over all the ways the hidden
cards and future streets could fall. That average is called **expected value (EV)**, and
the whole engine exists to pick the action with the highest EV given everything we know.

Three ideas do most of the work:
- **You play ranges, not hands.** Since the opponent can't see your cards, what matters
  is the *whole set of hands* you'd play this way (your "range"), not the one you happen
  to hold. Good play keeps your ranges balanced so the opponent can't read you; good
  reading means estimating *their* range from their actions.
- **Equity** is your share of the pot if all the cards ran out now — your probability of
  winning (plus ties). "I have 62% equity" means you win this pot 62% of the time on
  average against the opponent's likely holdings.
- **Pot odds & EV.** If calling 20 to win a pot of 80, you're risking 20 to win 100, so
  you need to be good ~20% of the time to break even. Compare your equity to the price.
  Every decision — fold, call, raise, and *how much* — is ultimately an EV comparison.

Other levers that feed EV: **position** (acting last is a big advantage — more
information), **initiative/aggression** (betting wins pots two ways: best hand *or* they
fold), **bet sizing** (how much to bet/raise — in No-Limit this is a near-infinite dial),
and **board texture** (how the community cards connect).

## 2. GTO: the "unexploitable" baseline
**Game Theory Optimal (GTO)** play means playing a **Nash equilibrium**: a strategy that
is *best response to itself*, so the opponent can't change their play to beat it.

- In **two-player, zero-sum** games (heads-up poker — one player's win is the other's
  loss), an equilibrium has a remarkable guarantee: **play it and you cannot lose in the
  long run, no matter what the opponent does.** You might not win much against a strong
  player, but you can't be exploited. This makes GTO the perfect **baseline** — a floor
  on how badly you can do.
- GTO is **not** "the play that always wins" and **not** "maximally profitable." Against
  a bad player who folds too much, GTO leaves money on the table — it doesn't *punish*
  the leak, it just refuses to *have* one itself. Punishing leaks is exploitation (§3).
- How "unexploitable" a strategy is gets measured by its **exploitability**: how much a
  perfect opponent could win against it, in milli-big-blinds per game (mbb/g). A true
  equilibrium has exploitability ~0; our solver's job is to get close and *measure* it.

**What's settled:** Heads-up **Limit** Hold'em was essentially **solved** in 2015
(the "Cepheus" project, using an algorithm called CFR+) — a computer plays it
near-perfectly. Heads-up **No-Limit** is too big to solve exactly, but bots
(**Libratus** and **DeepStack**, 2017) beat top human pros using strong approximations.
So heads-up is where the theory is rock-solid, and that's why we build it first.

## 3. Exploitation: beating real, imperfect opponents
Real opponents have **leaks** — they fold too much to bets, call too much, bluff too
little, size their bets in tells. GTO ignores those; **exploitative** play deviates from
GTO to *punish* them: if they fold too often, bluff more; if they never bluff, fold your
medium hands to their big bets.

The catch: **every deviation from GTO opens you up to being exploited back.** If you
start bluffing wildly because they folded twice, a thinking opponent adjusts and traps
you. So the real skill — and what this engine encodes — is a **balance**:
> Play a near-GTO baseline, and deviate from it **only as much as a reliable read
> justifies.** Strong, repeated evidence → bigger, confident deviation. Thin or noisy
> evidence → stay close to GTO. Under uncertainty, GTO is the safe harbor.

## 4. Reading opponents: profiles at three time horizons
To exploit, you have to model the opponent. We track their tendencies at three horizons,
because each answers a different question:
- **This hand:** what does *this* betting line mean right now? (range reading)
- **Recent (this session / last N hands):** are they tilting, card-dead, running over
  the table? Short-term state.
- **Long-term:** their stable style — the classic stats: how often they voluntarily put
  money in (VPIP), how often they raise pre-flop (PFR), their aggression, how often they
  fold to a continuation bet, their 3-bet frequency, and so on.

From these we estimate their **range** in a given spot and spot the **leaks**, then
compute the adjustment that profits most against *that* opponent — bounded by how
confident the read is (§3).

## 5. Heads-up vs. a full table: where the guarantees stop
This is the single most important honesty in the whole project:

- **Heads-up (2 players) is zero-sum** → a Nash equilibrium exists and is
  **unexploitable**. We can compute it, measure how close we are, and trust it.
- **Multiway (3–9 players) is NOT pairwise zero-sum.** With three or more players the
  beautiful guarantees break: equilibria are **not unique**, playing one does **not**
  guarantee you can't lose, and the dynamics between the *other* players (who folds, who
  applies pressure, even implicit "collusion" by checking down) change what's correct.
  There is **no known way to compute a guaranteed-strong multiway strategy.**
- The best multiway bots (**Pluribus**, 2019, which beat pros 6-handed) are
  **approximations** — a pre-computed "blueprint" strategy plus limited live search,
  trained by self-play. They work well in practice but come with **no theoretical
  guarantee** the way heads-up does.

So our plan: **solve and trust heads-up; approximate multiway, and be explicit that it's
an approximation.**

## 6. Where the math gets uncertain (the frontier)
The owner asked us to probe what *isn't* solved. The honest open problems, and the
avenues to attack them (detailed technically in `DECISION_ALGORITHM.md`):

1. **No-Limit bet sizing is continuous.** You can bet *any* amount, so the "tree" of
   possibilities is infinite. Solvers cheat by allowing only a few sizes (e.g. ⅓ pot,
   ¾ pot, pot, all-in) and "translating" real bets to the nearest one — which is
   approximate and exploitable. *Avenues:* finer size sets, position-/texture-dependent
   sizing, or learning good sizes.
2. **Multiway has no tractable, guaranteed solution** (see §5). *Avenues:* self-play
   approximation (Pluribus-style), depth-limited search, or simplifying the other
   players into a single modeled "field."
3. **Opponents adapt.** A fixed exploit gets countered. Modeling a *moving target* is
   unsolved in general. *Avenues:* Bayesian opponent models that update as evidence
   arrives, and capping our deviation so we're never wildly exploitable while we learn.
4. **Abstraction loses fidelity.** We can't solve the real game, only a simplified
   ("abstracted") version, then map real spots onto it — losing detail that a strong
   opponent could attack. *Avenues:* better abstractions, and live "re-solving" of the
   exact current spot.
5. **Real-time compute is finite.** Perfectly re-solving every spot is too slow.
   *Avenues:* a fast pre-computed blueprint for most spots + targeted deep search only
   where it matters.

For each, there are three families of attack: **more computing power** (bigger solves,
finer abstractions), **better theory** (smarter algorithms, safe re-solving), and **AI
decision-making** (learned strategies / value networks). We **lean toward the first two**
— deterministic, explainable, reproducible — and treat AI/learning as an *optional
experiment* for the frontier cases, never the unexamined default (see §7).

## 7. Why we lean deterministic
A **deterministic** engine means: the same situation, the same opponent model, and the
same random seed always produce the same decision — and we can *explain why*. We prefer:
- **computed equilibrium strategies** (from the CFR algorithm) you can inspect and
  measure, over
- **black-box neural agents** that may play well but can't tell you *why* and aren't
  reproducible or auditable.

This makes the engine **testable, debuggable, and trustworthy** — you can ask it "why
this fold?" and get pot odds, range, equity, and the exploit adjustment, not a shrug.
Where we do use randomness (Monte-Carlo equity, sampled solving) it is **seeded**, so
runs reproduce exactly. AI/learning stays a clearly-labeled research track for the
problems theory can't yet reach (§6), not the foundation.

## 8. Glossary
- **EV (Expected Value):** average profit of an action over all unknown outcomes.
- **Equity:** your probability of winning the pot if the hand ran to showdown now.
- **Range:** the full set of hands you (or they) could hold in a given line.
- **Pot odds:** the price you're getting to call (risk vs. reward), as a break-even %.
- **GTO / Nash equilibrium:** a self-best-responding strategy; unexploitable heads-up.
- **Exploitability (mbb/g):** how much a perfect opponent beats a strategy for; 0 = an
  exact equilibrium.
- **CFR (Counterfactual Regret Minimization):** the main algorithm for computing
  approximate equilibria in hidden-information games (see the technical doc).
- **Abstraction:** simplifying the game (grouping similar hands, limiting bet sizes) so
  it's small enough to solve; introduces approximation.
- **Blueprint:** a pre-computed baseline strategy for the (abstracted) whole game.
- **Re-solving / subgame solving:** recomputing the exact current situation live to
  sharpen the blueprint's decision.
- **Position:** where you act in the betting order; acting later = more information = better.
- **VPIP / PFR / AF:** opponent stats — how loose, how aggressive pre-flop, how
  aggressive overall.
- **ICM (Independent Chip Model):** in tournaments, chips aren't linear money; ICM
  converts stacks to payout-equity so decisions account for survival, not just chips.
