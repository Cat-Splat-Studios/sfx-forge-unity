// SPDX-License-Identifier: MIT
namespace CatSplat.SfxForge
{
    /// <summary>The kinds of sound SfxGenerator makes from nothing.</summary>
    public enum SfxCategory { Click, Chime, Leap, Rise, Zap, Thud, Blast, Door, Alarm, Hum }

    /// <summary>
    /// A new sound of a kind, from a seed: a click, a chime, a zap, a blast ... Each kind has two recipes; a recipe is an
    /// engine, a range for each value it names, and values that are 0 half the time. What a recipe does not name is the
    /// plain patch's (SfxPatch.Create); LEVEL is always 100. The same category and seed give the same patch on every
    /// platform and runtime (SfxRandom). The kinds, their names and the recipes are this engine's own: made for its
    /// five engines, from the smallest sound to the largest, then the three that are things (two of them held).
    /// </summary>
    public static class SfxGenerator
    {
        public const int Count = 10;
        public static readonly string[] Names = { "CLICK", "CHIME", "LEAP", "RISE", "ZAP", "THUD", "BLAST", "DOOR", "ALARM", "HUM" };

        // the values a recipe names (SfxParam, in wire order), and the mark for "0 half the time"
        const int Freq = 0, PitchEnv = 1, EnvTime = 2, A = 3, B = 4, Noise = 5, Cutoff = 6, Reso = 7, Decay = 8, Crush = 9, Attack = 10, Sustain = 11, Release = 12,
                  FilterEnv = 13, LfoRate = 15, ToPitch = 16, ToFilter = 17, ToTimbre = 18, Spread = 19, Chorus = 20, Delay = 21, Reverb = 22, Table = 24;
        const int OrOff = 256;

        sealed class Recipe
        {
            public SfxEngine engine;
            public readonly byte[] low = new byte[SfxPatch.ParamCount], high = new byte[SfxPatch.ParamCount];
            public uint orOff;                                  // a bit for each value that is 0 half the time
        }

        // value, low, high - three numbers for each value named
        static Recipe Make(SfxEngine engine, params int[] named)
        {
            var r = new Recipe { engine = engine };
            SfxPatch plain = SfxPatch.Create(engine);
            for (int i = 0; i < SfxPatch.ParamCount; i++) r.low[i] = r.high[i] = plain[(SfxParam)i];
            for (int i = 0; i + 2 < named.Length; i += 3)
            {
                int k = named[i] & 255;
                r.low[k] = (byte)named[i + 1]; r.high[k] = (byte)named[i + 2];
                if ((named[i] & OrOff) != 0) r.orOff |= 1u << k;
            }
            return r;
        }

        // [category][recipe]. A and B are an engine's two own values: FM RATIO / FM DEPTH, ARP STEP / ARP SPEED,
        // GRAIN RATE / GRAIN AMT, POSITION / WARP. FREQ 60 is about 450 Hz; PITCH ENV above 64 falls to the pitch, below it rises.
        static readonly Recipe[][] Recipes =
        {
            new[] {     // CLICK: the shortest
                Make(SfxEngine.UiBlip, Freq, 60, 92, A | OrOff, 20, 64, B, 80, 120, Cutoff, 110, 127, Reso, 0, 15, Decay, 14, 32),
                Make(SfxEngine.SciFiFm, Freq, 66, 96, PitchEnv, 64, 80, EnvTime, 10, 30, A, 40, 90, B, 10, 45, Decay, 12, 28),
            },
            new[] {     // CHIME: a bright step up, or a small bell
                Make(SfxEngine.UiBlip, Freq, 72, 92, A, 20, 66, B, 70, 110, Cutoff, 110, 127, Reso, 0, 20, Decay, 38, 62, Delay | OrOff, 0, 20, Reverb, 0, 25),
                Make(SfxEngine.SciFiFm, Freq, 78, 96, A, 60, 100, B, 20, 50, Decay, 45, 70, Reverb, 10, 35),
            },
            new[] {     // LEAP: a short rise, one note
                Make(SfxEngine.UiBlip, Freq, 50, 74, PitchEnv, 30, 54, EnvTime, 35, 65, A, 0, 0, B, 0, 40, Cutoff, 95, 127, Reso, 0, 25, Decay, 28, 48),
                Make(SfxEngine.Wavetable, Table, 0, 1, Freq, 48, 70, PitchEnv, 28, 52, EnvTime, 35, 65, A, 0, 90, Cutoff, 95, 127, Decay, 28, 48),
            },
            new[] {     // RISE: a long rise
                Make(SfxEngine.UiBlip, Freq, 48, 70, PitchEnv, 20, 50, EnvTime, 60, 95, A, 15, 64, B, 40, 80, Cutoff, 105, 127, Decay, 62, 82, LfoRate, 70, 100, ToPitch | OrOff, 0, 35, Delay, 10, 45, Reverb, 0, 30),
                Make(SfxEngine.Wavetable, Table, 0, 5, Freq, 45, 65, PitchEnv, 14, 44, EnvTime, 65, 100, A, 0, 60, B, 0, 60, Cutoff, 100, 127, FilterEnv, 30, 60, Decay, 66, 84, LfoRate, 40, 90, ToTimbre, 30, 90, Spread, 10, 50, Chorus, 0, 40, Reverb, 10, 35),
            },
            new[] {     // ZAP: a fast fall
                Make(SfxEngine.SciFiFm, Freq, 55, 85, PitchEnv, 92, 122, EnvTime, 25, 60, A, 30, 90, B, 40, 100, Cutoff, 100, 127, Reso, 0, 40, Decay, 40, 66, Crush | OrOff, 0, 40, Delay | OrOff, 0, 30),
                Make(SfxEngine.Wavetable, Table, 0, 1, Freq, 55, 80, PitchEnv, 96, 124, EnvTime, 20, 50, A, 0, 127, B, 0, 80, Cutoff, 90, 127, FilterEnv, 70, 100, Decay, 35, 60, Spread, 0, 40),
            },
            new[] {     // THUD: short and low
                Make(SfxEngine.Impact, Freq, 22, 48, PitchEnv, 76, 104, EnvTime, 15, 40, A, 0, 70, B, 30, 100, Noise, 20, 80, Cutoff, 70, 110, Reso, 0, 25, Decay, 22, 42, Crush | OrOff, 0, 50),
                Make(SfxEngine.Foley, Freq, 30, 50, PitchEnv, 70, 96, EnvTime, 15, 35, Noise, 90, 127, Cutoff, 60, 100, Reso, 20, 60, Decay, 18, 36),
            },
            new[] {     // BLAST: low noise that takes its time
                Make(SfxEngine.Foley, Freq, 20, 40, PitchEnv, 80, 110, EnvTime, 55, 95, A, 0, 50, B, 0, 70, Noise, 110, 127, Cutoff, 55, 90, Reso, 10, 50, Decay, 62, 84, Crush | OrOff, 0, 45, Delay | OrOff, 0, 25, Reverb, 10, 50),
                Make(SfxEngine.Impact, Freq, 14, 32, PitchEnv, 84, 112, EnvTime, 40, 75, A, 0, 40, B, 40, 110, Noise, 40, 100, Cutoff, 60, 95, Reso, 0, 30, Decay, 66, 84, Reverb, 10, 45),
            },
            new[] {     // DOOR: a creak, or it shuts
                Make(SfxEngine.Foley, Freq, 24, 44, PitchEnv, 40, 90, EnvTime, 70, 110, A, 20, 70, B, 70, 127, Noise, 90, 127, Cutoff, 45, 80, Reso, 40, 90, Attack, 20, 55, Decay, 60, 80, LfoRate, 20, 60, ToFilter, 0, 50, Reverb, 10, 40),
                Make(SfxEngine.Impact, Freq, 18, 34, PitchEnv, 70, 90, EnvTime, 15, 35, B, 20, 60, Noise, 30, 70, Cutoff, 55, 85, Decay, 30, 48, Reverb, 10, 40),
            },
            new[] {     // ALARM: held, and its pitch swings
                Make(SfxEngine.SciFiFm, Freq, 62, 84, A, 30, 70, B, 10, 50, Cutoff, 100, 127, Sustain, 90, 127, Release, 15, 40, LfoRate, 60, 105, ToPitch, 60, 127),
                Make(SfxEngine.Wavetable, Table, 1, 1, Freq, 60, 82, A, 0, 127, Cutoff, 100, 127, Sustain, 90, 127, Release, 15, 40, LfoRate, 70, 100, ToPitch, 70, 127),
            },
            new[] {     // HUM: held, low, slowly alive
                Make(SfxEngine.SciFiFm, Freq, 14, 32, EnvTime, 0, 0, A, 10, 40, B, 35, 75, Noise, 0, 20, Cutoff, 60, 90, Reso, 20, 60, Sustain, 110, 127, Release, 30, 50, LfoRate, 30, 80, ToPitch | OrOff, 0, 12, ToTimbre, 0, 40),
                Make(SfxEngine.Wavetable, Table, 0, 5, Freq, 14, 34, A, 0, 127, B, 0, 70, Cutoff, 55, 90, Attack, 20, 50, Sustain, 110, 127, Release, 30, 50, LfoRate, 15, 50, ToTimbre, 20, 80, Spread, 20, 70, Chorus, 20, 60),
            },
        };

        static Recipe[] Of(SfxCategory category) => Recipes[(int)category >= 0 && (int)category < Count ? (int)category : 0];

        /// <summary>The sound of this category and seed: always the same one.</summary>
        public static SfxPatch Make(SfxCategory category, uint seed)
        {
            var rng = new SfxRandom(seed);
            return Make(category, ref rng);
        }

        /// <summary>
        /// The next sound of a generator. What is drawn, in this order: the recipe (0 or 1); for each of the 25 values in
        /// wire order one number inside its range (also where the range is one value); then for each value that is 0 half
        /// the time, in wire order, one bit.
        /// </summary>
        public static SfxPatch Make(SfxCategory category, ref SfxRandom rng)
        {
            Recipe r = Of(category)[rng.Range(0, 1)];
            var p = new SfxPatch { engine = r.engine };
            for (int i = 0; i < SfxPatch.ParamCount; i++) p[(SfxParam)i] = (byte)(r.low[i] + rng.Range(0, r.high[i] - r.low[i]));
            for (int i = 0; i < SfxPatch.ParamCount; i++)
                if ((r.orOff & (1u << i)) != 0 && (rng.Next() & 1) != 0) p[(SfxParam)i] = 0;
            return p;
        }

        /// <summary>A category whose sounds are held while their pad or note is (ALARM, HUM); the others are one-shots.</summary>
        public static bool IsSustained(SfxCategory category) => category == SfxCategory.Alarm || category == SfxCategory.Hum;

        /// <summary>The patch is one a recipe of this category can give: its engine, and every value inside the recipe's range.</summary>
        public static bool Fits(SfxCategory category, SfxPatch patch)
        {
            foreach (Recipe r in Of(category))
            {
                if (r.engine != patch.engine) continue;
                bool inside = true;
                for (int i = 0; i < SfxPatch.ParamCount && inside; i++)
                {
                    int v = patch[(SfxParam)i];
                    inside = (v >= r.low[i] && v <= r.high[i]) || (v == 0 && (r.orOff & (1u << i)) != 0);
                }
                if (inside) return true;
            }
            return false;
        }
    }
}
