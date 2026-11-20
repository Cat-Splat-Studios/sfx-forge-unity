// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// A piece of music made on SFX FORGE: its 16 sounds and its song (8 patterns, a chain, a layer threshold per sound).
    /// Captured from the device in Window > SFX Forge; played in the game by SfxMusicPlayer.
    /// </summary>
    [CreateAssetMenu(menuName = "SFX Forge/Song", fileName = "New Song", order = 221)]
    public class SfxSong : ScriptableObject
    {
        [Tooltip("The 16 sounds the song plays (pads 1-16 of the device).")]
        public SfxPatch[] sounds = new SfxPatch[SfxProtocol.Slots];

        [Tooltip("The song as the device packs it (core/sfx_song.h).")]
        [HideInInspector] public byte[] data = new byte[0];

        public bool IsValid => data != null && sounds != null && sounds.Length == SfxProtocol.Slots
            && ((data.Length == SfxProtocol.SongBytes && data[0] == 5) || (data.Length == SfxProtocol.SongBytesV4 && data[0] == 4) || (data.Length == SfxProtocol.SongBytesV3 && data[0] == 3)
                || (data.Length == SfxProtocol.SongBytesV2 && data[0] == 2) || (data.Length == SfxProtocol.SongBytesV1 && data[0] == 1));
        /// <summary>Bars in its arrangement (0: none, it walks its chain).</summary>
        public int ArrangementBars => IsValid && data[0] >= 4 ? data[Arr] : 0;
        int Arr => data[0] == 4 ? 66 + SfxProtocol.Slots : 66;                 // (format 4 kept one lane target for each sound before it)
        /// <summary>The pattern (0..7) of a bar of the arrangement.</summary>
        public int ArrangementPattern(int bar) => bar >= 0 && bar < ArrangementBars ? data[Arr + 1 + bar] : -1;
        /// <summary>How many automation lanes move a knob of this sound (a song holds 24 in all).</summary>
        public int AutomationLanes(int sound)
        {
            if (!IsValid || sound < 0 || sound >= SfxProtocol.Slots) return 0;
            if (data[0] == 4) return data[66 + sound] != 0 ? 1 : 0;
            int n = 0;
            if (data[0] >= 5) for (int l = 0; l < 24; l++) if (data[99 + l * 2] == sound + 1 && data[100 + l * 2] != 0) n++;
            return n;
        }
        /// <summary>It carries a mix (faders, pans, mutes, master effects): a song from firmware 0.6 on.</summary>
        public bool HasMix => IsValid && data[0] >= 2;
        /// <summary>A sound's fader: 1 = as designed, up to 1.27.</summary>
        public float Volume(int sound) => HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[7 + SfxProtocol.Slots + sound] / 100f : 1f;
        /// <summary>A sound's pan: -1 left .. 0 the middle .. 1 right.</summary>
        public float Pan(int sound) => HasMix && sound >= 0 && sound < SfxProtocol.Slots ? Mathf.Clamp((data[7 + 2 * SfxProtocol.Slots + sound] - 64) / 63f, -1f, 1f) : 0f;
        public int StepsPerPattern => IsValid ? data[1] : 0;
        public int Bpm => IsValid ? data[2] << 7 | data[3] : 0;
        /// <summary>How many patterns it walks through by itself (1 = it stays on one).</summary>
        public int Chain => IsValid ? data[6] : 0;
        /// <summary>From which intensity (0..1) a sound plays: 0 = always.</summary>
        public float Layer(int sound) => IsValid && sound >= 0 && sound < SfxProtocol.Slots ? data[7 + sound] / 127f : 0f;

        // ---- reading the packed song: what an Inspector shows. Nothing here changes it. -------------------------------------
        /// <summary>
        /// The most steps a pattern can have (this song's own length is StepsPerPattern), the automation lanes of a song,
        /// the most bars of an arrangement.
        /// </summary>
        public const int MaxSteps = 16, Lanes = 24, MaxBars = 32;

        /// <summary>How far the off-beat steps wait: 0..100.</summary>
        public int Swing => IsValid ? data[4] : 0;
        /// <summary>The scale notes were entered in on the device: 0 chromatic, 1 major, 2 minor, 3 pentatonic.</summary>
        public int Scale => IsValid ? data[5] : 0;
        /// <summary>A sound's layer mark as the song keeps it: 0..127 (0 = always).</summary>
        public int LayerRaw(int sound) => IsValid && sound >= 0 && sound < SfxProtocol.Slots ? data[7 + sound] : 0;
        /// <summary>A sound's fader as the song keeps it: 0..127, 100 = as designed.</summary>
        public int VolumeRaw(int sound) => HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[7 + SfxProtocol.Slots + sound] : 100;
        /// <summary>A sound's pan as the song keeps it: 0 left .. 64 the middle .. 127 right.</summary>
        public int PanRaw(int sound) => HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[7 + 2 * SfxProtocol.Slots + sound] : 64;
        /// <summary>The sound is muted in the song's mix.</summary>
        public bool Muted(int sound)
        {
            if (!HasMix || sound < 0 || sound >= SfxProtocol.Slots) return false;
            int at = 7 + 3 * SfxProtocol.Slots;
            return ((data[at] | data[at + 1] << 7 | (data[at + 2] & 3) << 14) >> sound & 1) != 0;
        }
        /// <summary>A master effect as the song keeps it: delay time 0..5 (1, 2, 3, 4, 6, 8 sixteenths), the others 0..127.</summary>
        public int Master(SfxMaster what)
        {
            int i = (int)what;
            if (i < 0 || i > (int)SfxMaster.Drive) return 0;
            return HasMix ? data[7 + 3 * SfxProtocol.Slots + 3 + i] : MasterPlain[i];
        }
        static readonly byte[] MasterPlain = { 2, 64, 91, 40, 0 };            // (a song from before the mixer plays with these)

        // where a pattern's sound begins in the packed song, and how long a sound is there: 3 bytes of steps, 16 notes,
        // then (format 3 on) 16 accents, then (format 4 only) its one lane
        int Body => data[0] >= 5 ? Arr + 1 + MaxBars + Lanes * 2 : data[0] == 4 ? Arr + 1 + MaxBars : data[0] >= 2 ? 66 : 7 + SfxProtocol.Slots;
        int Block => data[0] == 4 ? 3 + 3 * MaxSteps : data[0] == 3 || data[0] >= 5 ? 3 + 2 * MaxSteps : 3 + MaxSteps;
        int At(int pattern, int sound) => Body + (pattern * SfxProtocol.Slots + sound) * Block;
        bool Has(int pattern, int sound, int step) => IsValid && pattern >= 0 && pattern < SfxProtocol.Patterns && sound >= 0 && sound < SfxProtocol.Slots && step >= 0 && step < MaxSteps;

        /// <summary>The sound plays on this step (0..15) of this pattern (0..7).</summary>
        public bool StepOn(int pattern, int sound, int step)
        {
            if (!Has(pattern, sound, step)) return false;
            int at = At(pattern, sound);
            return ((data[at] | data[at + 1] << 7 | (data[at + 2] & 3) << 14) >> step & 1) != 0;
        }
        /// <summary>The note of a step: 0 = the sound as designed (drums, effects), else a MIDI note.</summary>
        public int StepNote(int pattern, int sound, int step) => Has(pattern, sound, step) ? data[At(pattern, sound) + 3 + step] : 0;
        /// <summary>How hard a step is played: 0..7, soft .. full (5 = as it always was).</summary>
        public int StepVelocity(int pattern, int sound, int step) => Has(pattern, sound, step) && data[0] >= 3 ? data[At(pattern, sound) + 3 + MaxSteps + step] & 7 : 5;
        /// <summary>How many steps a step holds: 0 = by itself (to the sound's end, its next note or the bar line).</summary>
        public int StepLength(int pattern, int sound, int step) => Has(pattern, sound, step) && data[0] >= 3 ? data[At(pattern, sound) + 3 + MaxSteps + step] >> 3 & 15 : 0;

        /// <summary>The sound (0..15) a lane (0..23) moves a knob of, -1: the lane is free.</summary>
        public int LaneSound(int lane)
        {
            if (!IsValid || lane < 0 || lane >= Lanes) return -1;
            if (data[0] == 4) return lane < SfxProtocol.Slots && data[66 + lane] != 0 ? lane : -1;
            if (data[0] < 5 || data[Arr + 2 + MaxBars + lane * 2] == 0) return -1;
            return data[Arr + 1 + MaxBars + lane * 2] - 1;                   // (a lane without a sound is free, whatever it names)
        }
        /// <summary>
        /// Which knob a lane moves (SfxAutoTarget): 0 = none, 1..25 the sound's own values (SfxParam + 1), then its fader, its pan, the master's five.
        /// A free lane (LaneSound -1) moves nothing: 0.
        /// </summary>
        public int LaneTarget(int lane)
        {
            if (!IsValid || lane < 0 || lane >= Lanes) return 0;
            if (data[0] == 4) return lane < SfxProtocol.Slots ? data[66 + lane] : 0;
            if (data[0] < 5 || data[Arr + 1 + MaxBars + lane * 2] == 0) return 0;      // (a lane without a sound is free, whatever it names)
            return data[Arr + 2 + MaxBars + lane * 2];
        }
        /// <summary>A lane's point on a step of a pattern: -1 = none there (the point before holds), else the value 0..126.</summary>
        public int LanePoint(int pattern, int lane, int step)
        {
            if (!IsValid || pattern < 0 || pattern >= SfxProtocol.Patterns || lane < 0 || lane >= Lanes || step < 0 || step >= MaxSteps) return -1;
            if (data[0] == 4) return lane < SfxProtocol.Slots ? data[At(pattern, lane) + 3 + 2 * MaxSteps + step] - 1 : -1;
            if (data[0] < 5) return -1;
            return data[Body + SfxProtocol.Patterns * SfxProtocol.Slots * Block + (pattern * Lanes + lane) * MaxSteps + step] - 1;
        }

        public void Set(SfxPatch[] newSounds, byte[] packedSong)
        {
            sounds = (SfxPatch[])newSounds.Clone();
            data = (byte[])packedSong.Clone();
        }
    }
}
