using System;

namespace PokerEngine.Core
{
    /// <summary>
    /// The single seeded PRNG the engine owns. We do NOT use <see cref="System.Random"/>:
    /// its algorithm is not guaranteed stable across runtimes, which would break the
    /// determinism contract (same state + model + seed ⇒ same decision; see
    /// docs/DECISION_ALGORITHM §9). Implementation: xorshift128+ seeded via SplitMix64 —
    /// fast, well-distributed, identical on every platform for a given seed.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _s0;
        private ulong _s1;

        public DeterministicRandom(ulong seed)
        {
            Seed = seed;
            ulong z = seed;
            _s0 = SplitMix64(ref z);
            _s1 = SplitMix64(ref z);
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL; // avoid the all-zero fixed point
        }

        /// <summary>The seed this generator was constructed with (for logging/replays).</summary>
        public ulong Seed { get; }

        private static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Next raw 64-bit value (xorshift128+).</summary>
        public ulong NextUInt64()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            ulong result = s0 + s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
            return result;
        }

        /// <summary>A double in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>An int in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must exceed minInclusive.");
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % range);
        }
    }
}
