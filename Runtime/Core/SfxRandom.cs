// SPDX-License-Identifier: MIT
namespace CatSplat.SfxForge
{
    /// <summary>
    /// The device's own generator (xorshift32, sfx__rand in core/sfx_core.h): the same seed gives the same numbers on
    /// every platform and runtime, which System.Random does not promise. Used wherever a seed must give a sound again.
    /// </summary>
    public struct SfxRandom
    {
        const uint Fallback = 0x9E3779B9u;
        uint state;

        /// <summary>0 is taken as 0x9E3779B9 (the generator cannot leave 0).</summary>
        public SfxRandom(uint seed)
        {
            state = seed != 0 ? seed : Fallback;
        }

        /// <summary>The next number: any 32 bits except 0.</summary>
        public uint Next()
        {
            uint x = state != 0 ? state : Fallback;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>
        /// low..high, both inclusive: low + Next() % (high - low + 1). One draw, also when low = high.
        /// </summary>
        public int Range(int low, int high)
        {
            uint r = Next();
            return high <= low ? low : low + (int)(r % (uint)(high - low + 1));
        }

        /// <summary>0 &lt;= x &lt; 1.</summary>
        public float Unit() => (Next() >> 8) / 16777216f;

        /// <summary>
        /// A seed for stream <paramref name="index"/> of a seed: the streams of one seed do not follow
        /// each other. Never 0.
        /// </summary>
        public static uint Mix(uint seed, uint index)
        {
            var r = new SfxRandom(seed ^ (index * 0x9E3779B9u + 0x7F4A7C15u));
            r.Next();
            r.Next();
            return r.Next();
        }
    }
}
