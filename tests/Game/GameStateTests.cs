using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Core.Game;
using Xunit;

namespace PokerEngine.Tests
{
    /// <summary>End-to-end betting-engine behaviour: blinds, folds, min-raise, all-in side pots, determinism.</summary>
    public sealed class GameStateTests
    {
        private static readonly TableRules Stakes = new(SmallBlind: 1, BigBlind: 2);

        private static Card[] Hole(string a, string b) => new[] { Card.Parse(a), Card.Parse(b) };

        private static bool HasKind(IReadOnlyList<PlayerAction> actions, ActionKind kind)
            => actions.Any(a => a.Kind == kind);

        // ---- Heads-up blinds & a simple fold ------------------------------------------------

        [Fact]
        public void HeadsUp_BlindsPostedAndButtonActsFirst()
        {
            var hands = new List<Card[]> { Hole("As", "Ks"), Hole("2c", "2d") };
            var g = GameState.StartHand(Stakes, new[] { 100, 100 }, button: 0, hands);

            // Button (seat 0) is the small blind in heads-up and acts first preflop.
            Assert.Equal(1, g.HandCommitted(0)); // small blind
            Assert.Equal(2, g.HandCommitted(1)); // big blind
            Assert.Equal(99, g.Stack(0));
            Assert.Equal(98, g.Stack(1));
            Assert.Equal(0, g.ToAct);
            Assert.Equal(2, g.CurrentBet);
            Assert.Equal(3, g.Pot);
        }

        [Fact]
        public void HeadsUp_FoldGivesPotToOtherPlayer()
        {
            var hands = new List<Card[]> { Hole("As", "Ks"), Hole("2c", "2d") };
            var g = GameState.StartHand(Stakes, new[] { 100, 100 }, button: 0, hands);

            // SB (button) folds preflop; BB wins the 3-chip pot.
            g.ApplyAction(PlayerAction.Fold());
            Assert.True(g.IsHandComplete());

            int[] net = g.Settle();

            // Seat 0 loses its posted small blind; seat 1 gains it.
            Assert.Equal(-1, net[0]);
            Assert.Equal(1, net[1]);
            Assert.Equal(99, g.Stack(0));
            Assert.Equal(101, g.Stack(1));
        }

        [Fact]
        public void HeadsUp_LimpCheck_ReachesFlop()
        {
            var hands = new List<Card[]> { Hole("As", "Ks"), Hole("2c", "2d") };
            var board = new[]
            {
                Card.Parse("7h"), Card.Parse("8h"), Card.Parse("9h"),
                Card.Parse("Td"), Card.Parse("Jc"),
            };
            var g = GameState.StartHand(Stakes, new[] { 100, 100 }, button: 0, hands, board);

            g.ApplyAction(PlayerAction.Call());  // SB completes to 2
            g.ApplyAction(PlayerAction.Check()); // BB checks its option
            Assert.True(g.IsBettingRoundComplete());

            g.AdvanceStreet();
            Assert.Equal(Street.Flop, g.Street);
            Assert.Equal(3, g.Board.Count);
            Assert.Equal(0, g.CurrentBet);

            // Postflop the non-button (BB, seat 1) acts first heads-up.
            Assert.Equal(1, g.ToAct);
        }

        // ---- Min-raise rules ----------------------------------------------------------------

        [Fact]
        public void Raise_BelowMinimum_IsNotOffered()
        {
            // 3-handed so there is a clean open/raise sequence.
            var hands = new List<Card[]> { Hole("As", "Ah"), Hole("Ks", "Kh"), Hole("Qs", "Qh") };
            var g = GameState.StartHand(Stakes, new[] { 200, 200, 200 }, button: 0, hands);

            // UTG (seat 0, button+? ) faces the big blind of 2. Min legal raise-to is 4 (BB + BB).
            var legal = g.LegalActions();
            Assert.True(HasKind(legal, ActionKind.Raise));
            var raises = legal.Where(a => a.Kind == ActionKind.Raise).Select(a => a.To).ToList();
            Assert.Contains(4, raises);             // exactly the minimum raise-to is offered
            Assert.DoesNotContain(3, raises);       // a raise to 3 (below min) is never offered
        }

        [Fact]
        public void ShortAllInBelowFullRaise_DoesNotReopenBettingForPriorCaller()
        {
            // Seats: 0=button/SB, 1=BB, 2=UTG. Stacks chosen so seat 2 can only shove short.
            // Open to 10 by seat 2; seat 0 raises to 20 (full); seat 1 shoves a short all-in;
            // when action returns to seat 0, the short shove must NOT reopen a re-raise.
            var hands = new List<Card[]> { Hole("As", "Ah"), Hole("Ks", "Kh"), Hole("Qs", "Qh") };
            var g = GameState.StartHand(Stakes, new[] { 200, 25, 200 }, button: 0, hands);

            // Preflop order 3-handed: UTG = seat 0 (button+? ) ... verify by ToAct.
            // With button 0: SB=1, BB=2, UTG = button+3 = seat 0.
            Assert.Equal(0, g.ToAct);

            g.ApplyAction(PlayerAction.RaiseTo(10));  // seat 0 opens to 10 (LastRaiseSize 8)
            Assert.Equal(1, g.ToAct);                 // SB (seat 1)
            // Seat 1 has only 25 behind committed 1 => max to is 25 < 10+8=18? it's >=18, so a full
            // raise is possible. Use a clean short all-in instead: seat 1 calls, seat 2 shoves short.
            g.ApplyAction(PlayerAction.Call());       // seat 1 calls to 10
            Assert.Equal(2, g.ToAct);                 // BB (seat 2)

            g.ApplyAction(PlayerAction.RaiseTo(18));  // seat 2 full-raises to 18 (LastRaiseSize 8)
            // Action returns to seat 0, who already acted; they face a full raise so MAY re-raise.
            Assert.Equal(0, g.ToAct);
            Assert.True(HasKind(g.LegalActions(), ActionKind.Raise));

            // Seat 0 just calls to 18. Seat 1 (25 stack) can only shove short to 25 (increment 7 < 8).
            g.ApplyAction(PlayerAction.Call());       // seat 0 calls to 18
            Assert.Equal(1, g.ToAct);
            g.ApplyAction(PlayerAction.RaiseTo(25));  // seat 1 short all-in (7 over 18, below min 8)
            Assert.Equal(SeatStatus.AllIn, g.Status(1));

            // Now action is on seat 2, who already acted and faces only a short all-in: call/fold only.
            Assert.Equal(2, g.ToAct);
            var seat2Legal = g.LegalActions();
            Assert.True(HasKind(seat2Legal, ActionKind.Call));
            Assert.False(HasKind(seat2Legal, ActionKind.Raise)); // betting not reopened
        }

        // ---- All-in & side pots end to end --------------------------------------------------

        [Fact]
        public void ThreeHandedAllIn_SettlesSidePotsAndStacks()
        {
            // Unequal stacks; everyone gets all-in preflop. Fixed hole cards + fixed board decide it.
            // Seat 0 (short) holds the nuts on this board; seat 2 second-best; seat 1 worst.
            // Board: 2h 5d 9c Jh Qs (no flush/straight). Hands:
            //   seat 0: AhAs  -> pair of aces (best)
            //   seat 1: 3c3d  -> pair of threes (worst)
            //   seat 2: KhKs  -> pair of kings (middle)
            var hands = new List<Card[]>
            {
                Hole("Ah", "As"),
                Hole("3c", "3d"),
                Hole("Kh", "Ks"),
            };
            var board = new[]
            {
                Card.Parse("2h"), Card.Parse("5d"), Card.Parse("9c"),
                Card.Parse("Jc"), Card.Parse("Qs"),
            };

            // Stacks: 50 / 120 / 200. Button = seat 0.
            var g = GameState.StartHand(Stakes, new[] { 50, 120, 200 }, button: 0, hands, board);

            // Drive everyone all-in. Just have each act all-in / call in turn until the round ends.
            DriveToShowdownAllIn(g);

            Assert.True(g.IsHandComplete());
            int[] net = g.Settle();

            // Total contributions: 50 + 120 + 200 = 370. Pot conserved.
            Assert.Equal(0, net.Sum());

            // Main pot = 50*3 = 150 -> seat 0 (best). Side pot A = 70*2 = 140 -> seat 2 (beats seat 1).
            // Side pot B (excess) = 80 -> returns to seat 2 (only contributor at that level).
            // Seat 0 wins 150 (net +100). Seat 1 loses its 120. Seat 2 wins 140+80=220 (net +20).
            Assert.Equal(150 - 50, net[0]);
            Assert.Equal(-120, net[1]);
            Assert.Equal(220 - 200, net[2]);

            // Final stacks reflect winnings added back.
            Assert.Equal(150, g.Stack(0));
            Assert.Equal(0, g.Stack(1));
            Assert.Equal(220, g.Stack(2));
        }

        // ---- Determinism --------------------------------------------------------------------

        [Fact]
        public void SameSeedAndActions_ProduceIdenticalStacks()
        {
            int[] Run()
            {
                var rng = new DeterministicRandom(2024);
                var g = GameState.StartHand(Stakes, new[] { 100, 100 }, button: 0, rng);
                // A fixed action script: SB calls, BB checks; then check it down to showdown.
                g.ApplyAction(PlayerAction.Call());
                g.ApplyAction(PlayerAction.Check());
                while (!g.IsHandComplete())
                {
                    g.AdvanceStreet();
                    while (!g.IsBettingRoundComplete() && g.ToAct >= 0)
                    {
                        var legal = g.LegalActions();
                        // Prefer to check; otherwise call.
                        var act = legal.FirstOrDefault(a => a.Kind == ActionKind.Check);
                        g.ApplyAction(act.Kind == ActionKind.Check ? act : PlayerAction.Call());
                    }
                }
                g.Settle();
                return new[] { g.Stack(0), g.Stack(1) };
            }

            Assert.Equal(Run(), Run());
        }

        /// <summary>Apply all-in / call actions until the betting round resolves with everyone committed.</summary>
        private static void DriveToShowdownAllIn(GameState g)
        {
            // Keep acting while someone is to act: shove all-in if possible, else call.
            int guard = 0;
            while (g.ToAct >= 0 && !g.IsHandComplete())
            {
                if (guard++ > 100) break; // safety
                var legal = g.LegalActions();
                // Prefer the largest raise (all-in); otherwise call.
                var shove = legal.Where(a => a.Kind == ActionKind.Raise || a.Kind == ActionKind.Bet)
                                 .OrderByDescending(a => a.To)
                                 .FirstOrDefault();
                if (shove.Kind == ActionKind.Raise || shove.Kind == ActionKind.Bet)
                    g.ApplyAction(shove);
                else
                    g.ApplyAction(PlayerAction.Call());
            }

            // Run out remaining streets with no further betting (all-in) to reach Complete.
            while (!g.IsHandComplete() && g.Street != Street.Complete)
                g.AdvanceStreet();
        }
    }
}
