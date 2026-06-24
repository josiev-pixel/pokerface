using System.Collections.Generic;

namespace PokerEngine.Core
{
    /// <summary>
    /// A standard 52-card deck with a seeded, deterministic shuffle (Fisher–Yates over the
    /// engine's <see cref="DeterministicRandom"/>). Same seed ⇒ same order, every platform —
    /// the basis for reproducible deals in equity sampling and self-play (DECISION_ALGORITHM §9).
    /// </summary>
    public sealed class Deck
    {
        private readonly List<Card> _cards;
        private int _next;

        /// <summary>A fresh deck in canonical order (index 0..51), not yet shuffled.</summary>
        public Deck()
        {
            _cards = new List<Card>(52);
            for (int i = 0; i < 52; i++) _cards.Add(Card.FromIndex(i));
        }

        /// <summary>Cards remaining to be dealt.</summary>
        public int Remaining => _cards.Count - _next;

        /// <summary>Shuffle in place with a seeded Fisher–Yates pass and reset the deal pointer.</summary>
        public void Shuffle(DeterministicRandom rng)
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
            _next = 0;
        }

        /// <summary>Deal the next card off the top.</summary>
        public Card Deal()
        {
            if (Remaining <= 0) throw new System.InvalidOperationException("The deck is empty.");
            return _cards[_next++];
        }

        /// <summary>Deal <paramref name="count"/> cards.</summary>
        public IReadOnlyList<Card> Deal(int count)
        {
            var dealt = new List<Card>(count);
            for (int i = 0; i < count; i++) dealt.Add(Deal());
            return dealt;
        }
    }
}
