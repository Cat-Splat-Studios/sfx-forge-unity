// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// A piece of music made on SFX FORGE: its 16 sounds and its song (8 patterns, a chain, a layer threshold per
    /// sound). Captured from the device in Window > SFX Forge; played in the game by SfxMusicPlayer.
    /// </summary>
    [CreateAssetMenu(menuName = "SFX Forge/Song", fileName = "New Song", order = 221)]
    public class SfxSong : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// The highest asset format this version of SFX Forge writes for a song: a number of the song asset alone
        /// (sounds and variation sets count theirs in SfxAssetFormat.Current). 1: a packed song up to format 5.
        /// 2: a packed song of format 6 (firmware 1.6.0: a grid of 16, 32 or 64 steps for each pattern). The number
        /// follows the packed song, so an asset that holds an older song still says 1.
        /// </summary>
        public const int CurrentFormat = 2;

        // Which asset format wrote this file. A file without the line (every asset from
        // before the number was kept) is format 1: the initialiser has to stay the literal 1.
        [SerializeField, HideInInspector]
        int format = 1;

        // Which asset format saved this file last: always this version's number when this version writes the file.
        // A newer SFX Forge that finds its own "format" with a lower number here knows the file went through an older
        // version, which wrote back only what it knew. (Nothing reads it in this version.)
        [SerializeField, HideInInspector]
        int savedBy = CurrentFormat;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (format < Need)
            {
                format = Need;
            }

            savedBy = CurrentFormat;
        }

        // What the packed song asks of the asset: an older package cannot know a format-6 song by its bytes (it may be
        // shorter than any song it knows), so the asset's own number says it - and that package warns, and
        // shows its banner.
        int Need => data != null && data.Length > 0 && data[0] >= 6 ? 2 : 1;

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }

        /// <summary>The asset format the file was written in: 1, or 2 with a packed song of format 6.</summary>
        public int Format => format;

        /// <summary>
        /// A newer SFX Forge made this asset: there may be more in its file than this version reads. What is read
        /// plays; saving the asset here writes only that, and what this version does not know is lost.
        /// </summary>
        public bool IsFromNewerVersion => format > CurrentFormat;

        void OnEnable() => SfxAssetFormat.WarnIfNewer(this, format, CurrentFormat);

        [Tooltip("The 16 sounds the song plays (pads 1-16 of the device).")]
        public SfxPatch[] sounds = new SfxPatch[SfxProtocol.Slots];

        [Tooltip("The song as the device packs it (core/sfx_song.h).")]
        [HideInInspector]
        public byte[] data = new byte[0];

        /// <summary>
        /// There are 16 sounds and the packed song is one this version can read (SfxPackedSong.IsValid).
        /// </summary>
        public bool IsValid =>
            data != null && sounds != null && sounds.Length == SfxProtocol.Slots && SfxPackedSong.IsValid(data);

        /// <summary>
        /// The packed song begins with a format above the one this version knows (6) and is at least as long as the
        /// shortest song there is: a song from a newer SFX FORGE, not a broken one. It is not valid here all the same –
        /// it does not play, and nothing of it shows.
        /// </summary>
        public bool IsNewer => SfxProtocol.IsNewerSong(data);

        /// <summary>The format of the packed song: 1..6, 0 when it is not a song this version reads.</summary>
        public int PackedFormat => SfxPackedSong.Format(data);

        /// <summary>Bars in its arrangement (0: none, it walks its chain).</summary>
        public int ArrangementBars => IsValid && data[0] >= 4 ? data[Arr] : 0;

        // (format 4 kept one lane target for each sound before it)
        int Arr => data[0] == 4 ? SfxPackedSong.HeadArrangement + SfxProtocol.Slots : SfxPackedSong.HeadArrangement;

        /// <summary>The pattern (0..7) of a bar of the arrangement.</summary>
        public int ArrangementPattern(int bar) => bar >= 0 && bar < ArrangementBars ? data[Arr + 1 + bar] : -1;

        /// <summary>How many automation lanes move a knob of this sound (a song holds 24 in all).</summary>
        public int AutomationLanes(int sound)
        {
            if (!IsValid || sound < 0 || sound >= SfxProtocol.Slots)
            {
                return 0;
            }

            if (data[0] == 4)
            {
                return data[SfxPackedSong.HeadArrangement + sound] != 0 ? 1 : 0;
            }

            int n = 0;
            if (data[0] >= 5)
            {
                for (int l = 0; l < Lanes; l++)
                {
                    // (a lane: the sound + 1, then the target - as LaneSound and LaneTarget read them)
                    if (data[Arr + 1 + MaxBars + l * 2] == sound + 1 && data[Arr + 2 + MaxBars + l * 2] != 0)
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        /// <summary>It carries a mix (faders, pans, mutes, master effects): a song from firmware 0.6 on.</summary>
        public bool HasMix => IsValid && data[0] >= 2;

        /// <summary>A sound's fader: 1 = as designed, up to 1.27.</summary>
        public float Volume(int sound) =>
            HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[SfxPackedSong.HeadVolumes + sound] / 100f : 1f;

        /// <summary>A sound's pan: -1 left .. 0 the middle .. 1 right.</summary>
        public float Pan(int sound) =>
            HasMix && sound >= 0 && sound < SfxProtocol.Slots
                ? Mathf.Clamp((data[SfxPackedSong.HeadPans + sound] - 64) / 63f, -1f, 1f)
                : 0f;

        /// <summary>
        /// The bar in sixteenths, 1..16 - the steps of a pattern whose grid is sixteenths (a finer one has 2 or 4 in
        /// each: GridSteps).
        /// </summary>
        public int StepsPerPattern => IsValid ? data[SfxPackedSong.HeadBar] : 0;

        /// <summary>The tempo, in beats a minute.</summary>
        public int Bpm => IsValid ? data[SfxPackedSong.HeadBpm] << 7 | data[SfxPackedSong.HeadBpm + 1] : 0;

        /// <summary>How many patterns it walks through by itself (1 = it stays on one).</summary>
        public int Chain => IsValid ? data[SfxPackedSong.HeadChain] : 0;

        /// <summary>From which intensity (0..1) a sound plays: 0 = always.</summary>
        public float Layer(int sound) =>
            IsValid && sound >= 0 && sound < SfxProtocol.Slots ? data[SfxPackedSong.HeadLayers + sound] / 127f : 0f;

        // ---- reading the packed song: what an Inspector shows. Nothing here changes it. -----------------------------

        /// <summary>The most sixteenths a bar can have (this song's own is StepsPerPattern).</summary>
        public const int MaxSteps = SfxProtocol.Steps;

        /// <summary>The automation lanes of a song.</summary>
        public const int Lanes = SfxProtocol.Lanes;

        /// <summary>The most bars of an arrangement.</summary>
        public const int MaxBars = SfxProtocol.Bars;

        /// <summary>The most steps a pattern holds: 16 sixteenths, each cut in up to 4.</summary>
        public const int MaxGridSteps = SfxProtocol.GridSteps;

        /// <summary>How far the off-beat steps wait: 0..100.</summary>
        public int Swing => IsValid ? data[SfxPackedSong.HeadSwing] : 0;

        /// <summary>
        /// The scale notes were entered in on the device: 0 chromatic, 1 major, 2 minor, 3 pentatonic.
        /// </summary>
        public int Scale => IsValid ? data[SfxPackedSong.HeadScale] : 0;

        /// <summary>A sound's layer mark as the song keeps it: 0..127 (0 = always).</summary>
        public int LayerRaw(int sound) =>
            IsValid && sound >= 0 && sound < SfxProtocol.Slots ? data[SfxPackedSong.HeadLayers + sound] : 0;

        /// <summary>A sound's fader as the song keeps it: 0..127, 100 = as designed.</summary>
        public int VolumeRaw(int sound) =>
            HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[SfxPackedSong.HeadVolumes + sound] : 100;

        /// <summary>A sound's pan as the song keeps it: 0 left .. 64 the middle .. 127 right.</summary>
        public int PanRaw(int sound) =>
            HasMix && sound >= 0 && sound < SfxProtocol.Slots ? data[SfxPackedSong.HeadPans + sound] : 64;

        /// <summary>The sound is muted in the song's mix.</summary>
        public bool Muted(int sound)
        {
            if (!HasMix || sound < 0 || sound >= SfxProtocol.Slots)
            {
                return false;
            }

            int at = 7 + 3 * SfxProtocol.Slots;
            return ((data[at] | data[at + 1] << 7 | (data[at + 2] & 3) << 14) >> sound & 1) != 0;
        }

        /// <summary>
        /// A master effect as the song keeps it: delay time 0..5 (1, 2, 3, 4, 6, 8 sixteenths), the others 0..127.
        /// </summary>
        public int Master(SfxMaster what)
        {
            int i = (int)what;
            if (i < 0 || i > (int)SfxMaster.Drive)
            {
                return 0;
            }

            return HasMix ? data[SfxPackedSong.HeadMaster + i] : MasterPlain[i];
        }

        // (a song from before the mixer plays with these)
        static readonly byte[] MasterPlain = { 2, 64, 91, 40, 0 };

        // ---- the steps. A pattern is a bar; its grid cuts every sixteenth in 1, 2 or 4 steps (16, 32, 64 steps a full
        // bar). The accessors without "Grid" in their name count in sixteenths, as they always did: for a pattern with
        // a finer grid they say what stands on the first step of that sixteenth. The Grid... ones count in the
        // pattern's own steps.

        /// <summary>
        /// The steps a full bar of this pattern (0..7) has: 16, 32 or 64. 0: not a song, no such pattern.
        /// </summary>
        public int Grid(int pattern) =>
            sounds != null && sounds.Length == SfxProtocol.Slots ? SfxPackedSong.Grid(data, pattern) : 0;

        /// <summary>The steps a sixteenth of this pattern has: 1, 2 or 4. 0: not a song, no such pattern.</summary>
        public int StepsPerSixteenth(int pattern) =>
            sounds != null && sounds.Length == SfxProtocol.Slots ? SfxPackedSong.Div(data, pattern) : 0;

        /// <summary>
        /// The steps this pattern plays: the song's bar (StepsPerPattern sixteenths) in the pattern's grid.
        /// </summary>
        public int GridSteps(int pattern) => StepsPerPattern * StepsPerSixteenth(pattern);

        /// <summary>A pattern of the song has a grid finer than sixteenths.</summary>
        public bool HasFinerGrid
        {
            get
            {
                for (int p = 0; p < SfxProtocol.Patterns; p++)
                {
                    if (StepsPerSixteenth(p) > 1)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The sound plays on this step (0 .. Grid - 1, in the pattern's own grid) of this pattern (0..7).
        /// </summary>
        public bool GridStepOn(int pattern, int sound, int step) =>
            IsValid && SfxPackedSong.StepOn(data, pattern, sound, step);

        /// <summary>
        /// The note of a step of the pattern's grid: 0 = the sound as designed (drums, effects), else a MIDI note.
        /// </summary>
        public int GridStepNote(int pattern, int sound, int step) =>
            IsValid ? SfxPackedSong.StepNote(data, pattern, sound, step) : 0;

        /// <summary>
        /// How hard a step of the pattern's grid is played: 0..7, soft .. full (5 = as it always was).
        /// </summary>
        public int GridStepVelocity(int pattern, int sound, int step) =>
            IsValid ? SfxPackedSong.StepVelocity(data, pattern, sound, step) : 5;

        /// <summary>
        /// How many steps of the pattern's grid a step holds: 0 = by itself (to the sound's end, its next note
        /// or the bar line).
        /// </summary>
        public int GridStepLength(int pattern, int sound, int step) =>
            IsValid ? SfxPackedSong.StepLength(data, pattern, sound, step) : 0;

        /// <summary>
        /// A lane's point on a step of the pattern's grid: -1 = none there (the point before holds), else
        /// the value 0..126.
        /// </summary>
        public int GridLanePoint(int pattern, int lane, int step) =>
            IsValid ? SfxPackedSong.LanePoint(data, pattern, lane, step) : -1;

        // the first step of sixteenth `step` (0..15) in the pattern's grid; -1: out of range
        int First(int pattern, int step) => step >= 0 && step < MaxSteps ? step * StepsPerSixteenth(pattern) : -1;

        /// <summary>
        /// The sound plays on this sixteenth (0..15) of this pattern (0..7): on its first step, in a pattern
        /// with a finer grid.
        /// </summary>
        public bool StepOn(int pattern, int sound, int step) => GridStepOn(pattern, sound, First(pattern, step));

        /// <summary>The note of a sixteenth: 0 = the sound as designed (drums, effects), else a MIDI note.</summary>
        public int StepNote(int pattern, int sound, int step) => GridStepNote(pattern, sound, First(pattern, step));

        /// <summary>How hard a sixteenth is played: 0..7, soft .. full (5 = as it always was).</summary>
        public int StepVelocity(int pattern, int sound, int step) =>
            GridStepVelocity(pattern, sound, First(pattern, step));

        /// <summary>
        /// How many sixteenths a step holds (rounded up in a pattern with a finer grid): 0 = by itself (to the sound's
        /// end, its next note or the bar line).
        /// </summary>
        public int StepLength(int pattern, int sound, int step)
        {
            int div = StepsPerSixteenth(pattern);
            return div > 0 ? (GridStepLength(pattern, sound, First(pattern, step)) + div - 1) / div : 0;
        }

        /// <summary>The sound (0..15) a lane (0..23) moves a knob of, -1: the lane is free.</summary>
        public int LaneSound(int lane)
        {
            if (!IsValid || lane < 0 || lane >= Lanes)
            {
                return -1;
            }

            if (data[0] == 4)
            {
                return lane < SfxProtocol.Slots && data[SfxPackedSong.HeadArrangement + lane] != 0 ? lane : -1;
            }

            if (data[0] < 5 || data[Arr + 2 + MaxBars + lane * 2] == 0)
            {
                return -1;
            }

            // (a lane without a sound is free, whatever it names)
            return data[Arr + 1 + MaxBars + lane * 2] - 1;
        }

        /// <summary>
        /// Which knob a lane moves (SfxAutoTarget): 0 = none, 1..25 the sound's own values (SfxParam + 1), then its
        /// fader, its pan, the master's five. A free lane (LaneSound -1) moves nothing: 0.
        /// </summary>
        public int LaneTarget(int lane)
        {
            if (!IsValid || lane < 0 || lane >= Lanes)
            {
                return 0;
            }

            if (data[0] == 4)
            {
                return lane < SfxProtocol.Slots ? data[SfxPackedSong.HeadArrangement + lane] : 0;
            }

            // (a lane without a sound is free, whatever it names)
            if (data[0] < 5 || data[Arr + 1 + MaxBars + lane * 2] == 0)
            {
                return 0;
            }

            return data[Arr + 2 + MaxBars + lane * 2];
        }

        /// <summary>
        /// A lane's point on a sixteenth (0..15) of a pattern - on its first step, in a pattern with a finer grid: -1 =
        /// none there (the point before holds), else the value 0..126.
        /// </summary>
        public int LanePoint(int pattern, int lane, int step) => GridLanePoint(pattern, lane, First(pattern, step));

        /// <summary>Takes a song as it came from the device: copies of its 16 sounds and of its packed bytes.</summary>
        public void Set(SfxPatch[] newSounds, byte[] packedSong)
        {
            sounds = (SfxPatch[])newSounds.Clone();
            data = (byte[])packedSong.Clone();

            // (the asset's number follows the packed song: 2 for format 6)
            format = Need;
        }
    }
}
