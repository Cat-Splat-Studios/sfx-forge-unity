// SPDX-License-Identifier: GPL-3.0-or-later
//
// A patch as samples: the one way every tool renders a sound.
//   SfxBakeOptions  how to render: rate, mono, pitch, note, velocity, trim, normalise
//   SfxBaked        the samples, and what is known of them
//   SfxBake         Render, and two steps of it that can be used alone: TrimEnd and Resample
using System;
using System.Globalization;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// How a sound is rendered to samples. The defaults are what PLAY and BAKE WAV do: 48 kHz, stereo, as designed.
    /// </summary>
    [Serializable]
    public sealed class SfxBakeOptions
    {
        /// <summary>
        /// 8000..48000. The sound is always rendered at 48000, the device's own rate, and brought to this one
        /// afterwards (SfxBake.Resample): a lower rate is the same sound, as long, with nothing above half the rate.
        /// </summary>
        public int sampleRate = 48000;

        /// <summary>One channel: (left + right) / 2.</summary>
        public bool mono;

        /// <summary>Transposes the sound: -48..48.</summary>
        public float semitones;

        /// <summary>0: as designed. 1..127: played as that note.</summary>
        public int midiNote;

        /// <summary>0.01..1.</summary>
        public float velocity = 1f;

        /// <summary>
        /// 0: as baked. -90..-20: the end is cut after the last sample above this (dBFS, of the file as written).
        /// </summary>
        public float trimDb;

        /// <summary>NaN: off. -60..0: the peak the file gets (dBFS).</summary>
        public float normaliseDb = float.NaN;

        static string N(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// Null, or the first value that is out of range, in words: "sample rate 96000 is outside 8000..48000".
        /// </summary>
        public string Problem()
        {
            if (sampleRate < 8000 || sampleRate > 48000)
            {
                return "sample rate " + sampleRate.ToString(CultureInfo.InvariantCulture) + " is outside 8000..48000";
            }

            if (float.IsNaN(semitones) || semitones < -48f || semitones > 48f)
            {
                return "pitch " + N(semitones) + " is outside -48..48 semitones";
            }

            if (midiNote < 0 || midiNote > 127)
            {
                return "note " + midiNote.ToString(CultureInfo.InvariantCulture) + " is outside 0..127";
            }

            if (float.IsNaN(velocity) || velocity < 0.01f || velocity > 1f)
            {
                return "velocity " + N(velocity) + " is outside 0.01..1";
            }

            if (float.IsNaN(trimDb) || (trimDb != 0f && (trimDb < -90f || trimDb > -20f)))
            {
                return "trim " + N(trimDb) + " is neither 0 (off) nor inside -90..-20 dB";
            }

            if (!float.IsNaN(normaliseDb) && (normaliseDb < -60f || normaliseDb > 0f))
            {
                return "normalise " + N(normaliseDb) + " is outside -60..0 dB";
            }

            return null;
        }

        /// <summary>A copy of these options that can be changed on its own.</summary>
        public SfxBakeOptions Clone() => (SfxBakeOptions)MemberwiseClone();
    }

    /// <summary>A rendered sound, and what is known of it.</summary>
    public sealed class SfxBaked
    {
        /// <summary>Interleaved (L R L R ...) when channels is 2.</summary>
        public float[] samples;

        /// <summary>1 (mono) or 2 (stereo).</summary>
        public int channels;

        /// <summary>The rate of the samples, in Hz.</summary>
        public int sampleRate;

        /// <summary>How many sample frames the sound has (a frame: one sample of each channel).</summary>
        public int frames;

        /// <summary>The level of what is in samples.</summary>
        public SfxLevelReading level;

        /// <summary>What normalise applied, in dB (0: nothing).</summary>
        public float gainDb;

        /// <summary>The sound still rang at 12 s, the longest a bake renders: it is cut there.</summary>
        public bool cutAtLimit;

        /// <summary>How long the sound is: frames / sampleRate.</summary>
        public float Seconds => sampleRate > 0 ? frames / (float)sampleRate : 0f;
    }

    /// <summary>
    /// A patch as samples, with options: the one way every tool renders (the level line, a variation set's clips, the
    /// batch bake). The same patch and options give the same samples every time - a bake has its own player and seed.
    /// </summary>
    public static class SfxBake
    {
        /// <summary>The longest a bake renders (SfxNative.Bake).</summary>
        public const float MaxSeconds = 12f;

        /// <summary>
        /// The rate every sound is rendered at: the device's own. The engine's times are made for it.
        /// </summary>
        public const int EngineRate = 48000;

        /// <summary>
        /// Bakes (stereo, at 48 kHz), then: the sample rate if another is asked for, mono if asked, normalise,
        /// trim (so the trim's threshold is of the file as written), measure. A hold that is no number or below 0
        /// is 0, one above 12 s is 12 s. Null: the native library is missing, the options are out of range, or
        /// the engine gave no sound.
        /// </summary>
        public static SfxBaked Render(SfxPatch patch, float holdSeconds, SfxBakeOptions options = null)
        {
            if (options == null)
            {
                options = new SfxBakeOptions();
            }

            if (options.Problem() != null)
            {
                return null;
            }

            if (float.IsNaN(holdSeconds) || holdSeconds < 0f)
            {
                holdSeconds = 0f;
            }

            if (holdSeconds > MaxSeconds)
            {
                holdSeconds = MaxSeconds;
            }

            if (
                !SfxNative.Bake(
                    patch,
                    out float[] left,
                    out float[] right,
                    EngineRate,
                    options.velocity,
                    options.midiNote,
                    options.semitones,
                    holdSeconds,
                    MaxSeconds
                )
            )
            {
                return null;
            }

            if (left == null || right == null || left.Length == 0)
            {
                return null;
            }

            int frames = Math.Min(left.Length, right.Length);
            var baked = new SfxBaked
            {
                sampleRate = options.sampleRate,
                channels = options.mono ? 1 : 2,
                cutAtLimit = frames >= (int)(MaxSeconds * EngineRate),
            };
            if (options.sampleRate != EngineRate)
            {
                left = Resample(left, frames, EngineRate, options.sampleRate);
                right = Resample(right, frames, EngineRate, options.sampleRate);
                frames = left.Length;
                if (frames == 0)
                {
                    return null;
                }
            }

            if (options.mono)
            {
                for (int i = 0; i < frames; i++)
                {
                    left[i] = (left[i] + right[i]) * 0.5f;
                }

                right = null;
            }

            if (!float.IsNaN(options.normaliseDb))
            {
                float peak = SfxLevels.Measure(left, right, frames).peak;
                if (peak >= 0.00001f) // (a silent sound stays as it is)
                {
                    float gain = SfxLevels.FromDb(options.normaliseDb) / peak;
                    for (int i = 0; i < frames; i++)
                    {
                        left[i] *= gain;
                        if (right != null)
                        {
                            right[i] *= gain;
                        }
                    }

                    baked.gainDb = (float)(20.0 * Math.Log10(gain));
                }
            }

            if (options.trimDb != 0f)
            {
                frames = TrimEnd(left, right, frames, options.trimDb, 256 * options.sampleRate / 48000);
            }

            baked.frames = frames;
            baked.level = SfxLevels.Measure(left, right, frames);
            if (right == null)
            {
                if (left.Length != frames)
                {
                    Array.Resize(ref left, frames);
                }

                baked.samples = left;
            }
            else
            {
                var both = new float[frames * 2];
                for (int i = 0; i < frames; i++)
                {
                    both[i * 2] = left[i];
                    both[i * 2 + 1] = right[i];
                }

                baked.samples = both;
            }

            return baked;
        }

        /// <summary>
        /// Cuts the end after the last sample above thresholdDb (dBFS; right may be null) and returns the new number of
        /// frames: that sample, and fadeFrames after it which fade to nothing. Nothing above the threshold: only
        /// the fade is left.
        /// </summary>
        public static int TrimEnd(float[] left, float[] right, int frames, float thresholdDb, int fadeFrames)
        {
            if (left == null)
            {
                return 0;
            }

            // (never past what is there)
            if (frames > left.Length)
            {
                frames = left.Length;
            }

            if (right != null && frames > right.Length)
            {
                frames = right.Length;
            }

            if (frames <= 0)
            {
                return 0;
            }

            if (fadeFrames < 1)
            {
                fadeFrames = 1;
            }

            float gate = SfxLevels.FromDb(thresholdDb);
            int last = -1;
            for (int i = frames - 1; i >= 0; i--)
            {
                if (Math.Abs(left[i]) > gate || (right != null && Math.Abs(right[i]) > gate))
                {
                    last = i;
                    break;
                }
            }

            int keep = last + 1 + fadeFrames;

            // (what is cut is shorter than the fade: left as baked)
            if (keep >= frames)
            {
                return frames;
            }

            for (int i = 0; i < fadeFrames; i++)
            {
                float g = (fadeFrames - 1 - i) / (float)fadeFrames;
                left[last + 1 + i] *= g;
                if (right != null)
                {
                    right[last + 1 + i] *= g;
                }
            }

            return keep;
        }

        // the resampler's reach: zero crossings of its filter each way
        const int Zeros = 32;

        /// <summary>
        /// One channel at another sample rate: as long in seconds, the same level, and - going down - nothing left
        /// above half the new rate (a windowed sinc, 32 zero crossings each way). The same input gives the same output
        /// every time. frames: how many of samples are used. Equal rates: a copy.
        /// </summary>
        public static float[] Resample(float[] samples, int frames, int fromRate, int toRate)
        {
            if (samples == null || fromRate <= 0 || toRate <= 0)
            {
                return new float[0];
            }

            if (frames > samples.Length)
            {
                frames = samples.Length;
            }

            if (frames <= 0)
            {
                return new float[0];
            }

            if (fromRate == toRate)
            {
                var same = new float[frames];
                Array.Copy(samples, same, frames);
                return same;
            }

            double step = fromRate / (double)toRate; // input samples for one of the output

            // the filter's corner, as a part of the input's half rate
            double cut = Math.Min(1.0, toRate / (double)fromRate) * 0.92;
            int half = (int)Math.Ceiling(Zeros / cut);
            int count = (int)Math.Round(frames / step);
            var result = new float[count];
            for (int o = 0; o < count; o++)
            {
                double at = o * step;
                int centre = (int)Math.Floor(at);
                double sum = 0;
                double weights = 0;
                for (int k = centre - half + 1; k <= centre + half; k++)
                {
                    double d = at - k;
                    double u = d / half;
                    if (u <= -1.0 || u >= 1.0)
                    {
                        continue;
                    }

                    double x = Math.PI * d * cut;
                    double w =
                        (Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(x) / x)
                        * (0.42 + 0.5 * Math.Cos(Math.PI * u) + 0.08 * Math.Cos(2.0 * Math.PI * u));

                    // (beyond the ends there is silence: it counts, so the ends do not swell)
                    weights += w;
                    if (k >= 0 && k < frames)
                    {
                        sum += samples[k] * w;
                    }
                }

                result[o] = weights != 0 ? (float)(sum / weights) : 0f;
            }

            return result;
        }
    }
}
