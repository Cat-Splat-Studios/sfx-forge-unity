// SPDX-License-Identifier: MIT
//
// How loud a sound is, and how to make sounds as loud as each other.
//   SfxLevelReading  a peak and an RMS
//   SfxLevelMeasure  which of the two to match by
//   SfxLevels        dB arithmetic, Measure, and Match: the LEVEL values that make sounds as loud as each other
using System;

namespace CatSplat.SfxForge
{
    /// <summary>How loud a rendered sound is: its peak and the RMS of its body.</summary>
    public struct SfxLevelReading
    {
        /// <summary>Linear; 0 = silent.</summary>
        public float peak;

        /// <summary>Linear: the RMS of the sound's body (SfxLevels.Measure says what the body is).</summary>
        public float rms;

        /// <summary>How many sample frames were measured.</summary>
        public int frames;

        /// <summary>The peak in dBFS, -120 for silence.</summary>
        public float PeakDb => SfxLevels.ToDb(peak);

        /// <summary>Plain RMS in dBFS: a full-scale sine reads -3.01.</summary>
        public float RmsDb => SfxLevels.ToDb(rms);

        /// <summary>The peak is over full scale: a 16-bit file would be clamped.</summary>
        public bool Clips => peak > 1f;

        /// <summary>The peak is under 0.00001 (-100 dBFS): there is nothing to hear.</summary>
        public bool Silent => peak < 0.00001f;
    }

    /// <summary>What sounds are matched by: the RMS of their bodies, or their peaks.</summary>
    public enum SfxLevelMeasure
    {
        Rms,
        Peak,
    }

    /// <summary>
    /// Levels in dB, and what a patch's LEVEL does to them. LEVEL is applied as (level / 100)^2 after everything that
    /// distorts, and a bake's effects are linear: a baked sound scales exactly with it, by 40 * log10(level / 100) dB.
    /// So sounds can be matched by arithmetic, without trying. This is exact for the peak. The RMS follows it as
    /// long as the sound is not very quiet: a bake ends where the sound falls under a fixed small level, so a
    /// sound whose peak is under about -26 dBFS loses the end of its body, and its RMS reads up to a few dB
    /// higher than the law says.
    /// </summary>
    public static class SfxLevels
    {
        /// <summary>What silence reads in dB: nothing reads lower.</summary>
        public const float Floor = -120f;

        /// <summary>The highest LEVEL a patch can have.</summary>
        public const int MaxLevel = 127;

        /// <summary>A linear level in dB: 1 is 0 dB, and anything at or under 0.000001 is Floor.</summary>
        public static float ToDb(float linear) => linear <= 0.000001f ? Floor : (float)(20.0 * Math.Log10(linear));

        /// <summary>A level in dB as a linear one: 0 dB is 1, and Floor or lower is 0.</summary>
        public static float FromDb(float db) => db <= Floor ? 0f : (float)Math.Pow(10.0, db / 20.0);

        /// <summary>
        /// peak: the largest sample of both channels. rms: over the sound's body - from the first to the last frame
        /// at or above a hundredth of the peak (40 dB under it), both channels together - so silence before it and
        /// a long quiet tail do not lower it, and it does not depend on LEVEL. right may be null (mono). frames
        /// below 0: all of left.
        /// </summary>
        public static SfxLevelReading Measure(float[] left, float[] right, int frames = -1)
        {
            var reading = new SfxLevelReading();
            if (left == null)
            {
                return reading;
            }

            if (frames < 0 || frames > left.Length)
            {
                frames = left.Length;
            }

            if (right != null && frames > right.Length)
            {
                frames = right.Length;
            }

            reading.frames = frames;
            float peak = 0f;
            for (int i = 0; i < frames; i++)
            {
                float a = Math.Abs(left[i]);
                if (a > peak)
                {
                    peak = a;
                }

                if (right != null)
                {
                    a = Math.Abs(right[i]);
                    if (a > peak)
                    {
                        peak = a;
                    }
                }
            }

            reading.peak = peak;
            if (peak <= 0f || float.IsNaN(peak))
            {
                return reading;
            }

            float gate = peak * 0.01f;
            int first = -1;
            int last = -1;
            for (int i = 0; i < frames; i++)
            {
                if (Math.Abs(left[i]) < gate && (right == null || Math.Abs(right[i]) < gate))
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }

                last = i;
            }

            if (first < 0)
            {
                return reading;
            }

            double sum = 0;
            for (int i = first; i <= last; i++)
            {
                sum += (double)left[i] * left[i];
                if (right != null)
                {
                    sum += (double)right[i] * right[i];
                }
            }

            reading.rms = (float)Math.Sqrt(sum / ((double)(last - first + 1) * (right != null ? 2 : 1)));
            return reading;
        }

        /// <summary>
        /// What a LEVEL does to the sound, in dB: 40 * log10(level / 100). 100 is 0, 127 is +4.15, 0 the floor.
        /// </summary>
        public static float LevelDb(int level) => level <= 0 ? Floor : (float)(40.0 * Math.Log10(level / 100.0));

        /// <summary>
        /// The LEVEL that makes a sound at <paramref name="level"/> louder by changeDb: 1..127. A sound at 0 stays at
        /// 0, and a change that is no number changes nothing.
        /// </summary>
        public static int LevelFor(int level, float changeDb)
        {
            if (level <= 0)
            {
                return 0;
            }

            if (float.IsNaN(changeDb))
            {
                return level > MaxLevel ? MaxLevel : level;
            }

            double v = Math.Round(level * Math.Pow(10.0, changeDb / 40.0), MidpointRounding.AwayFromZero);
            return v < 1 ? 1
                : v > MaxLevel ? MaxLevel
                : (int)v;
        }

        static float Of(SfxLevelReading r, SfxLevelMeasure by) => by == SfxLevelMeasure.Rms ? r.RmsDb : r.PeakDb;

        static bool Counts(int level, SfxLevelReading r) =>
            level > 0 && !r.Silent && !float.IsNaN(r.peak) && !float.IsNaN(r.rms);

        /// <summary>
        /// A new LEVEL for every sound (into newLevels) so that all read the same, and that reading in dB.
        /// readings[i] is sound i as it reads at levels[i]. wantedDb NaN: the reading is chosen - the loudest there
        /// is now, lowered to what every sound can reach with LEVEL 127 and to where no peak passes 0 dBFS. A
        /// wantedDb is lowered the same way, and raised to what the loudest sound still reads at LEVEL 1 (where that
        /// is not above the other limit):
        /// what is returned is the reading the sounds really get. A silent sound, one at LEVEL 0 and one whose reading
        /// is no number keep their level. LEVEL is a whole number: near 100 a step is 0.17 dB, at 20 it is 0.85 dB, at
        /// 5 it is 3.2 dB - quiet sounds agree less closely. NaN: there was nothing to match.
        /// </summary>
        public static float Match(
            int[] levels,
            SfxLevelReading[] readings,
            SfxLevelMeasure by,
            int[] newLevels,
            float wantedDb = float.NaN
        )
        {
            int n = Math.Min(levels.Length, Math.Min(readings.Length, newLevels.Length));
            float target = float.NaN;
            float reach = float.MaxValue;
            float least = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                newLevels[i] = levels[i];
                if (!Counts(levels[i], readings[i]))
                {
                    continue;
                }

                float reads = Of(readings[i], by);

                // the loudest now
                if (float.IsNaN(target) || reads > target)
                {
                    target = reads;
                }

                // what LEVEL 127 gives
                reach = Math.Min(reach, reads + LevelDb(MaxLevel) - LevelDb(levels[i]));
                reach = Math.Min(reach, reads - readings[i].PeakDb); // its peak at 0 dBFS

                // what LEVEL 1 leaves
                least = Math.Max(least, reads + LevelDb(1) - LevelDb(levels[i]));
            }

            if (float.IsNaN(target))
            {
                return float.NaN;
            }

            if (!float.IsNaN(wantedDb))
            {
                target = wantedDb;
            }

            if (target < least)
            {
                target = least;
            }

            if (target > reach)
            {
                target = reach;
            }

            for (int i = 0; i < n; i++)
            {
                if (!Counts(levels[i], readings[i]))
                {
                    continue;
                }

                int level = LevelFor(levels[i], target - Of(readings[i], by));

                // (rounded up over full scale)
                if (level > 1 && readings[i].PeakDb + LevelDb(level) - LevelDb(levels[i]) > 0f)
                {
                    level--;
                }

                newLevels[i] = level;
            }

            return target;
        }
    }
}
