// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// The device's own engine as a native library (native/sfx_native.c): a patch or a song sounds here as it does there.
    /// Windows x64 for now.
    /// </summary>
    public static class SfxNative
    {
        const string Lib = "sfxforge";
        const int Abi = 4;

        [DllImport(Lib)] static extern int sfxn_abi();
        [DllImport(Lib)] static extern int sfxn_voice_size();
        [DllImport(Lib)] static extern int sfxn_trigger(IntPtr voice, byte[] patch, float vel, float noteSt, float sr, uint seed);
        [DllImport(Lib)] static extern int sfxn_update(IntPtr voice, byte[] patch);
        [DllImport(Lib)] static extern void sfxn_release(IntPtr voice);
        [DllImport(Lib)] static extern int sfxn_active(IntPtr voice);
        [DllImport(Lib)] static extern int sfxn_render(IntPtr voice, ref SfxMods mods, [In, Out] float[] buffer, int n);

        [DllImport(Lib)] static extern int sfxn_player_size();
        [DllImport(Lib)] static extern void sfxn_player_init(IntPtr player, float sr);
        [DllImport(Lib)] static extern int sfxn_player_set_patch(IntPtr player, int slot, byte[] patch);
        [DllImport(Lib)] static extern int sfxn_player_set_song(IntPtr player, byte[] song, int n);
        [DllImport(Lib)] static extern void sfxn_player_start(IntPtr player, int pattern);
        [DllImport(Lib)] static extern void sfxn_player_stop(IntPtr player);
        [DllImport(Lib)] static extern void sfxn_player_cue(IntPtr player, int pattern);
        [DllImport(Lib)] static extern void sfxn_player_set_loop(IntPtr player, int on);
        [DllImport(Lib)] static extern void sfxn_player_set_live(IntPtr player, ref SfxMods mods, float intensity);
        [DllImport(Lib)] static extern void sfxn_player_set_track(IntPtr player, int slot, int vol, int pan, int mute);
        [DllImport(Lib)] static extern void sfxn_player_set_master(IntPtr player, int index, int value);
        [DllImport(Lib)] static extern void sfxn_player_note_on(IntPtr player, int slot, int key, float vel, int midiNote, float noteSt);
        [DllImport(Lib)] static extern void sfxn_player_note_off(IntPtr player, int key);
        [DllImport(Lib)] static extern uint sfxn_player_render(IntPtr player, [In, Out] float[] left, [In, Out] float[] right, int n);
        [DllImport(Lib)] static extern void sfxn_player_state(IntPtr player, [Out] int[] out4);
        [DllImport(Lib)] static extern int sfxn_bake(IntPtr player, byte[] patch, float sr, float vel, int midiNote, float noteSt, float holdSeconds, [Out] float[] left, [Out] float[] right, int max);

        static int available = -1;
        static volatile bool noLoop;                    // a library from before the loop flag: it plays on, in the song's order

        /// <summary>False where the library is missing (another platform): nothing plays locally, the link still works.</summary>
        public static bool Available
        {
            get
            {
                if (available < 0)
                {
                    try { available = sfxn_abi() == Abi ? 1 : 0; }
                    catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException) { available = 0; }
                }
                return available == 1;
            }
        }

        // ---- one voice, dry and mono: the smallest way to play a patch (no effects) ------------------------------------
        public static int VoiceSize => sfxn_voice_size();

        public static bool Trigger(IntPtr voice, byte[] patch, float velocity, float semitones, float sampleRate, uint seed)
            => sfxn_trigger(voice, patch, velocity, semitones, sampleRate, seed) == 0;

        public static void Update(IntPtr voice, byte[] patch) => sfxn_update(voice, patch);
        public static void Release(IntPtr voice) => sfxn_release(voice);
        public static bool Active(IntPtr voice) => sfxn_active(voice) != 0;

        /// <summary>Adds n mono samples to buffer. True while the voice sounds.</summary>
        public static bool Render(IntPtr voice, ref SfxMods mods, float[] buffer, int n) => sfxn_render(voice, ref mods, buffer, n) != 0;

        // ---- the player: 16 sounds, 16 voices, chorus / delay / reverb, a song and its sequencer. Stereo. ----------------
        /// <summary>Bytes to allocate for a player (about 0.6 MB: its effects' memory is inside).</summary>
        public static int PlayerSize => sfxn_player_size();
        public static void PlayerInit(IntPtr player, float sampleRate) => sfxn_player_init(player, sampleRate);
        public static bool PlayerSetPatch(IntPtr player, int slot, byte[] patch) => sfxn_player_set_patch(player, slot, patch) == 0;
        /// <summary>The packed song (what the device sends). False: not a song, the player keeps what it had.</summary>
        public static bool PlayerSetSong(IntPtr player, byte[] song) => song != null && sfxn_player_set_song(player, song, song.Length) == 0;
        /// <summary>pattern -1: the one it is on.</summary>
        public static void PlayerStart(IntPtr player, int pattern) => sfxn_player_start(player, pattern);
        public static void PlayerStop(IntPtr player) => sfxn_player_stop(player);
        /// <summary>Change to this pattern on the next bar line (at once while stopped).</summary>
        public static void PlayerCue(IntPtr player, int pattern) => sfxn_player_cue(player, pattern);
        /// <summary>
        /// on: the pattern that plays repeats until PlayerCue chooses another. Off: the song's arrangement or chain, on from
        /// its first bar with that pattern. PlayerInit switches it off; PlayerStart and PlayerStop leave it. False: the
        /// library is an older one without it - nothing changed, the song plays in its order.
        /// </summary>
        public static bool PlayerSetLoop(IntPtr player, bool on)
        {
            if (noLoop) return false;
            try { sfxn_player_set_loop(player, on ? 1 : 0); return true; }
            catch (EntryPointNotFoundException) { noLoop = true; return false; }
        }
        public static void PlayerSetLive(IntPtr player, ref SfxMods mods, float intensity) => sfxn_player_set_live(player, ref mods, intensity);
        /// <summary>A sound's place in the mix: volume 0..127 (100 = as designed), pan 0..127 (64 = the middle), mute 0 / 1. -1 leaves a value.</summary>
        public static void PlayerSetTrack(IntPtr player, int slot, int volume, int pan, int mute) => sfxn_player_set_track(player, slot, volume, pan, mute);
        /// <summary>The master effects: see SfxMaster. Delay time 0..5 (1, 2, 3, 4, 6, 8 sixteenths), the others 0..127.</summary>
        public static void PlayerSetMaster(IntPtr player, SfxMaster what, int value) => sfxn_player_set_master(player, (int)what, value);
        /// <summary>
        /// A sound by hand, beside the song. key (0..199) names this note for PlayerNoteOff. midiNote 0: as designed
        /// (a sound effect), else at that pitch; semitones transposes either.
        /// </summary>
        public static void PlayerNoteOn(IntPtr player, int slot, int key, float velocity, int midiNote = 0, float semitones = 0f) => sfxn_player_note_on(player, slot, key, velocity, midiNote, semitones);
        public static void PlayerNoteOff(IntPtr player, int key) => sfxn_player_note_off(player, key);
        /// <summary>Adds n stereo samples. Returns the sounds that play, a bit each; bit 31 while only effects ring; 0 = silence.</summary>
        public static uint PlayerRender(IntPtr player, float[] left, float[] right, int n) => sfxn_player_render(player, left, right, n);
        /// <summary>out4: running, step (-1 = none yet), pattern, bar.</summary>
        public static void PlayerState(IntPtr player, int[] out4) => sfxn_player_state(player, out4);

        /// <summary>
        /// The whole sound with its effects, as stereo samples: left and right, the same length. A sustained patch is held
        /// for holdSeconds, then let go. midiNote 0: as designed. False: the library is missing or the patch is not valid.
        /// </summary>
        public static bool Bake(SfxPatch patch, out float[] left, out float[] right, int sampleRate = 48000, float velocity = 1f, int midiNote = 0, float semitones = 0f, float holdSeconds = 1.5f, float maxSeconds = 12f)
        {
            left = right = null;
            if (!Available) return false;
            int max = (int)(maxSeconds * sampleRate);
            var l = new float[max]; var r = new float[max];
            IntPtr scratch = Marshal.AllocHGlobal(sfxn_player_size());
            int n;
            try { n = sfxn_bake(scratch, patch.Pack(), sampleRate, velocity, midiNote, semitones, holdSeconds, l, r, max); }
            finally { Marshal.FreeHGlobal(scratch); }
            if (n < 0) return false;
            left = new float[n]; right = new float[n];
            Array.Copy(l, left, n); Array.Copy(r, right, n);
            return true;
        }

        /// <summary>As Bake, with left and right interleaved (L R L R …) – what an AudioClip takes.</summary>
        public static float[] BakeInterleaved(SfxPatch patch, int sampleRate = 48000, float velocity = 1f, int midiNote = 0, float semitones = 0f, float holdSeconds = 1.5f)
        {
            if (!Bake(patch, out var l, out var r, sampleRate, velocity, midiNote, semitones, holdSeconds)) return null;
            var both = new float[l.Length * 2];
            for (int i = 0; i < l.Length; i++) { both[i * 2] = l[i]; both[i * 2 + 1] = r[i]; }
            return both;
        }
    }
}
