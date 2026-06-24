using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using Xunit;

namespace PokerEngine.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new DeterministicRandom(12345);
        var b = new DeterministicRandom(12345);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void ZeroSeed_DoesNotCollapseToZeroStream()
    {
        var rng = new DeterministicRandom(0);
        Assert.Contains(Enumerable.Range(0, 10).Select(_ => rng.NextUInt64()), v => v != 0);
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var rng = new DeterministicRandom(7);
        for (int i = 0; i < 10_000; i++)
            Assert.InRange(rng.NextInt(0, 52), 0, 51);
    }
}

public class CardTests
{
    [Fact]
    public void IndexRoundTrips_ForAll52()
    {
        for (int i = 0; i < 52; i++)
            Assert.Equal(i, Card.FromIndex(i).Index);
    }

    [Theory]
    [InlineData("As", Rank.Ace, Suit.Spades)]
    [InlineData("Td", Rank.Ten, Suit.Diamonds)]
    [InlineData("2c", Rank.Two, Suit.Clubs)]
    [InlineData("Kh", Rank.King, Suit.Hearts)]
    public void ParseAndToString_RoundTrip(string text, Rank rank, Suit suit)
    {
        var card = Card.Parse(text);
        Assert.Equal(rank, card.Rank);
        Assert.Equal(suit, card.Suit);
        Assert.Equal(text, card.ToString());
    }

    [Fact]
    public void Equality_IsByRankAndSuit()
    {
        Assert.Equal(new Card(Rank.Ace, Suit.Spades), Card.Parse("As"));
        Assert.NotEqual(new Card(Rank.Ace, Suit.Spades), new Card(Rank.Ace, Suit.Hearts));
    }
}

public class DeckTests
{
    [Fact]
    public void FreshDeck_Has52UniqueCards()
    {
        var deck = new Deck();
        Assert.Equal(52, deck.Remaining);
        var all = deck.Deal(52);
        Assert.Equal(52, all.Distinct().Count());
    }

    [Fact]
    public void Shuffle_IsDeterministicForSameSeed()
    {
        var a = new Deck();
        var b = new Deck();
        a.Shuffle(new DeterministicRandom(99));
        b.Shuffle(new DeterministicRandom(99));
        Assert.Equal(a.Deal(52), b.Deal(52));
    }

    [Fact]
    public void Shuffle_PreservesTheFull52()
    {
        var deck = new Deck();
        deck.Shuffle(new DeterministicRandom(3));
        Assert.Equal(52, deck.Deal(52).Distinct().Count());
    }

    [Fact]
    public void DifferentSeeds_GenerallyDiffer()
    {
        var a = new Deck();
        var b = new Deck();
        a.Shuffle(new DeterministicRandom(1));
        b.Shuffle(new DeterministicRandom(2));
        Assert.NotEqual<IReadOnlyList<Card>>(a.Deal(52).ToList(), b.Deal(52).ToList());
    }

    [Fact]
    public void Deal_ReducesRemaining()
    {
        var deck = new Deck();
        deck.Deal(2);
        Assert.Equal(50, deck.Remaining);
    }
}
