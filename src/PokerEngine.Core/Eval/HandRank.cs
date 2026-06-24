namespace PokerEngine.Core.Eval
{
    /// <summary>
    /// The nine poker hand categories, ordered so a higher value beats a lower one.
    /// Used as the most-significant part of a packed <see cref="HandValue"/>.
    /// </summary>
    public enum HandRank
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        FourOfAKind = 7,
        StraightFlush = 8,
    }
}
