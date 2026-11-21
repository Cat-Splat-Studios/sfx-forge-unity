// SPDX-License-Identifier: MIT
namespace CatSplat.SfxForge
{
    /// <summary>
    /// The order a variation set's variants are played in: random, but never one of the last few again. With a seed it
    /// is the same order every time (SfxRandom).
    /// </summary>
    public sealed class SfxShuffle
    {
        readonly int count;
        readonly int[] recent;                  // the last ones returned (a ring)
        int held, head, last = -1;
        SfxRandom rng;

        /// <summary>count: how many there are (at least 1). noRepeat: the last N returned are not chosen (at most count - 1 of them).</summary>
        public SfxShuffle(int count, int noRepeat, uint seed)
        {
            this.count = count < 1 ? 1 : count;
            int avoid = noRepeat < 0 ? 0 : noRepeat > this.count - 1 ? this.count - 1 : noRepeat;
            recent = new int[avoid];
            rng = new SfxRandom(seed);
        }

        public int Count => count;
        /// <summary>How many of the last ones returned are not chosen: noRepeat, at most count - 1.</summary>
        public int NoRepeat => recent.Length;
        /// <summary>What Next returned last, -1 before the first.</summary>
        public int Last => last;

        /// <summary>0..count-1, each of those not among the last min(noRepeat, count - 1) returned as likely as the others.</summary>
        public int Next()
        {
            int pick = rng.Range(0, count - held - 1), at = 0;
            for (int i = 0; i < count; i++)
            {
                if (IsRecent(i)) continue;
                if (pick-- == 0) { at = i; break; }
            }
            if (recent.Length > 0)
            {
                recent[head] = at; head = (head + 1) % recent.Length;
                if (held < recent.Length) held++;
            }
            return last = at;
        }

        /// <summary>0 &lt;= x &lt; 1 from the same generator: what a play's pitch and volume are drawn with.</summary>
        public float NextUnit() => rng.Unit();

        bool IsRecent(int i)
        {
            for (int k = 0; k < held; k++) if (recent[k] == i) return true;
            return false;
        }
    }
}
