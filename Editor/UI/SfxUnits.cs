// SPDX-License-Identifier: MIT
using System;
using System.Globalization;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// What the device writes on its display: every value in its unit, every name, and the pages of four – ported from
    /// sfx-forge/sfx-forge.c (param_name, param_text, time_text, percent, note_text, mix_text, PAGE_PARAMS).
    /// </summary>
    public static class SfxUnits
    {
        public const int EngineKnob = -2, NoKnob = -1;

        public static readonly string[] EngineNames = { "IMPACT", "UI BLIP", "SCI-FI FM", "FOLEY", "WAVETABLE" };
        public static readonly string[] EngineShort = { "IMPACT", "BLIP", "FM", "FOLEY", "WAVE" };
        public static readonly string[] TableNames = { "SAW-SQ", "PULSE", "PEAK", "VOWEL", "ORGAN", "GLASS" };
        public static readonly string[] DelayTimes = { "1/16", "1/8", "3/16", "1/4", "3/8", "1/2" };

        /// <summary>The sound's pages, as the stage names them.</summary>
        public static readonly string[] PageNames = { "OSC", "TONE", "FILTER", "WAVE", "ENV", "LFO", "FX" };
        /// <summary>… and as the display titles them.</summary>
        public static readonly string[] PageTitles = { "SOUND   OSC", "SOUND   TONE", "SOUND   FILTER", "SOUND   WAVE", "ENVELOPE", "LFO", "EFFECT SENDS" };
        public const int PageOsc = 0, PageTone = 1, PageFilter = 2, PageWave = 3, PageEnv = 4, PageLfo = 5, PageFx = 6, PageCount = 7;

        /// <summary>What knobs 1-4 turn on each page: a SfxParam, EngineKnob or NoKnob. All 25 parameters are here once.</summary>
        public static readonly int[][] PageParams =
        {
            new[] { EngineKnob, (int)SfxParam.BaseFreq, (int)SfxParam.PitchEnvAmount, (int)SfxParam.PitchEnvDecay },
            new[] { (int)SfxParam.FmRatio, (int)SfxParam.FmDepth, (int)SfxParam.NoiseMix, (int)SfxParam.Bitcrush },
            new[] { (int)SfxParam.FilterCutoff, (int)SfxParam.FilterRes, (int)SfxParam.FilterEnv, (int)SfxParam.FilterEnvDecay },
            new[] { (int)SfxParam.Table, (int)SfxParam.Spread, NoKnob, NoKnob },
            new[] { (int)SfxParam.Attack, (int)SfxParam.AmpDecay, (int)SfxParam.Sustain, (int)SfxParam.Release },
            new[] { (int)SfxParam.LfoRate, (int)SfxParam.LfoPitch, (int)SfxParam.LfoFilter, (int)SfxParam.LfoTimbre },
            new[] { (int)SfxParam.Chorus, (int)SfxParam.Delay, (int)SfxParam.Reverb, (int)SfxParam.Level },
        };

        static readonly string[] Names =
        {
            "FREQ", "PITCH ENV", "ENV TIME", "FM RATIO", "FM DEPTH", "NOISE", "CUTOFF", "RESO", "DECAY", "CRUSH",
            "ATTACK", "SUSTAIN", "RELEASE", "FILTER ENV", "F.ENV TIME", "LFO RATE", "> PITCH", "> FILTER", "> TIMBRE", "SPREAD", "CHORUS", "DELAY", "REVERB", "LEVEL", "TABLE"
        };

        /// <summary>A parameter's name on the display (A and B change their job with the engine).</summary>
        public static string ParamName(SfxParam k, SfxEngine engine)
        {
            if (engine == SfxEngine.UiBlip && k == SfxParam.FmRatio) return "ARP STEP";
            if (engine == SfxEngine.UiBlip && k == SfxParam.FmDepth) return "ARP SPEED";
            if (engine == SfxEngine.Foley && k == SfxParam.FmRatio) return "GRAIN RATE";
            if (engine == SfxEngine.Foley && k == SfxParam.FmDepth) return "GRAIN AMT";
            if (engine == SfxEngine.Foley && k == SfxParam.FilterCutoff) return "BAND";
            if (engine == SfxEngine.Wavetable && k == SfxParam.FmRatio) return "POSITION";
            if (engine == SfxEngine.Wavetable && k == SfxParam.FmDepth) return "WARP";
            return Names[(int)k];
        }

        /// <summary>The values that stand in the middle at 64 and go both ways.</summary>
        public static bool IsCentred(SfxParam k) => k == SfxParam.PitchEnvAmount || k == SfxParam.FilterEnv;

        // 2^x as the device computes it (vd_exp2 in the SDK: a polynomial that reads up to 0.008 % low) – with the exact
        // power, two of the display's texts would differ by one (FREQ 76Hz, ATTACK 755ms). Every step is rounded to a
        // float, as there: C# may otherwise carry more digits between them.
        static float Exp2(float x)
        {
            if (x < -126f) return 0f;
            if (x > 126f) x = 126f;
            int i = (int)x;
            if (x < 0f && x != i) i--;
            float f = (float)(x - i);
            float p = (float)(f * 0.0013334f);
            p = (float)(0.0096181f + p); p = (float)(f * p);
            p = (float)(0.0555041f + p); p = (float)(f * p);
            p = (float)(0.2402265f + p); p = (float)(f * p);
            p = (float)(0.6931472f + p); p = (float)(f * p);
            p = (float)(1f + p);
            float whole = 1f;                                   // 2^i: exact
            for (int k = 0; k < i; k++) whole *= 2f;
            for (int k = 0; k > i; k--) whole *= 0.5f;
            return (float)(p * whole);
        }
        // a * 2^(u * octaves), each step a float
        static float Curve(float a, float u, float octaves) => (float)(a * Exp2((float)(u * octaves)));
        static string Fixed1(float v) { int iv = (int)(float)((float)(v * 10f) + 0.5f); return (iv / 10).ToString(CultureInfo.InvariantCulture) + "." + (iv % 10).ToString(CultureInfo.InvariantCulture); }
        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>"120ms", "1.4s".</summary>
        public static string TimeText(float seconds)
        {
            if (seconds < 0.9995f) return Int((int)(float)((float)(seconds * 1000f) + 0.5f)) + "ms";
            return Fixed1(seconds) + "s";
        }

        /// <summary>0..127 as "0%" .. "100%".</summary>
        public static string Percent(int v127) => Int((v127 * 100 + 63) / 127) + "%";

        /// <summary>A value (0..127) of a parameter as the display writes it: "85Hz", "3.6kHz", "75ms", "+32", "42%", "OFF", "SAW-SQ".</summary>
        public static string ParamText(SfxParam k, int v)
        {
            float u = (float)(v / 127f);
            switch (k)
            {
                case SfxParam.Table: return TableNames[v % TableNames.Length];
                case SfxParam.Sustain: case SfxParam.Bitcrush: if (v == 0) return "OFF"; break;
                case SfxParam.PitchEnvAmount: case SfxParam.FilterEnv: return v > 64 ? "+" + Int(v - 64) : Int(v - 64);
                case SfxParam.BaseFreq: case SfxParam.FilterCutoff:
                {
                    float hz = k == SfxParam.BaseFreq ? Curve(20f, u, 9.5f) : Curve(40f, u, 8.6f);
                    return hz >= 1000f ? Fixed1((float)(hz / 1000f)) + "kHz" : Int((int)hz) + "Hz";
                }
                case SfxParam.Attack: return TimeText(Curve(0.0015f, u, 11.4f));
                case SfxParam.AmpDecay: return TimeText(Curve(0.010f, u, 8.6f));
                case SfxParam.Release: return TimeText(Curve(0.005f, u, 9.6f));
                case SfxParam.PitchEnvDecay: case SfxParam.FilterEnvDecay: return TimeText(Curve(0.005f, u, 9.0f));
                case SfxParam.LfoRate: { float hz = Curve(0.1f, u, 8.2f); return (hz < 10f ? Fixed1(hz) : Int((int)hz)) + "Hz"; }
                case SfxParam.Level: case SfxParam.FmRatio: case SfxParam.FmDepth: return Int(v);
            }
            return Percent(v);
        }

        /// <summary>"A#3", or "--": the sound as designed.</summary>
        public static string NoteText(int note)
        {
            string[] n = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return note <= 0 ? "--" : n[note % 12] + Int(note / 12 - 1);
        }

        /// <summary>A pan (0..127, 64 = the middle): "L12", "C", "R30".</summary>
        public static string PanText(int v) => v == 64 ? "C" : v < 64 ? "L" + Int(64 - v) : "R" + Int(v - 64);

        /// <summary>The master delay's time (0..5): "1/16" .. "1/2".</summary>
        public static string DelayTimeText(int v) => DelayTimes[((v % DelayTimes.Length) + DelayTimes.Length) % DelayTimes.Length];

        // ---- automation: what a lane moves (auto_name, auto_text, auto_hi) ---------------------------------------------------
        static readonly string[] MasterNames = { "DELAY TIME", "FEEDBACK", "ROOM SIZE", "DAMPING", "DRIVE" };
        public static readonly string[] ScaleNames = { "CHROMATIC", "MAJOR", "MINOR", "PENTATONIC" };

        /// <summary>The knob a lane moves (SfxAutoTarget), as the display names it: "CUTOFF", "VOLUME", "PAN", "FEEDBACK" – "OFF" for none.</summary>
        public static string AutoName(int target, SfxEngine engine)
        {
            if (target <= SfxAutoTarget.None || target >= SfxAutoTarget.Count) return "OFF";
            if (target >= SfxAutoTarget.FirstMaster) return MasterNames[target - SfxAutoTarget.FirstMaster];
            return target == SfxAutoTarget.Pan ? "PAN" : target == SfxAutoTarget.Volume ? "VOLUME" : ParamName((SfxParam)(target - SfxAutoTarget.FirstParam), engine);
        }

        /// <summary>A lane's value as the display writes it: the knob's own unit.</summary>
        public static string AutoText(int target, int v)
        {
            if (target <= SfxAutoTarget.None || target >= SfxAutoTarget.Count) return "";
            if (target == SfxAutoTarget.FirstMaster + (int)SfxMaster.DelayTime) return DelayTimeText(v);
            if (target >= SfxAutoTarget.FirstMaster) return Percent(v);
            if (target == SfxAutoTarget.Volume) return Int(v);
            if (target == SfxAutoTarget.Pan) return PanText(v);
            return ParamText((SfxParam)(target - SfxAutoTarget.FirstParam), v);
        }

        /// <summary>The highest value a lane's knob takes.</summary>
        public static int AutoHi(int target)
            => target == SfxAutoTarget.FirstMaster + (int)SfxMaster.DelayTime ? DelayTimes.Length - 1 : target == SfxAutoTarget.FirstParam + (int)SfxParam.Table ? TableNames.Length - 1 : 126;

        /// <summary>The seconds of a hold: "1.50s".</summary>
        public static string HoldText(float seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";

        // ---- typing a value ------------------------------------------------------------------------------------------------
        /// <summary>
        /// What a text is worth for comparing: its number, in the smallest unit ("1.4s" = 1400, "3.6kHz" = 3600, "42%" = 42,
        /// "OFF" = 0). False: no number in it.
        /// </summary>
        public static bool Magnitude(string text, out double value) => Magnitude(text, out value, out _);

        /// <summary>As Magnitude, and what stands behind the number, in small letters: "ms", "s", "khz", "%", "" for none.</summary>
        public static bool Magnitude(string text, out double value, out string unit)
        {
            value = 0; unit = "";
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().ToLowerInvariant().Replace(',', '.');
            if (t == "off") return true;
            int n = 0;
            while (n < t.Length && (char.IsDigit(t[n]) || t[n] == '.' || ((t[n] == '-' || t[n] == '+') && n == 0))) n++;
            if (n == 0 || !double.TryParse(t.Substring(0, n), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            unit = t.Substring(n).Trim();
            if (unit.StartsWith("k") || unit == "s" || unit.StartsWith("sec")) value *= 1000.0;
            return true;
        }

        /// <summary>
        /// A typed length in seconds: "2", "2s", "2 sec", "1.5 seconds" are seconds, "200ms" a fifth of one. False: no number in it.
        /// </summary>
        public static bool TryParseSeconds(string text, out float seconds)
        {
            seconds = 0f;
            if (!Magnitude(text, out double m, out string unit)) return false;
            if (unit.StartsWith("m")) seconds = (float)(m / 1000.0);                                     // milliseconds
            else if (unit == "s" || unit.StartsWith("sec")) seconds = (float)(m / 1000.0);               // (Magnitude made milliseconds of them)
            else if (unit.Length == 0) seconds = (float)m;
            else return false;                                                                           // "2k", "2hz", "2%": not a time
            return true;
        }

        /// <summary>
        /// The value (low..high) whose text – as <paramref name="format"/> writes it – is what was typed, or comes nearest to
        /// it: "200" or "200hz" finds the cutoff that reads 198Hz, "1.2s" the decay of 1.2s. False: not understood.
        /// </summary>
        public static bool TryParse(string typed, int low, int high, Func<int, string> format, out int value)
        {
            value = low;
            if (string.IsNullOrWhiteSpace(typed) || format == null) return false;
            string t = typed.Trim();
            for (int v = low; v <= high; v++) if (string.Equals(format(v), t, StringComparison.OrdinalIgnoreCase)) { value = v; return true; }
            if (!Magnitude(t, out double want)) return false;
            double best = double.MaxValue;
            for (int v = low; v <= high; v++)
            {
                if (!Magnitude(format(v), out double m)) continue;
                double d = Math.Abs(m - want);
                if (d < best) { best = d; value = v; }
            }
            return best < double.MaxValue;
        }

        // ---- the tools: levels, a set's values, a page's values as bits ----------------------------------------------------
        /// <summary>A level in dB: "-3.2dB", "+1.3dB", "0.0dB" - "SILENT" at the floor (SfxLevels.Floor).</summary>
        public static string DbText(float db)
        {
            if (float.IsNaN(db) || db <= SfxLevels.Floor) return "SILENT";
            return DbNumber(db) + "dB";
        }

        /// <summary>The number of DbText alone: "-3.2", "+1.3", "0.0".</summary>
        public static string DbNumber(float db)
        {
            double tenths = Math.Round(db * 10.0, MidpointRounding.AwayFromZero);
            if (tenths == 0) return "0.0";
            return (tenths > 0 ? "+" : "") + (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>How far a play is tuned either way: "+-50ct", "OFF" at 0.</summary>
        public static string CentsText(float cents) => cents < 0.5f ? "OFF" : "±" + Int((int)(cents + 0.5f)) + "ct";

        /// <summary>A seed as its eight hex digits: "3F2A91C4".</summary>
        public static string SeedText(uint seed) => seed.ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>A typed seed: hex digits, with or without "0x". False: not one.</summary>
        public static bool TryParseSeed(string typed, out uint seed)
        {
            seed = 0;
            if (string.IsNullOrWhiteSpace(typed)) return false;
            string t = typed.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
            return t.Length > 0 && t.Length <= 8 && uint.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed);
        }

        /// <summary>The values of a page (PageOsc .. PageFx) as SfxParam bits: what a lock of the whole page holds.</summary>
        public static uint PageMask(int page)
        {
            uint mask = 0;
            if (page < 0 || page >= PageCount) return 0;
            foreach (int k in PageParams[page]) if (k >= 0) mask |= 1u << k;
            return mask;
        }

        /// <summary>A typed level: "-3", "-3.5dB", "+1 db", "0". False: no number in it, or another unit.</summary>
        public static bool TryParseDb(string typed, out float db)
        {
            db = 0f;
            if (!Magnitude(typed, out double m, out string unit)) return false;
            if (unit.Length > 0 && unit != "db") return false;
            if (typed.Trim().ToLowerInvariant() == "off") return false;
            db = (float)m;
            return true;
        }

        /// <summary>A typed text as a value of this parameter.</summary>
        public static bool TryParse(SfxParam k, string typed, out int value)
            => TryParse(typed, 0, k == SfxParam.Table ? TableNames.Length - 1 : 127, v => ParamText(k, v), out value);
    }
}
