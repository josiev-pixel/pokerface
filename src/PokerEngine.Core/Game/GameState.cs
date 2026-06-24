using System;
using System.Collections.Generic;
using PokerEngine.Core.Eval;

namespace PokerEngine.Core.Game
{
    /// <summary>
    /// One No-Limit Texas Hold'em hand in progress, for 2–9 players. Owns the betting state and
    /// drives the hand through its streets to showdown. Exact and deterministic: all randomness
    /// (when dealing) flows through a seeded <see cref="DeterministicRandom"/>, and chips are
    /// integers conserved across the hand. State is fully inspectable so a UI and the decision
    /// layer can read it without re-deriving anything.
    /// </summary>
    /// <remarks>
    /// Betting rules implemented: blinds/antes, check/call/bet/raise/fold, No-Limit sizing, correct
    /// min-raise, and the "short all-in does not reopen the betting" rule (an all-in raise smaller
    /// than a full raise lets players who already acted only call or fold, not re-raise). Heads-up
    /// uses the standard rule that the button posts the small blind and acts first preflop.
    /// </remarks>
    public sealed class GameState
    {
        private readonly TableRules _rules;
        private readonly int _seatCount;
        private readonly int[] _stack;
        private readonly int[] _streetCommitted;
        private readonly int[] _handCommitted;
        private readonly SeatStatus[] _status;
        private readonly Card[]?[] _holeCards;
        private readonly bool[] _hasActedThisRound;
        private readonly List<Card> _board = new(5);

        private readonly Deck? _deck;          // present when dealing from a shuffled deck
        private readonly IReadOnlyList<Card>? _predealtBoard; // present when the board is fixed up front

        private GameState(TableRules rules, int seatCount, Deck? deck, IReadOnlyList<Card>? predealtBoard)
        {
            _rules = rules;
            _seatCount = seatCount;
            _stack = new int[seatCount];
            _streetCommitted = new int[seatCount];
            _handCommitted = new int[seatCount];
            _status = new SeatStatus[seatCount];
            _holeCards = new Card[]?[seatCount];
            _hasActedThisRound = new bool[seatCount];
            _deck = deck;
            _predealtBoard = predealtBoard;
        }

        /// <summary>Number of seats dealt into this hand.</summary>
        public int SeatCount => _seatCount;

        /// <summary>Button seat index.</summary>
        public int Button { get; private set; }

        /// <summary>The current betting street.</summary>
        public Street Street { get; private set; }

        /// <summary>Seat index of the player to act, or -1 when no one is left to act.</summary>
        public int ToAct { get; private set; }

        /// <summary>Highest street commitment any player has reached this street (the amount to match).</summary>
        public int CurrentBet { get; private set; }

        /// <summary>Size of the last full bet/raise this street; the minimum legal raise increment.</summary>
        public int LastRaiseSize { get; private set; }

        /// <summary>The table stakes.</summary>
        public TableRules Rules => _rules;

        /// <summary>The community cards revealed so far (0, 3, 4, or 5).</summary>
        public IReadOnlyList<Card> Board => _board;

        /// <summary>Total chips committed to the pot this hand (sum of every seat's hand commitment).</summary>
        public int Pot
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _seatCount; i++) sum += _handCommitted[i];
                return sum;
            }
        }

        /// <summary>Chips behind for <paramref name="seat"/>.</summary>
        public int Stack(int seat) => _stack[seat];

        /// <summary>Chips <paramref name="seat"/> has committed on the current street.</summary>
        public int StreetCommitted(int seat) => _streetCommitted[seat];

        /// <summary>Chips <paramref name="seat"/> has committed over the whole hand.</summary>
        public int HandCommitted(int seat) => _handCommitted[seat];

        /// <summary>Standing of <paramref name="seat"/> in the hand.</summary>
        public SeatStatus Status(int seat) => _status[seat];

        /// <summary>The two hole cards of <paramref name="seat"/>, or null if unknown.</summary>
        public IReadOnlyList<Card>? HoleCards(int seat) => _holeCards[seat];

        // ---- Construction -------------------------------------------------------------------

        /// <summary>
        /// Start a hand with explicit hole cards (and optionally a pre-dealt board). Posts antes and
        /// blinds, sets the first player to act, and leaves the engine ready for <see cref="LegalActions"/>.
        /// </summary>
        public static GameState StartHand(
            TableRules rules,
            IReadOnlyList<int> stacks,
            int button,
            IReadOnlyList<Card[]> holeCards,
            IReadOnlyList<Card>? boardIfPredealt = null)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (stacks is null) throw new ArgumentNullException(nameof(stacks));
            if (holeCards is null) throw new ArgumentNullException(nameof(holeCards));
            int seatCount = stacks.Count;
            if (seatCount < 2 || seatCount > 9)
                throw new ArgumentException("A hand needs 2 to 9 players.", nameof(stacks));
            if (holeCards.Count != seatCount)
                throw new ArgumentException("Need hole cards for every seat.", nameof(holeCards));
            if (button < 0 || button >= seatCount)
                throw new ArgumentOutOfRangeException(nameof(button));

            var state = new GameState(rules, seatCount, deck: null, predealtBoard: boardIfPredealt);
            for (int i = 0; i < seatCount; i++)
            {
                if (stacks[i] <= 0) throw new ArgumentException("Every stack must be positive.", nameof(stacks));
                state._stack[i] = stacks[i];
                Card[] hc = holeCards[i];
                if (hc is null || hc.Length != 2)
                    throw new ArgumentException("Each seat needs exactly two hole cards.", nameof(holeCards));
                state._holeCards[i] = new[] { hc[0], hc[1] };
            }

            state.Begin(button);
            return state;
        }

        /// <summary>
        /// Start a hand by dealing hole cards (and, lazily, the board) from a shuffled deck. The deck
        /// must already be seeded/shuffled by the caller so the deal is reproducible.
        /// </summary>
        public static GameState StartHand(
            TableRules rules,
            IReadOnlyList<int> stacks,
            int button,
            Deck deck)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (stacks is null) throw new ArgumentNullException(nameof(stacks));
            if (deck is null) throw new ArgumentNullException(nameof(deck));
            int seatCount = stacks.Count;
            if (seatCount < 2 || seatCount > 9)
                throw new ArgumentException("A hand needs 2 to 9 players.", nameof(stacks));
            if (button < 0 || button >= seatCount)
                throw new ArgumentOutOfRangeException(nameof(button));

            var state = new GameState(rules, seatCount, deck, predealtBoard: null);

            // Deal two cards per seat, one at a time around the table from the small blind — the
            // exact order is immaterial to correctness but kept conventional and deterministic.
            var hands = new Card[seatCount][];
            for (int i = 0; i < seatCount; i++) hands[i] = new Card[2];
            for (int round = 0; round < 2; round++)
                for (int i = 0; i < seatCount; i++)
                    hands[i][round] = deck.Deal();

            for (int i = 0; i < seatCount; i++)
            {
                if (stacks[i] <= 0) throw new ArgumentException("Every stack must be positive.", nameof(stacks));
                state._stack[i] = stacks[i];
                state._holeCards[i] = hands[i];
            }

            state.Begin(button);
            return state;
        }

        /// <summary>Convenience: shuffle a fresh deck with <paramref name="rng"/>, then deal the hand.</summary>
        public static GameState StartHand(
            TableRules rules,
            IReadOnlyList<int> stacks,
            int button,
            DeterministicRandom rng)
        {
            if (rng is null) throw new ArgumentNullException(nameof(rng));
            var deck = new Deck();
            deck.Shuffle(rng);
            return StartHand(rules, stacks, button, deck);
        }

        /// <summary>Post antes and blinds, then open the preflop betting round.</summary>
        private void Begin(int button)
        {
            Button = button;
            Street = Street.Preflop;

            for (int i = 0; i < _seatCount; i++) _status[i] = SeatStatus.Active;

            // Antes first (each living seat posts up to its stack).
            if (_rules.Ante > 0)
                for (int i = 0; i < _seatCount; i++)
                    PostForced(i, _rules.Ante, countTowardCurrentBet: false);

            int sbSeat = SmallBlindSeat();
            int bbSeat = BigBlindSeat();
            PostForced(sbSeat, _rules.SmallBlind, countTowardCurrentBet: true);
            PostForced(bbSeat, _rules.BigBlind, countTowardCurrentBet: true);

            CurrentBet = _streetCommitted[bbSeat];
            // Antes can push a short stack all-in; the bet to match is still the big blind line.
            if (CurrentBet < _rules.BigBlind) CurrentBet = _rules.BigBlind;
            LastRaiseSize = _rules.BigBlind;

            // Everyone still able to act must act at least once.
            for (int i = 0; i < _seatCount; i++)
                _hasActedThisRound[i] = _status[i] != SeatStatus.Active;

            // The blinds were forced, not voluntary actions: the big blind still has the option to
            // raise, so they have not "acted" for round-completion purposes.
            FindNextToAct(FirstToActPreflop());
        }

        /// <summary>Move chips into the pot for a forced bet (blind/ante), clamping to the stack.</summary>
        private void PostForced(int seat, int amount, bool countTowardCurrentBet)
        {
            int pay = Math.Min(amount, _stack[seat]);
            _stack[seat] -= pay;
            _handCommitted[seat] += pay;
            if (countTowardCurrentBet) _streetCommitted[seat] += pay;
            if (_stack[seat] == 0) _status[seat] = SeatStatus.AllIn;
        }

        // ---- Seat geometry ------------------------------------------------------------------

        private int Next(int seat) => (seat + 1) % _seatCount;

        private int SmallBlindSeat()
            // Heads-up: the button is the small blind. Otherwise SB sits to the button's left.
            => _seatCount == 2 ? Button : Next(Button);

        private int BigBlindSeat()
            => _seatCount == 2 ? Next(Button) : Next(Next(Button));

        private int FirstToActPreflop()
        {
            // Heads-up: the small blind (the button) acts first. Otherwise UTG = seat left of the BB.
            int start = _seatCount == 2 ? SmallBlindSeat() : Next(BigBlindSeat());
            return NextActiveFrom(start);
        }

        private int FirstToActPostflop()
        {
            // First active seat to the left of the button (heads-up: the non-button player).
            int start = Next(Button);
            return NextActiveFrom(start);
        }

        /// <summary>First seat at or after <paramref name="start"/> (wrapping) that is <see cref="SeatStatus.Active"/>, or -1.</summary>
        private int NextActiveFrom(int start)
        {
            for (int k = 0; k < _seatCount; k++)
            {
                int seat = (start + k) % _seatCount;
                if (_status[seat] == SeatStatus.Active) return seat;
            }
            return -1;
        }

        // ---- Legal actions ------------------------------------------------------------------

        /// <summary>The legal actions for the player to act, with correct No-Limit sizing.</summary>
        public IReadOnlyList<PlayerAction> LegalActions()
        {
            var actions = new List<PlayerAction>();
            int seat = ToAct;
            if (seat < 0 || _status[seat] != SeatStatus.Active) return actions;

            int committed = _streetCommitted[seat];
            int stack = _stack[seat];
            int toCall = CurrentBet - committed;

            // Fold is always available (standard engines allow folding even when a check is free).
            actions.Add(PlayerAction.Fold());

            if (toCall <= 0)
            {
                // Nothing to call: may check, and may open a bet if any chips remain.
                actions.Add(PlayerAction.Check());
                if (stack > 0)
                {
                    int minBetTo = committed + Math.Min(_rules.BigBlind, stack);
                    int maxBetTo = committed + stack;
                    actions.Add(PlayerAction.BetTo(minBetTo));
                    if (maxBetTo != minBetTo) actions.Add(PlayerAction.BetTo(maxBetTo));
                }
            }
            else
            {
                // Facing a bet: call (all-in if short) is always legal while chips remain.
                actions.Add(PlayerAction.Call());

                // Raising requires (a) chips beyond the call and (b) the betting to be open to this
                // seat — a prior short all-in that did not reach a full raise does not reopen it.
                bool canReopen = toCall < stack && _reopenedForAll;
                if (canReopen)
                {
                    int minRaiseTo = CurrentBet + LastRaiseSize;
                    int maxRaiseTo = committed + stack;
                    if (minRaiseTo > maxRaiseTo) minRaiseTo = maxRaiseTo; // all-in shy of a full raise
                    actions.Add(PlayerAction.RaiseTo(minRaiseTo));
                    if (maxRaiseTo != minRaiseTo) actions.Add(PlayerAction.RaiseTo(maxRaiseTo));
                }
            }

            return actions;
        }

        // True while the action is reopened (street start or after a full bet/raise), so the seat to
        // act may raise. A short all-in (an all-in raise below a full raise) clears it, leaving
        // facing players only the option to call or fold.
        private bool _reopenedForAll = true;

        // ---- Applying actions ---------------------------------------------------------------

        /// <summary>Apply <paramref name="action"/> for the player to act and advance the action pointer.</summary>
        public void ApplyAction(PlayerAction action)
        {
            int seat = ToAct;
            if (seat < 0) throw new InvalidOperationException("No player is to act.");
            if (_status[seat] != SeatStatus.Active) throw new InvalidOperationException("That seat cannot act.");

            int committed = _streetCommitted[seat];
            int stack = _stack[seat];
            int toCall = CurrentBet - committed;

            switch (action.Kind)
            {
                case ActionKind.Fold:
                    _status[seat] = SeatStatus.Folded;
                    break;

                case ActionKind.Check:
                    if (toCall > 0) throw new InvalidOperationException("Cannot check facing a bet.");
                    break;

                case ActionKind.Call:
                {
                    int pay = Math.Min(toCall, stack);
                    MoveToPot(seat, pay);
                    break;
                }

                case ActionKind.Bet:
                {
                    if (CurrentBet != committed)
                        throw new InvalidOperationException("Cannot bet when there is already a bet to call.");
                    ApplyAggressive(seat, action.To, isBet: true);
                    break;
                }

                case ActionKind.Raise:
                {
                    if (toCall <= 0)
                        throw new InvalidOperationException("Cannot raise when there is nothing to call.");
                    if (!_reopenedForAll)
                        throw new InvalidOperationException("The betting is not reopened to this seat.");
                    ApplyAggressive(seat, action.To, isBet: false);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }

            _hasActedThisRound[seat] = true;
            AdvanceAfterAction();
        }

        /// <summary>Handle a bet or raise: validate the "to" amount, move chips, update the bet line.</summary>
        private void ApplyAggressive(int seat, int to, bool isBet)
        {
            int committed = _streetCommitted[seat];
            int stack = _stack[seat];
            int maxTo = committed + stack;

            if (to <= committed) throw new InvalidOperationException("A bet/raise must increase the commitment.");
            if (to > maxTo) throw new InvalidOperationException("Cannot commit more than the stack.");

            bool isAllIn = to == maxTo;
            int increment = to - CurrentBet; // how much this tops the current bet line by

            if (isBet)
            {
                // Opening bet: at least the big blind unless it is an all-in shove for less.
                if (!isAllIn && to - committed < _rules.BigBlind)
                    throw new InvalidOperationException("Opening bet is below the minimum.");
            }
            else
            {
                // Raise: a full raise tops the bet by at least the last raise size; a short all-in is
                // allowed but must be a genuine all-in (handled by the isAllIn branch below).
                if (!isAllIn && increment < LastRaiseSize)
                    throw new InvalidOperationException("Raise is below the minimum.");
            }

            int priorLine = CurrentBet;
            int pay = to - committed;
            MoveToPot(seat, pay);

            int newLine = _streetCommitted[seat];
            int raiseSize = newLine - priorLine;        // how much this tops the prior line by
            // An opening bet (prior line 0) is always a "full" raise and defines the first raise size.
            bool isFullRaise = priorLine == 0 || raiseSize >= LastRaiseSize;

            CurrentBet = newLine;

            if (isFullRaise)
            {
                // A full bet/raise sets a new line, defines the new minimum raise increment, and
                // reopens the action to everyone else still in the hand.
                LastRaiseSize = raiseSize;
                _reopenedForAll = true;
                ReArmEveryoneElse(seat);
            }
            else
            {
                // Short all-in: it raises the line (others must call the extra) but does NOT reopen
                // the betting to players who have already acted. LastRaiseSize is unchanged.
                _reopenedForAll = false;
                ReArmCallersFacingShortAllIn(seat);
            }
        }

        /// <summary>After a full bet/raise, everyone else still active must act again.</summary>
        private void ReArmEveryoneElse(int aggressor)
        {
            for (int i = 0; i < _seatCount; i++)
                if (i != aggressor && _status[i] == SeatStatus.Active)
                    _hasActedThisRound[i] = false;
        }

        /// <summary>
        /// After a short all-in, players who already acted may now owe a little more and so must get
        /// a chance to call/fold the increment — but they may not re-raise. We re-arm only those who
        /// have not yet matched the new line; their raise option stays closed via <see cref="_reopenedForAll"/>.
        /// </summary>
        private void ReArmCallersFacingShortAllIn(int aggressor)
        {
            for (int i = 0; i < _seatCount; i++)
                if (i != aggressor && _status[i] == SeatStatus.Active && _streetCommitted[i] < CurrentBet)
                    _hasActedThisRound[i] = false;
        }

        /// <summary>Move <paramref name="amount"/> from a seat's stack into the pot, flagging all-in.</summary>
        private void MoveToPot(int seat, int amount)
        {
            if (amount < 0) throw new InvalidOperationException("Negative chip movement.");
            _stack[seat] -= amount;
            _streetCommitted[seat] += amount;
            _handCommitted[seat] += amount;
            if (_stack[seat] == 0 && _status[seat] == SeatStatus.Active) _status[seat] = SeatStatus.AllIn;
        }

        // ---- Round / street progression -----------------------------------------------------

        /// <summary>Advance the action pointer after an action, settling the round if it is complete.</summary>
        private void AdvanceAfterAction()
        {
            // If only one non-folded player remains, the hand is over.
            if (CountInHand() <= 1)
            {
                ToAct = -1;
                Street = Street.Complete;
                return;
            }
            FindNextToAct(Next(ToAct));
        }

        /// <summary>
        /// Set <see cref="ToAct"/> to the next seat that still owes an action, searching clockwise
        /// from <paramref name="start"/> inclusive; sets it to -1 when the round is complete. A seat
        /// that already acted (flag set) is skipped, so passing the seat that just acted advances
        /// past it naturally.
        /// </summary>
        private void FindNextToAct(int start)
        {
            if (start < 0) start = 0;
            for (int k = 0; k < _seatCount; k++)
            {
                int seat = (start + k) % _seatCount;
                if (_status[seat] == SeatStatus.Active && !_hasActedThisRound[seat])
                {
                    ToAct = seat;
                    return;
                }
            }
            ToAct = -1; // round complete; caller advances the street
        }

        /// <summary>True when no active seat still owes an action this street.</summary>
        public bool IsBettingRoundComplete()
        {
            for (int i = 0; i < _seatCount; i++)
                if (_status[i] == SeatStatus.Active && !_hasActedThisRound[i])
                    return false;
            return true;
        }

        /// <summary>Number of seats still in the hand (not folded).</summary>
        private int CountInHand()
        {
            int n = 0;
            for (int i = 0; i < _seatCount; i++)
                if (_status[i] != SeatStatus.Folded) n++;
            return n;
        }

        /// <summary>Number of seats that can still voluntarily act (active with chips behind).</summary>
        private int CountActionable()
        {
            int n = 0;
            for (int i = 0; i < _seatCount; i++)
                if (_status[i] == SeatStatus.Active) n++;
            return n;
        }

        /// <summary>
        /// Move to the next street: reset the street commitments and bet line, deal the board, and set
        /// the first player to act. If the hand is already decided (one player left, or no one can act
        /// because everyone is all-in) this fast-forwards to showdown.
        /// </summary>
        public void AdvanceStreet()
        {
            if (Street == Street.Complete) return;
            if (!IsBettingRoundComplete())
                throw new InvalidOperationException("The betting round is not complete.");

            if (CountInHand() <= 1)
            {
                Street = Street.Complete;
                ToAct = -1;
                return;
            }

            if (Street == Street.River)
            {
                Street = Street.Complete;
                ToAct = -1;
                return;
            }

            // Reset street betting.
            for (int i = 0; i < _seatCount; i++)
            {
                _streetCommitted[i] = 0;
                _hasActedThisRound[i] = _status[i] != SeatStatus.Active;
            }
            CurrentBet = 0;
            LastRaiseSize = _rules.BigBlind;
            _reopenedForAll = true;

            Street = (Street)((int)Street + 1);
            DealBoardForStreet();

            // If at most one seat can still act, no betting happens — keep dealing to showdown.
            if (CountActionable() <= 1)
            {
                ToAct = -1;
                return;
            }

            FindNextToAct(FirstToActPostflop());
        }

        /// <summary>Deal this street's community cards from the pre-dealt board or the deck.</summary>
        private void DealBoardForStreet()
        {
            int target = Street switch
            {
                Street.Flop => 3,
                Street.Turn => 4,
                Street.River => 5,
                _ => _board.Count,
            };
            while (_board.Count < target)
            {
                if (_predealtBoard is not null)
                {
                    if (_board.Count >= _predealtBoard.Count)
                        throw new InvalidOperationException("Pre-dealt board is missing cards.");
                    _board.Add(_predealtBoard[_board.Count]);
                }
                else if (_deck is not null)
                {
                    _board.Add(_deck.Deal());
                }
                else
                {
                    throw new InvalidOperationException("No board source: provide a board or a deck.");
                }
            }
        }

        // ---- Completion / showdown ----------------------------------------------------------

        /// <summary>True when the hand has reached its terminal state and is ready to <see cref="Settle"/>.</summary>
        public bool IsHandComplete() => Street == Street.Complete || CountInHand() <= 1;

        /// <summary>
        /// Settle the hand at completion: evaluate showdown strengths for the non-folded seats, split
        /// the pot via <see cref="PotResolver"/>, pay winners back into their stacks, and return each
        /// seat's net result (winnings minus what it put in this hand). Idempotent in effect is not
        /// guaranteed — call exactly once when <see cref="IsHandComplete"/> is true.
        /// </summary>
        public int[] Settle()
        {
            if (!IsHandComplete())
                throw new InvalidOperationException("The hand is not complete.");

            var folded = new bool[_seatCount];
            var hands = new HandValue?[_seatCount];
            var contributions = new int[_seatCount];

            // Only run a real showdown when more than one player is left; if everyone else folded,
            // the lone survivor wins without revealing (and may not even have a five-card board).
            bool contested = CountInHand() > 1;

            for (int i = 0; i < _seatCount; i++)
            {
                contributions[i] = _handCommitted[i];
                folded[i] = _status[i] == SeatStatus.Folded;
                if (!folded[i] && contested) hands[i] = EvaluateSeat(i);
            }

            int[] winnings = PotResolver.Resolve(contributions, folded, hands, Button);

            var net = new int[_seatCount];
            for (int i = 0; i < _seatCount; i++)
            {
                _stack[i] += winnings[i];
                net[i] = winnings[i] - contributions[i];
            }
            return net;
        }

        /// <summary>Best five-card value for a seat using its hole cards and the board (needs 5+ cards).</summary>
        private HandValue EvaluateSeat(int seat)
        {
            Card[]? hc = _holeCards[seat];
            if (hc is null)
                throw new InvalidOperationException($"Seat {seat} reached showdown without known hole cards.");

            // If the hand ended before the river was dealt (everyone all-in earlier), fill the board
            // so a showdown can be evaluated. Only happens when there is a board source.
            EnsureBoardComplete();

            var cards = new List<Card>(hc.Length + _board.Count);
            cards.AddRange(hc);
            cards.AddRange(_board);
            return HandEvaluator.Evaluate(cards);
        }

        /// <summary>Run the board out to five cards for an all-in showdown, if a source is available.</summary>
        private void EnsureBoardComplete()
        {
            if (_board.Count >= 5) return;
            if (_predealtBoard is null && _deck is null) return; // explicit hole cards, no board source
            while (_board.Count < 5)
            {
                if (_predealtBoard is not null)
                {
                    if (_board.Count >= _predealtBoard.Count)
                        throw new InvalidOperationException("Pre-dealt board is missing cards for showdown.");
                    _board.Add(_predealtBoard[_board.Count]);
                }
                else
                {
                    _board.Add(_deck!.Deal());
                }
            }
        }
    }
}
