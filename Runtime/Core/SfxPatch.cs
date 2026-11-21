// SPDX-License-Identifier: MIT
using System;

namespace CatSplat.SfxForge
{
    public enum SfxEngine : byte { Impact, UiBlip, SciFiFm, Foley, Wavetable }

    /// <summary>The parameters of a patch, in wire order.</summary>
    public enum SfxParam
    {
        BaseFreq, PitchEnvAmount, PitchEnvDecay, FmRatio, FmDepth, NoiseMix, FilterCutoff, FilterRes, AmpDecay, Bitcrush,
        Attack, Sustain, Release, FilterEnv, FilterEnvDecay, LfoRate, LfoPitch, LfoFilter, LfoTimbre, Spread, Chorus, Delay, Reverb, Level, Table
    }

    /// <summary>
    /// One SFX FORGE sound: the same 28 bytes the device keeps (core/sfx_core.h). Every value is 0..127.
    /// Start from <see cref="Create"/>: a new SfxPatch() has level 0 and is silent.
    /// </summary>
    [Serializable]
    public struct SfxPatch : IEquatable<SfxPatch>
    {
        public const int Version = 2;
        public const int ParamCount = 25;
        public const int Bytes = 3 + ParamCount;
        public const int EngineCount = 5;
        public const int TableCount = 6;

        public SfxEngine engine;
        public byte baseFreq, pitchEnvAmount, pitchEnvDecay, fmRatio, fmDepth, noiseMix, filterCutoff, filterRes, ampDecay, bitcrush;
        public byte attack, sustain, release, filterEnv, filterEnvDecay, lfoRate, lfoPitch, lfoFilter, lfoTimbre, spread, chorus, delay, reverb, level, table;

        static readonly byte[] Plain = { 60, 64, 40, 40, 40, 0, 110, 10, 60, 0, 0, 0, 34, 64, 40, 50, 0, 0, 0, 0, 0, 0, 0, 100, 0 };

        /// <summary>
        /// A plain patch of an engine (a short one-shot, nothing modulated, no effects), with the first parameters
        /// set from <paramref name="first"/> in wire order: FREQ, PITCH ENV, ENV TIME, A, B, NOISE, CUTOFF, RESO, DECAY, CRUSH …
        /// </summary>
        public static SfxPatch Create(SfxEngine engine, params byte[] first)
        {
            var p = new SfxPatch { engine = engine };
            for (int i = 0; i < ParamCount; i++) p[(SfxParam)i] = first != null && i < first.Length ? first[i] : Plain[i];
            return p;
        }

        /// <summary>A laser.</summary>
        public static SfxPatch Default => Create(SfxEngine.SciFiFm, 70, 110, 45, 50, 70, 0, 120, 20, 58, 0);

        /// <summary>Held while its pad or note is held (sustain above 0); otherwise a one-shot that plays to its end.</summary>
        public bool IsSustained => sustain > 0;

        public byte this[SfxParam p]
        {
            get
            {
                switch (p)
                {
                    case SfxParam.BaseFreq: return baseFreq;
                    case SfxParam.PitchEnvAmount: return pitchEnvAmount;
                    case SfxParam.PitchEnvDecay: return pitchEnvDecay;
                    case SfxParam.FmRatio: return fmRatio;
                    case SfxParam.FmDepth: return fmDepth;
                    case SfxParam.NoiseMix: return noiseMix;
                    case SfxParam.FilterCutoff: return filterCutoff;
                    case SfxParam.FilterRes: return filterRes;
                    case SfxParam.AmpDecay: return ampDecay;
                    case SfxParam.Bitcrush: return bitcrush;
                    case SfxParam.Attack: return attack;
                    case SfxParam.Sustain: return sustain;
                    case SfxParam.Release: return release;
                    case SfxParam.FilterEnv: return filterEnv;
                    case SfxParam.FilterEnvDecay: return filterEnvDecay;
                    case SfxParam.LfoRate: return lfoRate;
                    case SfxParam.LfoPitch: return lfoPitch;
                    case SfxParam.LfoFilter: return lfoFilter;
                    case SfxParam.LfoTimbre: return lfoTimbre;
                    case SfxParam.Spread: return spread;
                    case SfxParam.Chorus: return chorus;
                    case SfxParam.Delay: return delay;
                    case SfxParam.Reverb: return reverb;
                    case SfxParam.Level: return level;
                    default: return table;
                }
            }
            set
            {
                byte v = value > 127 ? (byte)127 : value;
                switch (p)
                {
                    case SfxParam.BaseFreq: baseFreq = v; break;
                    case SfxParam.PitchEnvAmount: pitchEnvAmount = v; break;
                    case SfxParam.PitchEnvDecay: pitchEnvDecay = v; break;
                    case SfxParam.FmRatio: fmRatio = v; break;
                    case SfxParam.FmDepth: fmDepth = v; break;
                    case SfxParam.NoiseMix: noiseMix = v; break;
                    case SfxParam.FilterCutoff: filterCutoff = v; break;
                    case SfxParam.FilterRes: filterRes = v; break;
                    case SfxParam.AmpDecay: ampDecay = v; break;
                    case SfxParam.Bitcrush: bitcrush = v; break;
                    case SfxParam.Attack: attack = v; break;
                    case SfxParam.Sustain: sustain = v; break;
                    case SfxParam.Release: release = v; break;
                    case SfxParam.FilterEnv: filterEnv = v; break;
                    case SfxParam.FilterEnvDecay: filterEnvDecay = v; break;
                    case SfxParam.LfoRate: lfoRate = v; break;
                    case SfxParam.LfoPitch: lfoPitch = v; break;
                    case SfxParam.LfoFilter: lfoFilter = v; break;
                    case SfxParam.LfoTimbre: lfoTimbre = v; break;
                    case SfxParam.Spread: spread = v; break;
                    case SfxParam.Chorus: chorus = v; break;
                    case SfxParam.Delay: delay = v; break;
                    case SfxParam.Reverb: reverb = v; break;
                    case SfxParam.Level: level = v; break;
                    default: table = v; break;
                }
            }
        }

        public static readonly string[] EngineNames = { "Impact", "UI Blip", "Sci-Fi FM", "Foley", "Wavetable" };
        public static readonly string[] TableNames = { "Saw to Square", "Pulse", "Peak", "Vowel", "Organ", "Glass" };

        /// <summary>What a parameter is called for an engine (A and B change their job per engine).</summary>
        public static string Label(SfxParam p, SfxEngine engine)
        {
            if (engine == SfxEngine.UiBlip && p == SfxParam.FmRatio) return "Arp Step";
            if (engine == SfxEngine.UiBlip && p == SfxParam.FmDepth) return "Arp Speed";
            if (engine == SfxEngine.Foley && p == SfxParam.FmRatio) return "Grain Rate";
            if (engine == SfxEngine.Foley && p == SfxParam.FmDepth) return "Grain Amount";
            if (engine == SfxEngine.Foley && p == SfxParam.FilterCutoff) return "Band";
            if (engine == SfxEngine.Wavetable && p == SfxParam.FmRatio) return "Position";
            if (engine == SfxEngine.Wavetable && p == SfxParam.FmDepth) return "Warp";
            switch (p)
            {
                case SfxParam.BaseFreq: return "Frequency";
                case SfxParam.PitchEnvAmount: return "Pitch Env (64 = none)";
                case SfxParam.PitchEnvDecay: return "Pitch Env Time";
                case SfxParam.FmRatio: return "FM Ratio";
                case SfxParam.FmDepth: return "FM Depth";
                case SfxParam.NoiseMix: return "Noise";
                case SfxParam.FilterCutoff: return "Cutoff";
                case SfxParam.FilterRes: return "Resonance";
                case SfxParam.AmpDecay: return "Decay";
                case SfxParam.Bitcrush: return "Bitcrush";
                case SfxParam.Attack: return "Attack";
                case SfxParam.Sustain: return "Sustain (0 = one-shot)";
                case SfxParam.Release: return "Release";
                case SfxParam.FilterEnv: return "Filter Env (64 = none)";
                case SfxParam.FilterEnvDecay: return "Filter Env Time";
                case SfxParam.LfoRate: return "LFO Rate";
                case SfxParam.LfoPitch: return "LFO > Pitch";
                case SfxParam.LfoFilter: return "LFO > Filter";
                case SfxParam.LfoTimbre: return "LFO > Timbre";
                case SfxParam.Spread: return "Spread (wavetable)";
                case SfxParam.Chorus: return "Chorus";
                case SfxParam.Delay: return "Delay";
                case SfxParam.Reverb: return "Reverb";
                case SfxParam.Level: return "Level (100 = as designed)";
                default: return "Table (wavetable)";
            }
        }

        // ---- the 28 bytes ------------------------------------------------------------------------------------------
        public static byte Checksum(byte[] b, int offset)
        {
            int s = 0;
            for (int i = 0; i < Bytes - 1; i++) s += b[offset + i];
            return (byte)(s & 0x7F);
        }

        public void Pack(byte[] dst, int offset)
        {
            dst[offset] = Version;
            dst[offset + 1] = (byte)((int)engine % EngineCount);
            for (int i = 0; i < ParamCount; i++) dst[offset + 2 + i] = (byte)(this[(SfxParam)i] & 0x7F);
            dst[offset + Bytes - 1] = Checksum(dst, offset);
        }

        public byte[] Pack()
        {
            var b = new byte[Bytes];
            Pack(b, 0);
            return b;
        }

        /// <summary>False: not a valid patch (too short, wrong version, a byte over 127, checksum).</summary>
        public static bool TryUnpack(byte[] src, int offset, out SfxPatch patch)
        {
            patch = default;
            if (src == null || offset < 0 || src.Length < offset + Bytes) return false;
            if (src[offset] != Version || src[offset + 1] >= EngineCount) return false;
            for (int i = 0; i < Bytes; i++) if (src[offset + i] > 127) return false;
            if (src[offset + Bytes - 1] != Checksum(src, offset)) return false;
            patch.engine = (SfxEngine)src[offset + 1];
            for (int i = 0; i < ParamCount; i++) patch[(SfxParam)i] = src[offset + 2 + i];
            return true;
        }

        /// <summary>
        /// At least 28 bytes that begin with a version above this one's: a patch from a newer SFX FORGE, not a broken one
        /// (a later format may be longer, it will not be shorter). Fewer bytes than a patch has are a cut-off one, whatever
        /// they begin with. TryUnpack refuses it all the same – what its bytes mean is not known here.
        /// </summary>
        public static bool IsNewer(byte[] src, int offset)
            => src != null && offset >= 0 && offset <= src.Length - Bytes && src[offset] > Version && src[offset] <= 127;

        /// <summary>
        /// Every parameter moves by at most 15 % of its range (as MUTATE on the device). What kind of sound it is stays:
        /// the engine, the table, its level, and whether it is a one-shot or a sustained sound.
        /// </summary>
        public void Mutate(Random rng)
        {
            for (int i = 0; i < ParamCount; i++)
            {
                var p = (SfxParam)i;
                if (p == SfxParam.Level || p == SfxParam.Table || (p == SfxParam.Sustain && sustain == 0)) continue;
                int v = this[p] + rng.Next(-19, 20);
                int low = p == SfxParam.Sustain ? 1 : 0;
                this[p] = (byte)(v < low ? low : v > 127 ? 127 : v);
            }
        }

        /// <summary>
        /// As Mutate above, with the device's own generator, an amount and locks. step: the most a value moves either way,
        /// 0..127 (19 is the device's 15 %: StepOf(15)). locked: a bit for each SfxParam that stays (SfxMutate.Bit).
        /// One number is drawn for each of the 25 values in wire order, used or not - so locking a value changes no other.
        /// LEVEL, TABLE and a SUSTAIN of 0 stay; a SUSTAIN above 0 stays above 0.
        /// </summary>
        public void Mutate(ref SfxRandom rng, int step, uint locked = 0)
        {
            if (step < 0) step = 0; else if (step > 127) step = 127;
            for (int i = 0; i < ParamCount; i++)
            {
                var p = (SfxParam)i;
                int move = rng.Range(-step, step);
                if (p == SfxParam.Level || p == SfxParam.Table || (p == SfxParam.Sustain && sustain == 0) || (locked & (1u << i)) != 0) continue;
                int v = this[p] + move;
                int low = p == SfxParam.Sustain ? 1 : 0;
                this[p] = (byte)(v < low ? low : v > 127 ? 127 : v);
            }
        }

        /// <summary>An amount in percent (0..100) as the step Mutate takes: 0 is 0, 15 is 19 (the device's), 100 is 127; never 0 above 0.</summary>
        public static int StepOf(int percent)
        {
            if (percent <= 0) return 0;
            if (percent > 100) percent = 100;
            return Math.Max(1, (percent * 127 + 50) / 100);
        }

        public bool Equals(SfxPatch o)
        {
            if (engine != o.engine) return false;
            for (int i = 0; i < ParamCount; i++) if (this[(SfxParam)i] != o[(SfxParam)i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is SfxPatch o && Equals(o);

        public override int GetHashCode()
        {
            int h = (int)engine;
            for (int i = 0; i < ParamCount; i++) h = h * 31 + this[(SfxParam)i];
            return h;
        }
    }
}
