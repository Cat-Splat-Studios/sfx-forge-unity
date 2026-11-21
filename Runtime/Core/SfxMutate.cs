// SPDX-License-Identifier: MIT
namespace CatSplat.SfxForge
{
    /// <summary>
    /// Variants of a sound that can be made again: the MORE LIKE THIS grid of the sound's Inspector and a variation set
    /// both ask Variant, so tile n there and variant n here are the same patch for the same base, seed, amount and locks.
    /// </summary>
    public static class SfxMutate
    {
        /// <summary>Every value locked.</summary>
        public const uint All = (1u << SfxPatch.ParamCount) - 1;

        /// <summary>The bit of a value in a set of locked values.</summary>
        public static uint Bit(SfxParam p) => 1u << (int)p;

        /// <summary>
        /// Variant <paramref name="index"/> of a base: 0 is the base itself; n above 0 is the base mutated by up to
        /// <paramref name="percent"/> with the generator SfxRandom(Mix(seed, n)). A variant depends on the base, the seed,
        /// its own number, the amount and the locks - not on how many others there are or which were asked for first.
        /// </summary>
        public static SfxPatch Variant(SfxPatch basePatch, uint seed, int index, int percent, uint locked = 0)
        {
            if (index <= 0) return basePatch;
            var rng = new SfxRandom(SfxRandom.Mix(seed, (uint)index));
            basePatch.Mutate(ref rng, SfxPatch.StepOf(percent), locked);
            return basePatch;
        }

        /// <summary>
        /// A number for everything a variation set's clips are made from (FNV-1a, 32 bit): the packed patch, the hold in
        /// hundredths of a second, seed, count, amount, locks, mono - and the number of the recipe itself (1). It changes
        /// when one of them does. Never 0: that is "nothing to bake from".
        /// </summary>
        public static int Stamp(byte[] patch, int holdHundredths, uint seed, int count, int percent, uint locked, bool mono)
        {
            const uint Recipe = 1;
            uint h = 2166136261u;
            if (patch != null) foreach (byte b in patch) h = (h ^ b) * 16777619u;
            h = Add(h, (uint)holdHundredths); h = Add(h, seed); h = Add(h, (uint)count); h = Add(h, (uint)percent); h = Add(h, locked);
            h = Add(h, mono ? 1u : 0u); h = Add(h, Recipe);
            return h == 0 ? 1 : unchecked((int)h);
        }

        static uint Add(uint h, uint v)
        {
            for (int i = 0; i < 4; i++) { h = (h ^ (v & 0xFF)) * 16777619u; v >>= 8; }
            return h;
        }
    }
}
