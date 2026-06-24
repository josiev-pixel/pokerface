using System;
using System.Collections.Generic;

namespace PokerEngine.Core.Equity
{
    /// <summary>
    /// Builds concrete hole card combinations from starting hand buckets (e.g. AA expands to 6 specific hole cards).
    /// </summary>
    public static class RangeBuilder
    {
        /// <summary>
        /// Expands a starting hand bucket into all its concrete two-card combinations.
        /// For pairs: the 6 suit-pairs (e.g. AA ? [AhAs, AdAc, AhAd, AcAd, AsAd, AsAc]).
        /// For suited: the 4 same-suit combos (e.g. AKs ? [AhKs, AdKd, AcKc, AsKs]).
        /// For offsuit: the 12 different-suit combos (e.g. AKo ? [AhKd, AhKc, AhKs, AdKc, AdKs, AdKc, AcKs, AcKd, AcKd, AsKd, AsKc, AsKd]).
        /// </summary>
        public static IReadOnlyList<HoleCards> Expand(StartingHand hand)
        {
            var result = new List<HoleCards>();
            
            // Handle the case where both ranks are the same (pair)
            if (hand.IsPair)
            {
                // For pairs, we want all 6 combinations of suits for two cards with the same rank
                for (int i = 0; i < 4; i++)
                {
                    for (int j = i + 1; j < 4; j++)
                    {
                        var card1 = new Card(hand.High, (Suit)i);
                        var card2 = new Card(hand.High, (Suit)j);
                        result.Add(new HoleCards(card1, card2));
                    }
                }
            }
            else
            {
                // For non-pairs (suited or off-suit)
                for (int suit1 = 0; suit1 < 4; suit1++)
                {
                    var card1 = new Card(hand.High, (Suit)suit1);
                    for (int suit2 = 0; suit2 < 4; suit2++)
                    {
                        var card2 = new Card(hand.Low, (Suit)suit2);
                        
                        // Skip if it would be the same card
                        if (card1.Index == card2.Index)
                            continue;
                            
                        // For suited hands, only allow same-suit combinations
                        if (hand.Suited && suit1 != suit2)
                            continue;

                        // For off-suited hands, only allow different-suit combinations
                        if (!hand.Suited && suit1 == suit2)
                            continue;

                        result.Add(new HoleCards(card1, card2));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a starting hand text (e.g. "AA", "AKs", "AKo") and expands it into concrete hole cards.
        /// </summary>
        public static IReadOnlyList<HoleCards> Expand(string handText) => Expand(StartingHand.Parse(handText));
    }
}
