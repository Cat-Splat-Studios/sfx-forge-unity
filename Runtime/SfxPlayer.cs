// SPDX-License-Identifier: GPL-3.0-or-later
//
// SfxPlayer: the component that plays a preset live, with the device's engine. SfxMix (at the end) is the small
// helper it shares with SfxMusicPlayer.
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// Plays a preset in the game with the device's own engine – synthesized live, with its chorus, delay and reverb,
    /// so the four "Live" values can move the sound while it plays (an engine hum following speed), exactly as
    /// telemetry does on the device. No device needed.
    /// The sound is added to this object's AudioSource, which this component keeps running: leave its AudioClip empty.
    /// Not spatialized: for 3D one-shots bake a clip instead.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Player")]
    [RequireComponent(typeof(AudioSource))]
    public class SfxPlayer : MonoBehaviour
    {
        /// <summary>The sound it plays.</summary>
        public SfxPreset preset;

        /// <summary>The sound's volume, 0..1.</summary>
        [Range(0f, 1f)]
        public float volume = 0.8f;

        [Tooltip("Start the sound when this component is enabled (for a sustained sound: an engine, wind).")]
        public bool playOnEnable;

        /// <summary>Live: semitones added to the pitch, 0..24.</summary>
        [Header("Live (what telemetry moves on the device)")]
        [Range(0f, 24f)]
        public float pitchSemitones;

        /// <summary>Live: octaves added to the filter, -4..4.</summary>
        [Range(-4f, 4f)]
        public float cutoffOctaves;

        [Tooltip("Timbre: FM depth, or a wavetable's position.")]
        [Range(0f, 1f)]
        public float fm;

        /// <summary>Live: the level, 0..1.</summary>
        [Range(0f, 1f)]
        public float level = 1f;

        const int Keys = 16; // up to 16 notes of it at once

        // What the game thread asks of the audio thread: a Command's kind.
        const int CmdPlay = 0; // a note of the sound: patch, velocity, note, semitones
        const int CmdRelease = 1; // every note is let go
        const int CmdValues = 2; // new values for what sounds now: patch

        struct Command
        {
            public byte[] patch;
            public float velocity;
            public float semitones;
            public int kind;
            public int note;
        }

        readonly ConcurrentQueue<Command> commands = new ConcurrentQueue<Command>();
        readonly object gate = new object();
        IntPtr memory;
        int sampleRate;
        int nextKey;
        float[] left = new float[2048];
        float[] right = new float[2048];
        SfxMods current = SfxMods.None;
        SfxPatch lastPatch;
        AudioSource source;
        volatile bool sounding;
        static bool warned;

        /// <summary>It sounds (its echoes included).</summary>
        public bool IsPlaying => sounding;

        void OnEnable()
        {
            if (!SfxNative.Available)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(
                        "SFX Forge: the native library (sfxforge) is not available on this platform – SfxPlayer stays "
                            + "silent. Only clips and WAV files baked beforehand, in the editor on Windows x64, "
                            + "play here: baking needs the library too."
                    );
                }

                return;
            }

            sampleRate = AudioSettings.outputSampleRate;
            lock (gate)
            {
                memory = Marshal.AllocHGlobal(SfxNative.PlayerSize);
                SfxNative.PlayerInit(memory, sampleRate);
            }

            source = GetComponent<AudioSource>();
            if (preset != null)
            {
                lastPatch = preset.patch;
            }

            if (playOnEnable)
            {
                Play();
            }
        }

        void OnDisable()
        {
            lock (gate)
            {
                if (memory != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(memory);
                }

                memory = IntPtr.Zero;
                sounding = false;
            }

            while (commands.TryDequeue(out _)) { }
        }

        void Update()
        {
            // Unity only runs OnAudioFilterRead while the AudioSource plays (it does so without a clip, too)
            if (source != null && memory != IntPtr.Zero && !source.isPlaying)
            {
                source.Play();
            }

            if (preset != null && sounding && !preset.patch.Equals(lastPatch)) // edited while it sounds: hear it
            {
                lastPatch = preset.patch;
                commands.Enqueue(new Command { kind = CmdValues, patch = lastPatch.Pack() });
            }
        }

        /// <summary>
        /// Plays the preset. velocity 0..1; semitones transposes it; midiNote above 0 plays it as that note.
        /// </summary>
        public void Play(float velocity = 1f, float semitones = 0f, int midiNote = 0)
        {
            if (preset == null)
            {
                return;
            }

            lastPatch = preset.patch;
            Play(preset.patch, velocity, semitones, midiNote);
        }

        /// <summary>
        /// Plays this patch instead of the preset: velocity 0..1, semitones up or down, midiNote 0 = as designed.
        /// </summary>
        public void Play(SfxPatch patch, float velocity = 1f, float semitones = 0f, int midiNote = 0)
        {
            if (memory == IntPtr.Zero)
            {
                return;
            }

            commands.Enqueue(
                new Command
                {
                    kind = CmdPlay,
                    patch = patch.Pack(),
                    velocity = Mathf.Clamp01(velocity),
                    semitones = semitones,
                    note = midiNote,
                }
            );
        }

        /// <summary>Lets go of sustained sounds (they fade); one-shots just finish.</summary>
        public void Release()
        {
            if (memory != IntPtr.Zero)
            {
                commands.Enqueue(new Command { kind = CmdRelease });
            }
        }

        /// <summary>The same as the device does with a telemetry value 0..1.</summary>
        public void ApplyTelemetry(SfxTelemetry channel, float value01)
        {
            var m = new SfxMods
            {
                pitchSemitones = pitchSemitones,
                cutoffOctaves = cutoffOctaves,
                fm = fm,
                level = level,
            };
            SfxProtocol.Apply(ref m, channel, SfxProtocol.ToCc(value01));
            pitchSemitones = m.pitchSemitones;
            cutoffOctaves = m.cutoffOctaves;
            fm = m.fm;
            level = m.level;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                if (memory == IntPtr.Zero)
                {
                    return;
                }

                while (commands.TryDequeue(out var c))
                {
                    if (c.kind == CmdRelease)
                    {
                        for (int k = 0; k < Keys; k++)
                        {
                            SfxNative.PlayerNoteOff(memory, k);
                        }

                        continue;
                    }

                    SfxNative.PlayerSetPatch(memory, 0, c.patch); // (also reshapes what sounds now)
                    if (c.kind == CmdPlay)
                    {
                        SfxNative.PlayerNoteOn(memory, 0, nextKey, c.velocity, c.note, c.semitones);
                        nextKey = (nextKey + 1) % Keys;
                    }
                }

                int frames = data.Length / channels;
                if (left.Length < frames)
                {
                    left = new float[frames];
                    right = new float[frames];
                }

                Array.Clear(left, 0, frames);
                Array.Clear(right, 0, frames);
                float glide = Mathf.Exp(-frames / (0.03f * sampleRate)); // 30 ms, as on the device
                current.pitchSemitones = pitchSemitones + (current.pitchSemitones - pitchSemitones) * glide;
                current.cutoffOctaves = cutoffOctaves + (current.cutoffOctaves - cutoffOctaves) * glide;
                current.fm = fm + (current.fm - fm) * glide;
                current.level = level + (current.level - level) * glide;
                SfxNative.PlayerSetLive(memory, ref current, 1f);

                sounding = SfxNative.PlayerRender(memory, left, right, frames) != 0;
                if (!sounding)
                {
                    return;
                }

                SfxMix.Add(data, channels, left, right, frames, volume);
            }
        }
    }

    static class SfxMix
    {
        // the device's soft clip
        static float Clip(float x) =>
            x > 3f ? 1f
            : x < -3f ? -1f
            : x * (27f + x * x) / (27f + 9f * x * x);

        /// <summary>Adds a stereo block to Unity's interleaved buffer, at the device's output level.</summary>
        public static void Add(float[] data, int channels, float[] left, float[] right, int frames, float volume)
        {
            float gain = volume * 1.25f;
            for (int f = 0; f < frames; f++)
            {
                float l = Clip(left[f] * gain);
                float r = Clip(right[f] * gain);
                if (channels == 1)
                {
                    data[f] += (l + r) * 0.5f;
                }
                else
                {
                    data[f * channels] += l;
                    data[f * channels + 1] += r;
                }
            }
        }
    }
}
