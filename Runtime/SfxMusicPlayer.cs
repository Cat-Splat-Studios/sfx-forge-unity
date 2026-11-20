// SPDX-License-Identifier: MIT
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// Plays an SfxSong in the game with the device's own player – and lets the game shape it as it runs:
    /// Intensity brings the song's layers in and out, CuePattern changes to another pattern on the next bar line.
    /// The music is added to this object's AudioSource, which this component keeps running: leave its AudioClip empty.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Music Player")]
    [RequireComponent(typeof(AudioSource))]
    public class SfxMusicPlayer : MonoBehaviour
    {
        public SfxSong song;
        [Range(0f, 1f)] public float volume = 0.6f;
        public bool playOnEnable = true;
        [Tooltip("The pattern it starts on: 0 = A.")]
        [Range(0, 7)] public int startPattern;

        [Header("Live")]
        [Tooltip("How hot the game is. Every sound of the song has a threshold (LAYER on the device) and only plays at or above it.")]
        [Range(0f, 1f)] public float intensity = 1f;

        struct Command { public int kind, a, b, c, d; public float v; }   // 0 start, 1 stop, 2 cue, 3 note on, 4 note off, 5 track, 6 master
        readonly ConcurrentQueue<Command> commands = new ConcurrentQueue<Command>();
        readonly object gate = new object();
        readonly int[] state = new int[4];
        IntPtr memory;
        int sampleRate;
        float[] left = new float[2048], right = new float[2048];
        AudioSource source;
        SfxMods mods = SfxMods.None;
        volatile int step = -1, pattern, bar = -1;
        volatile bool running;
        static bool warned;

        /// <summary>The sequencer runs.</summary>
        public bool IsPlaying => running;
        /// <summary>The step it is on, 0..15 (-1: not yet).</summary>
        public int Step => step;
        /// <summary>The pattern it is on, 0 = A.</summary>
        public int Pattern => pattern;
        /// <summary>Bars since Play.</summary>
        public int Bar => bar;

        void OnEnable()
        {
            if (!SfxNative.Available)
            {
                if (!warned) { warned = true; Debug.LogWarning("SFX Forge: the native library (sfxforge) is not available on this platform – SfxMusicPlayer stays silent."); }
                return;
            }
            sampleRate = AudioSettings.outputSampleRate;
            source = GetComponent<AudioSource>();
            lock (gate)
            {
                memory = Marshal.AllocHGlobal(SfxNative.PlayerSize);
                SfxNative.PlayerInit(memory, sampleRate);
                Load();
            }
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            lock (gate)
            {
                if (memory != IntPtr.Zero) Marshal.FreeHGlobal(memory);
                memory = IntPtr.Zero; running = false; step = -1;
            }
            while (commands.TryDequeue(out _)) { }
        }

        void Load()                                                 // (inside the lock)
        {
            if (song != null && song.IsNewer)             // newer is not broken, and not silence without a word
                Debug.LogWarning($"SFX Forge: '{song.name}' holds a song in format {song.data[0]}, this version of SFX Forge knows up to {SfxProtocol.SongVersion}: it does not play here. Update the package to play it.", this);
            if (song == null || !song.IsValid) return;
            for (int i = 0; i < SfxProtocol.Slots; i++) SfxNative.PlayerSetPatch(memory, i, song.sounds[i].Pack());
            if (!SfxNative.PlayerSetSong(memory, song.data)) Debug.LogWarning($"SFX Forge: '{song.name}' is not a valid song.", this);
        }

        /// <summary>Takes the song asset again (after it changed) without stopping.</summary>
        public void Reload() { lock (gate) { if (memory != IntPtr.Zero) Load(); } }

        void Update()
        {
            // Unity only runs OnAudioFilterRead while the AudioSource plays (it does so without a clip, too)
            if (source != null && memory != IntPtr.Zero && !source.isPlaying) source.Play();
        }

        public void Play() { if (memory != IntPtr.Zero) commands.Enqueue(new Command { kind = 0, a = startPattern }); }
        public void Stop() { if (memory != IntPtr.Zero) commands.Enqueue(new Command { kind = 1 }); }

        /// <summary>Change to pattern 0..7 (A..H) on the next bar line – a new room, a boss, the menu.</summary>
        public void CuePattern(int index) { if (memory != IntPtr.Zero) commands.Enqueue(new Command { kind = 2, a = Mathf.Clamp(index, 0, SfxProtocol.Patterns - 1) }); }

        /// <summary>One of the song's 16 sounds by hand, beside the music: a sound effect (midiNote 0) or a note.</summary>
        public void PlaySound(int sound, float velocity = 1f, int midiNote = 0)
        {
            if (memory != IntPtr.Zero && sound >= 0 && sound < SfxProtocol.Slots) commands.Enqueue(new Command { kind = 3, a = sound, b = midiNote, v = Mathf.Clamp01(velocity) });
        }

        public void ReleaseSound(int sound, int midiNote = 0) { if (memory != IntPtr.Zero) commands.Enqueue(new Command { kind = 4, a = sound, b = midiNote }); }

        /// <summary>A sound's fader while the music plays: 1 = as designed, 0 = silent, up to 1.27. (Reload puts the song's own mix back.)</summary>
        public void SetTrackVolume(int sound, float volume) => Track(sound, Mathf.Clamp(Mathf.RoundToInt(volume * 100f), 0, 127), -1, -1);
        /// <summary>A sound's pan: -1 left .. 1 right.</summary>
        public void SetTrackPan(int sound, float pan) => Track(sound, -1, Mathf.Clamp(64 + Mathf.RoundToInt(pan * 63f), 0, 127), -1);
        public void SetTrackMute(int sound, bool mute) => Track(sound, -1, -1, mute ? 1 : 0);
        /// <summary>A master effect: delay time 0..5 (1, 2, 3, 4, 6, 8 sixteenths), the others 0..127.</summary>
        public void SetMaster(SfxMaster what, int value) { if (memory != IntPtr.Zero) commands.Enqueue(new Command { kind = 6, a = (int)what, b = value }); }
        void Track(int sound, int vol, int pan, int mute)
        {
            if (memory != IntPtr.Zero && sound >= 0 && sound < SfxProtocol.Slots) commands.Enqueue(new Command { kind = 5, a = sound, b = vol, c = pan, d = mute });
        }

        /// <summary>The same as the device does with a telemetry value 0..1 (Intensity, Pattern).</summary>
        public void ApplyTelemetry(SfxTelemetry channel, float value01)
        {
            if (channel == SfxTelemetry.Intensity) intensity = Mathf.Clamp01(value01);
            else if (channel == SfxTelemetry.Pattern)
            {
                int p = SfxProtocol.ToCc(value01) >> 4;
                if (p != pattern) CuePattern(p);
            }
        }

        static int Key(int sound, int midiNote) => midiNote > 0 ? 100 + midiNote % 96 : sound;   // (what names a note for its release)

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                if (memory == IntPtr.Zero) return;
                while (commands.TryDequeue(out var c))
                {
                    switch (c.kind)
                    {
                        case 0: SfxNative.PlayerStart(memory, c.a); break;
                        case 1: SfxNative.PlayerStop(memory); break;
                        case 2: SfxNative.PlayerCue(memory, c.a); break;
                        case 3: SfxNative.PlayerNoteOn(memory, c.a, Key(c.a, c.b), c.v, c.b); break;
                        case 4: SfxNative.PlayerNoteOff(memory, Key(c.a, c.b)); break;
                        case 5: SfxNative.PlayerSetTrack(memory, c.a, c.b, c.c, c.d); break;
                        case 6: SfxNative.PlayerSetMaster(memory, (SfxMaster)c.a, c.b); break;
                    }
                }
                int frames = data.Length / channels;
                if (left.Length < frames) { left = new float[frames]; right = new float[frames]; }
                Array.Clear(left, 0, frames); Array.Clear(right, 0, frames);
                SfxNative.PlayerSetLive(memory, ref mods, intensity);
                uint sounding = SfxNative.PlayerRender(memory, left, right, frames);
                SfxNative.PlayerState(memory, state);
                running = state[0] != 0; step = state[1]; pattern = state[2]; bar = state[3];
                if (sounding == 0) return;
                SfxMix.Add(data, channels, left, right, frames, volume);
            }
        }
    }
}
