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

        public void Set(SfxPatch[] newSounds, byte[] packedSong)
        {
            sounds = (SfxPatch[])newSounds.Clone();
            data = (byte[])packedSong.Clone();
        }
    }
}
