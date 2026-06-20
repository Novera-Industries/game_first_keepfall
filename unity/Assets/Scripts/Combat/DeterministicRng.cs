namespace Keepfall.Combat
{
    /// <summary>
    /// Tiny, allocation-free, fully deterministic PRNG (SplitMix64) used across combat so that a
    /// match replays IDENTICALLY from its <c>matchSeed</c> (source-of-truth §4 + §6 Product 3:
    /// retry restores "identical AI, map seed, and starting hand"). We do NOT use
    /// <c>System.Random</c> or <c>UnityEngine.Random</c> because their cross-version /
    /// cross-platform determinism is not guaranteed; SplitMix64 is a fixed, specified algorithm.
    /// Pure C# (no UnityEngine) so it runs in EditMode tests.
    /// </summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        /// <summary>Creates a generator seeded for a match. Same seed → same sequence, always.</summary>
        public DeterministicRng(ulong seed)
        {
            _state = seed;
        }

        /// <summary>The current internal state (so a sub-system can fork a stable child stream).</summary>
        public ulong State => _state;

        /// <summary>Returns the next 64-bit value (SplitMix64).</summary>
        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Returns a non-negative int in [0, <paramref name="exclusiveMax"/>).</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                return 0;
            }

            // Unbiased-enough modulo for gameplay; range here is tiny (deck/hand sizes).
            return (int)(NextUInt64() % (ulong)exclusiveMax);
        }

        /// <summary>Returns a double in [0, 1).</summary>
        public double NextDouble()
        {
            // Top 53 bits → uniform double in [0,1) (matches IEEE-754 mantissa width).
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>
        /// Derives a stable child seed from this stream and a label hash, so independent
        /// sub-systems (hand vs AI vs map) draw from separate, reproducible streams without
        /// stepping on each other's ordering.
        /// </summary>
        public static ulong DeriveSeed(ulong rootSeed, ulong salt)
        {
            unchecked
            {
                ulong z = rootSeed + salt * 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
