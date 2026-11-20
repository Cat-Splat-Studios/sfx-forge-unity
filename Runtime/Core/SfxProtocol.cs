// SPDX-License-Identifier: MIT
using System;

namespace CatSplat.SfxForge
{
    /// <summary>The song's master effects (core/sfx_song.h, SFX_M_*).</summary>
    public enum SfxMaster { DelayTime, DelayFeedback, ReverbSize, ReverbDamping, Drive }
    /// <summary>What a sound's automation lane moves (core/sfx_song.h, SFX_AT_*): 1..25 are the sound's own values (SfxParam + 1).</summary>
    public static class SfxAutoTarget { public const int None = 0, FirstParam = 1, Volume = 26, Pan = 27, FirstMaster = 28, Count = 33; }
    // (a song holds 24 lanes: each moves one knob of one sound, so a sound may have several)

    /// <summary>What a game sends the device while it plays (CC 10..17 on channel 2).</summary>
    public enum SfxTelemetry { Pitch, Cutoff, FmDepth, Level, Intensity, Pattern, Aux3, Aux4 }

    /// <summary>What moves a sounding voice: the device's "Mod" parameters, the same for a local SfxPlayer.</summary>
    public struct SfxMods
    {
        public float pitchSemitones, cutoffOctaves, fm, level;
        public static SfxMods None => new SfxMods { level = 1f };
    }

    /// <summary>The MIDI between SFX FORGE and a game engine (README.md, "The link").</summary>
    public static class SfxProtocol
    {
        public const int Slots = 16;
        public const int PadNoteBase = 36;          // pads <-> notes 36..51
        public const int TelemetryCcBase = 10;      // game -> device, channel 2
        public const int EncoderCcBase = 20;        // device -> game, channel 1: CC 20..25
        public const int EncoderCount = 6;
        public const byte TelemetryStatus = 0xB1;   // control change, channel 2
        public const int MaxText = 24;

        public const byte Dump = 0x01, Load = 0x02, Text = 0x03, Request = 0x04, SongChunk = 0x05, RequestSong = 0x06;
        public const byte RequestIdentity = 0x07, Identity = 0x08;      // firmware 1.4.0 on: an older one ignores the request
        public const int IdentityBytes = 18;        // what an identity says today; a later one may say more, at its end
        public const int SongVersion = 5;           // the first byte of a packed song (core/sfx_song.h, SFX_SONG_VERSION)
        // what SFX FORGE 1.x holds on VIBE FM (core/sfx_song.h): SfxProfile.VibeFm1 is made of these
        public const int Kits = 2, Steps = 16, Lanes = 24, Bars = 32, Voices = 16;
        public const int SongBytesV1 = 2455;        // a song from before the mixer: still played
        public const int SongBytesV2 = 2498;        // … and one from before velocity and length
        public const int SongBytesV3 = 4546;        // … and one from before automation and the arrangement
        public const int SongBytesV4 = 6643;        // … and one with a single automation lane for each sound
        public const int SongBytes = 7699;          // a packed song (core/sfx_song.h)
        public const int SongChunkBytes = 200;
        public const int SongChunks = (SongBytes + SongChunkBytes - 1) / SongChunkBytes;
        public const int Patterns = 8;
        static readonly byte[] Header = { 0xF0, 0x7D, 0x56, 0x42 };

        public static bool IsOurs(byte[] m)
        {
            if (m == null || m.Length < 6 || m[m.Length - 1] != 0xF7) return false;
            for (int i = 0; i < Header.Length; i++) if (m[i] != Header[i]) return false;
            return true;
        }

        /// <summary>F0 7D 56 42 02 slot [28] F7 – a patch into a pad of the device.</summary>
        public static byte[] BuildLoad(int slot, SfxPatch patch) => BuildPatch(Load, slot, patch);

        /// <summary>
        /// F0 7D 56 42 02 (16 + sound) [28] F7 – one of a song's 16 sounds: the pad of the device's MUSIC kit, whichever
        /// kit is selected there. Needs firmware 1.3.1; an older one ignores it.
        /// </summary>
        public static byte[] BuildLoadSongSound(int sound, SfxPatch patch)
        {
            byte[] m = BuildPatch(Load, sound, patch);
            m[5] = (byte)(Slots + sound);
            return m;
        }

        /// <summary>F0 7D 56 42 01 slot [28] F7 – what the device sends on EXPORT.</summary>
        public static byte[] BuildDump(int slot, SfxPatch patch) => BuildPatch(Dump, slot, patch);

        static byte[] BuildPatch(byte command, int slot, SfxPatch patch)
        {
            if (slot < 0 || slot >= Slots) throw new ArgumentOutOfRangeException(nameof(slot));
            var m = new byte[7 + SfxPatch.Bytes];
            Header.CopyTo(m, 0);
            m[4] = command; m[5] = (byte)slot;
            patch.Pack(m, 6);
            m[m.Length - 1] = 0xF7;
            return m;
        }

        public static byte[] BuildRequest(int slot)
        {
            if (slot < 0 || slot >= Slots) throw new ArgumentOutOfRangeException(nameof(slot));
            return new byte[] { 0xF0, 0x7D, 0x56, 0x42, Request, (byte)slot, 0xF7 };
        }

        /// <summary>Text for the device's top line: printable ASCII, 24 characters at most.</summary>
        public static byte[] BuildText(string text)
        {
            text = text ?? "";
            int n = Math.Min(text.Length, MaxText);
            var m = new byte[6 + n];
            Header.CopyTo(m, 0);
            m[4] = Text;
            for (int i = 0; i < n; i++) m[5 + i] = (byte)(text[i] >= 32 && text[i] < 127 ? text[i] : '?');
            m[m.Length - 1] = 0xF7;
            return m;
        }

        /// <summary>A dump or a load with a valid patch.</summary>
        public static bool TryParsePatch(byte[] m, out int slot, out SfxPatch patch)
        {
            slot = 0; patch = default;
            if (!IsOurs(m) || m.Length != 7 + SfxPatch.Bytes || (m[4] != Dump && m[4] != Load) || m[5] >= Slots) return false;
            slot = m[5];
            return SfxPatch.TryUnpack(m, 6, out patch);
        }

        public static byte[] BuildRequestSong() => new byte[] { 0xF0, 0x7D, 0x56, 0x42, RequestSong, 0xF7 };

        /// <summary>A packed song as its SysEx messages: F0 7D 56 42 05 chunk chunks [up to 200 bytes] F7.</summary>
        public static byte[][] BuildSong(byte[] song)
        {
            if (song == null || song.Length != SongBytes) throw new ArgumentException("not a packed song", nameof(song));
            var all = new byte[SongChunks][];
            for (int k = 0; k < SongChunks; k++)
            {
                int n = Math.Min(SongChunkBytes, SongBytes - k * SongChunkBytes);
                var m = new byte[8 + n];
                Header.CopyTo(m, 0);
                m[4] = SongChunk; m[5] = (byte)k; m[6] = (byte)SongChunks;
                Array.Copy(song, k * SongChunkBytes, m, 7, n);
                m[m.Length - 1] = 0xF7;
                all[k] = m;
            }
            return all;
        }

        /// <summary>
        /// One piece of a song: copies it into song (SongBytes long) where it belongs. True when this piece made the
        /// song whole. The pieces may come in any order: a browser's Web MIDI output can swap two neighbours (seen
        /// with the emulator, about one transfer in twenty). And more than once: a piece that is in already, byte for
        /// byte, is left alone. A piece that is in already with OTHER bytes belongs to another song: that begins a new
        /// transfer. <paramref name="seen"/> remembers which pieces are in, also after the song was whole, so that its
        /// late twins do not begin one: start with 0, and set it back to 0 before asking for a song again or to forget
        /// a transfer that broke off (<see cref="SfxLink"/> does both, the second after two seconds without a piece).
        /// </summary>
        public static bool TryTakeSongChunk(byte[] m, byte[] song, ref ulong seen)
        {
            if (!IsOurs(m) || m[4] != SongChunk || m.Length < 9 || m[6] != SongChunks) return false;
            int k = m[5], n = m.Length - 8;
            if (k >= SongChunks || n != Math.Min(SongChunkBytes, SongBytes - k * SongChunkBytes)) return false;
            ulong piece = 1UL << k;
            if ((seen & piece) != 0)
            {
                bool same = true;
                for (int i = 0; i < n && same; i++) same = song[k * SongChunkBytes + i] == m[7 + i];
                if (same) return false;
                seen = 0;
            }
            Array.Copy(m, 7, song, k * SongChunkBytes, n);
            seen |= piece;
            return seen == (1UL << SongChunks) - 1;
        }

        /// <summary>
        /// A packed song of a format above this one's (SongVersion): at least as many bytes as a song has today,
        /// beginning with a higher number. A song from a newer SFX FORGE, not a broken one: a later format may be
        /// longer, it will not be shorter, so fewer bytes are a cut-off song whatever they begin with.
        /// </summary>
        public static bool IsNewerSong(byte[] song)
            => song != null && song.Length >= SongBytes && song[0] > SongVersion && song[0] <= 127;

        /// <summary>
        /// A message that carries a patch (a dump or a load) or the first piece of a song in a format above this
        /// one's: what it is and its number ("a patch in format 3"), null for any other message. A device with a
        /// newer firmware sends these; nothing of them is read here.
        /// </summary>
        public static string NewerIn(byte[] m)
        {
            if (!IsOurs(m)) return null;
            if ((m[4] == Dump || m[4] == Load) && SfxPatch.IsNewer(m, 6) && m.Length >= 7 + SfxPatch.Bytes) return "a patch in format " + m[6];
            if (m[4] == SongChunk && m.Length >= 9 && m[5] == 0 && m[7] > SongVersion && m[7] <= 127) return "a song in format " + m[7];
            return null;
        }

        /// <summary>F0 7D 56 42 07 F7 – asks the device what it is; the answer is an identity. A firmware before 1.4.0 does not answer.</summary>
        public static byte[] BuildRequestIdentity() => new byte[] { 0xF0, 0x7D, 0x56, 0x42, RequestIdentity, 0xF7 };

        /// <summary>
        /// F0 7D 56 42 08 [18] F7 – what a device answers: its hardware, its firmware's version, its profile (the formats
        /// it speaks and what it holds), its flags. Here for tests and for a device made in software. Every value is cut to 0..127.
        /// </summary>
        public static byte[] BuildIdentity(SfxDeviceInfo d)
        {
            var m = new byte[6 + IdentityBytes];
            Header.CopyTo(m, 0);
            m[4] = Identity;
            var p = d.profile;
            int[] v = { d.hardware, d.major, d.minor, d.patch, p.id, p.patchFormat, p.songFormat, p.sounds, p.kits, p.patterns, p.steps,
                        p.lanes, p.bars, p.voices, p.engines, p.wavetables, p.patchValues, d.flags };
            for (int i = 0; i < IdentityBytes; i++) m[5 + i] = (byte)(v[i] < 0 ? 0 : v[i] > 127 ? 127 : v[i]);
            m[m.Length - 1] = 0xF7;
            return m;
        }

        /// <summary>
        /// An identity: at least 18 bytes between the command and the F7, each 0..127. What follows the first 18 is from
        /// a later version and is not read (fields are only ever added at the end). Shorter, or a byte over 127: not one.
        /// </summary>
        public static bool TryParseIdentity(byte[] m, out SfxDeviceInfo device)
        {
            device = default;
            if (!IsOurs(m) || m[4] != Identity || m.Length < 6 + IdentityBytes) return false;
            for (int i = 5; i < m.Length - 1; i++) if (m[i] > 127) return false;
            device.hardware = m[5]; device.major = m[6]; device.minor = m[7]; device.patch = m[8];
            device.profile = new SfxProfile
            {
                id = m[9], patchFormat = m[10], songFormat = m[11], sounds = m[12], kits = m[13], patterns = m[14], steps = m[15],
                lanes = m[16], bars = m[17], voices = m[18], engines = m[19], wavetables = m[20], patchValues = m[21]
            };
            device.flags = m[22];
            return true;
        }

        /// <summary>The telemetry value that cues pattern 0..7 (A..H) on the device.</summary>
        public static float PatternValue(int pattern) => (Math.Max(0, Math.Min(Patterns - 1, pattern)) * 16 + 8) / 127f;

        /// <summary>A telemetry value 0..1 as its controller value.</summary>
        public static byte ToCc(float value01)
        {
            if (!(value01 > 0f)) return 0;                       // (NaN too)
            return value01 >= 1f ? (byte)127 : (byte)(value01 * 127f + 0.5f);
        }

        /// <summary>What a controller value does on the device – so a local player can do the same.</summary>
        public static void Apply(ref SfxMods mods, SfxTelemetry channel, byte cc)
        {
            switch (channel)
            {
                case SfxTelemetry.Pitch: mods.pitchSemitones = cc / 127f * 24f; break;
                case SfxTelemetry.Cutoff: mods.cutoffOctaves = Math.Max(-4f, Math.Min(4f, (cc - 64) / 16f)); break;
                case SfxTelemetry.FmDepth: mods.fm = cc / 127f; break;
                case SfxTelemetry.Level: mods.level = cc / 127f; break;
            }
        }
    }
}
