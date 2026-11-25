// SPDX-License-Identifier: MIT
//
// The MIDI between SFX FORGE and a game engine (README.md, "The link"): every number of it, and the messages.
//   SfxMaster      the song's master effects
//   SfxAutoTarget  what an automation lane moves
//   SfxTelemetry   what a game sends while it plays
//   SfxMods        the four values that move a sounding voice
//   SfxProtocol    the constants, the Build... calls that make a message and the TryParse... calls that read one
// The firmware's side of it is sfx-forge/fw_protocol.h: a change there is a change here.
using System;

namespace CatSplat.SfxForge
{
    /// <summary>The song's master effects (core/sfx_song.h, SFX_M_*).</summary>
    public enum SfxMaster
    {
        DelayTime,
        DelayFeedback,
        ReverbSize,
        ReverbDamping,
        Drive,
    }

    /// <summary>
    /// What a sound's automation lane moves (core/sfx_song.h, SFX_AT_*): 1..25 are the sound's own
    /// values (SfxParam + 1).
    /// </summary>
    public static class SfxAutoTarget
    {
        /// <summary>The lane moves nothing.</summary>
        public const int None = 0;

        /// <summary>The first of the sound's own 25 values: SfxParam + 1.</summary>
        public const int FirstParam = 1;

        /// <summary>The sound's fader in the mix.</summary>
        public const int Volume = 26;

        /// <summary>The sound's pan in the mix.</summary>
        public const int Pan = 27;

        /// <summary>The first of the five master effects: FirstMaster + SfxMaster.</summary>
        public const int FirstMaster = 28;

        /// <summary>How many targets there are.</summary>
        public const int Count = 33;
    }

    // (a song holds 24 lanes: each moves one knob of one sound, so a sound may have several)

    /// <summary>What a game sends the device while it plays (CC 10..17 on channel 2).</summary>
    public enum SfxTelemetry
    {
        Pitch,
        Cutoff,
        FmDepth,
        Level,
        Intensity,
        Pattern,
        Aux3,
        Aux4,
    }

    /// <summary>What moves a sounding voice: the device's "Mod" parameters, the same for a local SfxPlayer.</summary>
    public struct SfxMods
    {
        /// <summary>Semitones added to the pitch.</summary>
        public float pitchSemitones;

        /// <summary>Octaves added to the filter.</summary>
        public float cutoffOctaves;

        /// <summary>Timbre added (FM depth, or a wavetable's position): 0..1 of the full range.</summary>
        public float fm;

        /// <summary>The level, 0..1.</summary>
        public float level;

        /// <summary>Nothing moved: all 0, level 1.</summary>
        public static SfxMods None => new SfxMods { level = 1f };
    }

    /// <summary>The MIDI between SFX FORGE and a game engine (README.md, "The link").</summary>
    public static class SfxProtocol
    {
        /// <summary>The sounds of a kit, and the pads.</summary>
        public const int Slots = 16;

        /// <summary>Pad 1's MIDI note: the pads are notes 36..51.</summary>
        public const int PadNoteBase = 36;

        /// <summary>The first telemetry controller: CC 10..17, game to device, channel 2.</summary>
        public const int TelemetryCcBase = 10;

        /// <summary>The first encoder's controller: CC 20..25, device to game, channel 1.</summary>
        public const int EncoderCcBase = 20;

        /// <summary>The encoders of the LINK page: 1-4, A, B.</summary>
        public const int EncoderCount = 6;

        /// <summary>The status byte of telemetry: a control change on channel 2.</summary>
        public const byte TelemetryStatus = 0xB1;

        /// <summary>The most characters of a text for the bottom line of the device's LINK page.</summary>
        public const int MaxText = 24;

        /// <summary>SysEx command: the device sends a patch (EXPORT, or the answer to Request).</summary>
        public const byte Dump = 0x01;

        /// <summary>SysEx command: a patch for the device to take.</summary>
        public const byte Load = 0x02;

        /// <summary>SysEx command: a text for the bottom line of the device's LINK page.</summary>
        public const byte Text = 0x03;

        /// <summary>SysEx command: asks the device for the patch of a pad.</summary>
        public const byte Request = 0x04;

        /// <summary>SysEx command: a piece of a song, either way.</summary>
        public const byte SongChunk = 0x05;

        /// <summary>SysEx command: asks the device for its song and its 16 sounds.</summary>
        public const byte RequestSong = 0x06;

        /// <summary>
        /// SysEx command: asks the device what it is (firmware 1.4.0 on: an older one ignores the request).
        /// </summary>
        public const byte RequestIdentity = 0x07;

        /// <summary>SysEx command: the device says what it is.</summary>
        public const byte Identity = 0x08;

        /// <summary>What an identity says today, in bytes; a later one may say more, at its end.</summary>
        public const int IdentityBytes = 18;

        /// <summary>The song format: the first byte of a packed song (core/sfx_song.h, SFX_SONG_VERSION).</summary>
        public const int SongVersion = 6;

        // what SFX FORGE 1.x holds on VIBE FM (core/sfx_song.h): SfxProfile.VibeFm1 is made of these
        /// <summary>The kits of sounds.</summary>
        public const int Kits = 2;

        /// <summary>The sixteenths in a bar at most (a song's own bar may be shorter).</summary>
        public const int Steps = 16;

        /// <summary>The automation lanes of a song.</summary>
        public const int Lanes = 24;

        /// <summary>The most bars of an arrangement.</summary>
        public const int Bars = 32;

        /// <summary>The voices that sound at once.</summary>
        public const int Voices = 16;

        /// <summary>
        /// The most steps a pattern holds: a bar of 16 sixteenths, each cut in up to 4 (firmware 1.6.0 on).
        /// </summary>
        public const int GridSteps = 64;

        /// <summary>The size of a packed song of format 1, from before the mixer: still played.</summary>
        public const int SongBytesV1 = 2455;

        /// <summary>... of format 2, from before velocity and length.</summary>
        public const int SongBytesV2 = 2498;

        /// <summary>... of format 3, from before automation and the arrangement.</summary>
        public const int SongBytesV3 = 4546;

        /// <summary>... of format 4, with a single automation lane for each sound.</summary>
        public const int SongBytesV4 = 6643;

        /// <summary>... of format 5, with 16 steps in every pattern and every row written.</summary>
        public const int SongBytesV5 = 7699;

        // A packed song of format 6 (firmware 1.6.0) keeps only the rows that are used, so it has no fixed size: a head
        // of 147 bytes, then a block for each of the 8 patterns - 211 bytes when the song is empty, 38 355 at the most.

        /// <summary>The head of a packed song of format 5 or 6, in bytes.</summary>
        public const int SongHead = 147;

        /// <summary>The shortest packed song of format 6: its head and eight empty patterns.</summary>
        public const int SongBytesMin = 211;

        /// <summary>The longest packed song of format 6.</summary>
        public const int SongBytesMax = 38355;

        /// <summary>The song message of firmware 1.6.0 on: pieces of up to 240 bytes, up to 160 of them.</summary>
        public const int SongPieceBytes = 240;

        /// <summary>The most pieces a song message of firmware 1.6.0 on has.</summary>
        public const int SongPiecesMax = 160;

        /// <summary>
        /// The song message up to firmware 1.5.0: 39 pieces of 200 bytes (the last 99) - a format-5 song.
        /// </summary>
        public const int OldSongPieceBytes = 200;

        /// <summary>The pieces of the song message up to firmware 1.5.0.</summary>
        public const int OldSongPieces = 39;

        /// <summary>Obsolete: the size of a format-5 song.</summary>
        [Obsolete("a song has no fixed size from format 6 on: SongBytesV5 (7699), SongBytesMin, SongBytesMax")]
        public const int SongBytes = 7699;

        /// <summary>Obsolete: the bytes of a piece of the old song message.</summary>
        [Obsolete("the old shape of the song message: SongPieceBytes")]
        public const int SongChunkBytes = 200;

        /// <summary>Obsolete: the pieces of the old song message.</summary>
        [Obsolete("the old shape of the song message: 39 pieces")]
        public const int SongChunks = 39;

        /// <summary>The patterns of a song: A..H.</summary>
        public const int Patterns = 8;
        static readonly byte[] Header = { 0xF0, 0x7D, 0x56, 0x42 };

        /// <summary>A SysEx message of SFX FORGE: it begins F0 7D 56 42, has a command and ends with F7.</summary>
        public static bool IsOurs(byte[] m)
        {
            if (m == null || m.Length < 6 || m[m.Length - 1] != 0xF7)
            {
                return false;
            }

            for (int i = 0; i < Header.Length; i++)
            {
                if (m[i] != Header[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>F0 7D 56 42 02 slot [28] F7 – a patch into a pad of the device.</summary>
        public static byte[] BuildLoad(int slot, SfxPatch patch) => BuildPatch(Load, slot, patch);

        /// <summary>
        /// F0 7D 56 42 02 (16 + sound) [28] F7 – one of a song's 16 sounds: the pad of the device's MUSIC kit,
        /// whichever kit is selected there. Needs firmware 1.3.1; an older one ignores it.
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
            if (slot < 0 || slot >= Slots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            var m = new byte[7 + SfxPatch.Bytes];
            Header.CopyTo(m, 0);
            m[4] = command;
            m[5] = (byte)slot;
            patch.Pack(m, 6);
            m[m.Length - 1] = 0xF7;
            return m;
        }

        /// <summary>The message that asks the device for the patch of a pad (slot 0..15).</summary>
        public static byte[] BuildRequest(int slot)
        {
            if (slot < 0 || slot >= Slots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return new byte[] { 0xF0, 0x7D, 0x56, 0x42, Request, (byte)slot, 0xF7 };
        }

        /// <summary>
        /// Text for the bottom line of the device's LINK page: printable ASCII, 24 characters at most.
        /// </summary>
        public static byte[] BuildText(string text)
        {
            text = text ?? "";
            int n = Math.Min(text.Length, MaxText);
            var m = new byte[6 + n];
            Header.CopyTo(m, 0);
            m[4] = Text;
            for (int i = 0; i < n; i++)
            {
                m[5 + i] = (byte)(text[i] >= 32 && text[i] < 127 ? text[i] : '?');
            }

            m[m.Length - 1] = 0xF7;
            return m;
        }

        /// <summary>A dump or a load with a valid patch.</summary>
        public static bool TryParsePatch(byte[] m, out int slot, out SfxPatch patch)
        {
            slot = 0;
            patch = default;
            if (!IsOurs(m) || m.Length != 7 + SfxPatch.Bytes || (m[4] != Dump && m[4] != Load) || m[5] >= Slots)
            {
                return false;
            }

            slot = m[5];
            return SfxPatch.TryUnpack(m, 6, out patch);
        }

        /// <summary>The message that asks the device for its song and its 16 sounds.</summary>
        public static byte[] BuildRequestSong() => new byte[] { 0xF0, 0x7D, 0x56, 0x42, RequestSong, 0xF7 };

        /// <summary>
        /// A format-5 song (7699 bytes) as its SysEx messages in the shape every firmware up to 1.5.0 takes:
        /// F0 7D 56 42 05 piece 39 [200 bytes, the last piece 99] F7. For a device that said it speaks song format 6,
        /// and for any other song, see <see cref="BuildSongPieces"/>.
        /// </summary>
        public static byte[][] BuildSong(byte[] song)
        {
            if (song == null || song.Length != SongBytesV5)
            {
                throw new ArgumentException("not a packed song of format 5 (7699 bytes)", nameof(song));
            }

            var all = new byte[OldSongPieces][];
            for (int k = 0; k < OldSongPieces; k++)
            {
                int n = Math.Min(OldSongPieceBytes, SongBytesV5 - k * OldSongPieceBytes);
                var m = new byte[8 + n];
                Header.CopyTo(m, 0);
                m[4] = SongChunk;
                m[5] = (byte)k;
                m[6] = (byte)OldSongPieces;
                Array.Copy(song, k * OldSongPieceBytes, m, 7, n);
                m[m.Length - 1] = 0xF7;
                all[k] = m;
            }

            return all;
        }

        /// <summary>
        /// A packed song of any format this version reads as its SysEx messages in the shape of firmware 1.6.0:
        /// F0 7D 56 42 05 [piece lo] [piece hi] [format] [pieces lo] [pieces hi] [up to 240 bytes] F7 - the piece
        /// number and the count of pieces in 14 bits each, the song's first byte in every piece. A firmware before
        /// 1.6.0 ignores these messages without a word (SfxLink.SendSong does not send them to one).
        /// </summary>
        public static byte[][] BuildSongPieces(byte[] song)
        {
            if (!SfxPackedSong.IsValid(song))
            {
                throw new ArgumentException("not a packed song", nameof(song));
            }

            return BuildPieces(song);
        }

        // (any bytes in that shape: for a device made in software, and for tests)
        internal static byte[][] BuildPieces(byte[] song)
        {
            int count = (song.Length + SongPieceBytes - 1) / SongPieceBytes;
            var all = new byte[count][];
            for (int k = 0; k < count; k++)
            {
                int n = Math.Min(SongPieceBytes, song.Length - k * SongPieceBytes);
                var m = new byte[11 + n];
                Header.CopyTo(m, 0);
                m[4] = SongChunk;
                m[5] = (byte)(k & 0x7F);
                m[6] = (byte)(k >> 7);
                m[7] = song[0];
                m[8] = (byte)(count & 0x7F);
                m[9] = (byte)(count >> 7);
                Array.Copy(song, k * SongPieceBytes, m, 10, n);
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
        /// a transfer that broke off. This reads the old shape of the song message only (39 pieces of 200 bytes, a
        /// format-5 song of 7699 bytes): <see cref="SfxSongTransfer"/> takes both shapes.
        /// </summary>
        [Obsolete("the old shape of the song message only: SfxSongTransfer takes both")]
        public static bool TryTakeSongChunk(byte[] m, byte[] song, ref ulong seen)
        {
            const int SongChunks = OldSongPieces;
            const int SongChunkBytes = OldSongPieceBytes;
            const int SongBytes = SongBytesV5;
            if (!IsOurs(m) || m[4] != SongChunk || m.Length < 9 || m[6] != SongChunks)
            {
                return false;
            }

            int k = m[5];
            int n = m.Length - 8;
            if (k >= SongChunks || n != Math.Min(SongChunkBytes, SongBytes - k * SongChunkBytes))
            {
                return false;
            }

            ulong piece = 1UL << k;
            if ((seen & piece) != 0)
            {
                bool same = true;
                for (int i = 0; i < n && same; i++)
                {
                    same = song[k * SongChunkBytes + i] == m[7 + i];
                }

                if (same)
                {
                    return false;
                }

                seen = 0;
            }

            Array.Copy(m, 7, song, k * SongChunkBytes, n);
            seen |= piece;
            return seen == (1UL << SongChunks) - 1;
        }

        /// <summary>
        /// A packed song of a format above this one's (SongVersion): it begins with a higher number and is at least as
        /// long as the shortest song there is (211 bytes). A song from a newer SFX FORGE, not a broken one: a later
        /// format will not be shorter than an empty song is today, so fewer bytes are a cut-off song whatever
        /// they begin with.
        /// </summary>
        public static bool IsNewerSong(byte[] song) =>
            song != null && song.Length >= SongBytesMin && song[0] > SongVersion && song[0] <= 127;

        /// <summary>
        /// A message that carries a patch (a dump or a load) or the first piece of a song in a format above this
        /// one's: what it is and its number ("a patch in format 3"), null for any other message. A device with a
        /// newer firmware sends these; nothing of them is read here. (The song's format stands at the same place in
        /// both shapes of the song message: the old one's first song byte, the new one's format byte. Piece 128 of the
        /// new shape has a 0 where piece 0 has one, and a 1 after it: it is not taken for a first piece.)
        /// </summary>
        public static string NewerIn(byte[] m)
        {
            if (!IsOurs(m))
            {
                return null;
            }

            if ((m[4] == Dump || m[4] == Load) && SfxPatch.IsNewer(m, 6) && m.Length >= 7 + SfxPatch.Bytes)
            {
                return "a patch in format " + m[6];
            }

            if (m[4] == SongChunk && m.Length >= 9 && m[5] == 0 && m[6] != 1 && m[7] > SongVersion && m[7] <= 127)
            {
                return "a song in format " + m[7];
            }

            return null;
        }

        /// <summary>
        /// F0 7D 56 42 07 F7 – asks the device what it is; the answer is an identity. A firmware before 1.4.0
        /// does not answer.
        /// </summary>
        public static byte[] BuildRequestIdentity() => new byte[] { 0xF0, 0x7D, 0x56, 0x42, RequestIdentity, 0xF7 };

        /// <summary>
        /// F0 7D 56 42 08 [18] F7 – what a device answers: its hardware, its firmware's version, its profile (the
        /// formats it speaks and what it holds), its flags. Here for tests and for a device made in software. Every
        /// value is cut to 0..127.
        /// </summary>
        public static byte[] BuildIdentity(SfxDeviceInfo d)
        {
            var m = new byte[6 + IdentityBytes];
            Header.CopyTo(m, 0);
            m[4] = Identity;
            var p = d.profile;
            int[] v =
            {
                d.hardware,
                d.major,
                d.minor,
                d.patch,
                p.id,
                p.patchFormat,
                p.songFormat,
                p.sounds,
                p.kits,
                p.patterns,
                p.steps,
                p.lanes,
                p.bars,
                p.voices,
                p.engines,
                p.wavetables,
                p.patchValues,
                d.flags,
            };
            for (int i = 0; i < IdentityBytes; i++)
            {
                m[5 + i] = (byte)(
                    v[i] < 0 ? 0
                    : v[i] > 127 ? 127
                    : v[i]
                );
            }

            m[m.Length - 1] = 0xF7;
            return m;
        }

        /// <summary>
        /// An identity: at least 18 bytes between the command and the F7, each 0..127. What follows the first 18
        /// is from a later version and is not read (fields are only ever added at the end). Shorter, or a byte
        /// over 127: not one.
        /// </summary>
        public static bool TryParseIdentity(byte[] m, out SfxDeviceInfo device)
        {
            device = default;
            if (!IsOurs(m) || m[4] != Identity || m.Length < 6 + IdentityBytes)
            {
                return false;
            }

            for (int i = 5; i < m.Length - 1; i++)
            {
                if (m[i] > 127)
                {
                    return false;
                }
            }

            device.hardware = m[5];
            device.major = m[6];
            device.minor = m[7];
            device.patch = m[8];
            device.profile = new SfxProfile
            {
                id = m[9],
                patchFormat = m[10],
                songFormat = m[11],
                sounds = m[12],
                kits = m[13],
                patterns = m[14],
                steps = m[15],
                lanes = m[16],
                bars = m[17],
                voices = m[18],
                engines = m[19],
                wavetables = m[20],
                patchValues = m[21],
            };
            device.flags = m[22];
            return true;
        }

        /// <summary>The telemetry value that cues pattern 0..7 (A..H) on the device.</summary>
        public static float PatternValue(int pattern) => (Math.Max(0, Math.Min(Patterns - 1, pattern)) * 16 + 8) / 127f;

        /// <summary>A telemetry value 0..1 as its controller value.</summary>
        public static byte ToCc(float value01)
        {
            // (NaN too)
            if (!(value01 > 0f))
            {
                return 0;
            }

            return value01 >= 1f ? (byte)127 : (byte)(value01 * 127f + 0.5f);
        }

        /// <summary>What a controller value does on the device – so a local player can do the same.</summary>
        public static void Apply(ref SfxMods mods, SfxTelemetry channel, byte cc)
        {
            switch (channel)
            {
                case SfxTelemetry.Pitch:
                    mods.pitchSemitones = cc / 127f * 24f;
                    break;
                case SfxTelemetry.Cutoff:
                    mods.cutoffOctaves = Math.Max(-4f, Math.Min(4f, (cc - 64) / 16f));
                    break;
                case SfxTelemetry.FmDepth:
                    mods.fm = cc / 127f;
                    break;
                case SfxTelemetry.Level:
                    mods.level = cc / 127f;
                    break;
            }
        }
    }
}
